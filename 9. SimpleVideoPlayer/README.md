# VideoPlayerControl — FFmpegDotNet `PlaybackEngine` example

This is an example project showing how to drive FFmpegDotNet's
**`PlaybackEngine`** class from a WPF UI. It pairs `PlaybackEngine` with:

- **SkiaSharp** (`SKGLElement`) for video frame rendering, and
- **NAudio 3.0** (WASAPI exclusive-mode) for audio output and as the playback clock.

The goal is to demonstrate a real, working end-to-end wiring — open a file, decode,
render, hear audio, scrub, mute, loop — not to be a hardened production player.
Known rough edges are called out explicitly below rather than hidden.

## Project layout

| File | Role |
|---|---|
| `VideoPlayerControl.cs` | Lookless WPF control. Owns bindable transport state, commands, the timeline/volume sliders, and the `SKGLElement` paint loop. |
| `IVideoSource.cs` | The contract the control depends on — implemented by `NAudio.MediaPlayer`, not by the control itself. |
| `Themes/Generic.xaml` | Default control template: video surface + a floating transport bar. |
| `PlaybackStateToGlyphConverter.cs` | Play/pause glyph for the transport bar. |
| `SliderFillWidthConverter.cs` | Computes the scrub-bar fill width so it lines up with the thumb's center. |
| `NAudio/MediaPlayer.cs` | **The `IVideoSource` implementation.** Wraps `PlaybackEngine`, owns the WASAPI player, and exposes the decoded frame as an `SKBitmap`. |
| `NAudio/MediaPlayerWaveProvider.cs` | `IWaveProvider` adapter — pulls decoded audio out of `PlaybackEngine` on NAudio's terms. |
| `NAudio/NAudioPlayer.cs` | Implements `PlaybackEngine`'s `IMediaClock` on top of the WASAPI player, so **audio drives the playback clock**. |
| `NAudio/WaveFormatExtensions.cs` | Conversions between `PlaybackEngine`'s `SampleFormat`/`ChannelLayout` and NAudio's `WaveFormat`. |

## How the pieces fit together

```
VideoPlayerControl (WPF)
        │  depends on
        ▼
   IVideoSource  ───────────────────────────────────────────────┐
        ▲ implemented by                                        │
        │                                                        │
NAudio.MediaPlayer                                                │
        │                                                        │
        ├── FFmpeg.MediaPlayer.PlaybackEngine   (demux + decode)  │
        │        │                                                │
        │        ├── VideoFrameReady event → engine.ReadVideo()  │
        │        │      copied into a persistent SKBitmap        │
        │        │      (the `Frame` property IVideoSource        │
        │        │       exposes — see caveat below)              │
        │        │                                                │
        │        └── Clock = NAudioPlayer<WasapiPlayer>          │
        │                                                        │
        └── NAudioPlayer<WasapiPlayer> : IMediaClock              │
                 │                                                │
                 └── WasapiPlayer (exclusive mode) ────────────────┘
                          + MediaPlayerWaveProvider (IWaveProvider)
```

**Audio is the clock.** `NAudioPlayer<T>` implements `PlaybackEngine.IMediaClock`
and is assigned to `engine.Clock` in `MediaPlayer.Open`. Its `Position` is
`AudioPlayer.GetPositionTimeSpan() + ptsOffset` — i.e. playback position comes
from how much audio WASAPI has actually consumed, not a separate timer. This is
the standard approach for AV sync: audio underruns are far more perceptible than
a video frame being a little early or late, so video timing follows the audio
clock rather than the other way around. `ClockChanged` bubbles up through
`MediaPlayer.PositionChanged`, which `VideoPlayerControl` listens to in order to
keep `Position` and the scrub bar in sync.

**Format negotiation happens once, at open.** `MediaPlayer.Open` asks the WASAPI
player whether the source's native sample format is directly supported; if not,
it either takes the closest match WASAPI offers or asks for a supported
exclusive-mode format outright, then builds an `aformat`/`aresample` FFmpeg
filter string so `PlaybackEngine` hands NAudio audio it can actually play. This
sidesteps `IsFormatSupported` not being reliable in all cases (see the comment
in `Open`) and avoids doing format conversion per-buffer at runtime.

**Video frames are delivered as a persistent `SKBitmap`, not per-frame images.**
`IVideoSource.Frame` is a single long-lived `SKBitmap` that `Engine_VideoFrameReady`
copies each decoded frame into (`frame.CopyTo(Frame)`), and
`VideoPlayerControl.OnPaintSurface` draws directly via `canvas.DrawBitmap`. This
is deliberately the simple CPU path — decode → system memory → `SKBitmap` →
GPU upload each paint — rather than the zero-copy GPU-texture path (FFmpeg
hardware frames wired directly into Skia's GL context via `WGL_NV_DX_interop2`,
or an all-Vulkan pipeline). That's a real, meaningfully more complex upgrade;
see the "Known limitations / possible next steps" section.

## Requirements

- .NET 10 / C# 14 — `WaveFormatExtensions.cs` uses C# 14 extension members
  (`extension(WaveFormat waveFormat) { ... }`).
- **FFmpegDotNet** — provides `FFmpeg.MediaPlayer.PlaybackEngine`,
  `FFmpeg.Audio`, `FFmpeg.Skia`, `FFmpeg.Utils`, `FFmpeg.AutoGen`.
- **NAudio 3.0** — `WasapiPlayerBuilder`, `IWavePosition`, and the exclusive-mode
  format helpers used here are 3.0 APIs.
- **SkiaSharp.Views.WPF** — for `SKGLElement`.

## Usage

`VideoPlayerControl.VideoSourceFactory` already defaults to
`() => new _9._SimpleVideoPlayer.NAudio.MediaPlayer()`, so in the common case you
don't need to wire anything yourself — just point it at a file:

```xml
<vp:VideoPlayerControl
    x:Name="Player"
    UriSource="{Binding CurrentVideoUri}"
    Stretch="Uniform"
    AutoPlay="True"
    Volume="{Binding Volume, Mode=TwoWay}"
    MediaFailed="Player_MediaFailed"/>
```

To use a different `IVideoSource` implementation instead (e.g. swapping in a
GPU-interop decoder later), either assign `Player.VideoSource` directly, or
override the static factory before any control opens media:

```csharp
VideoPlayerControl.VideoSourceFactory = () => new MyOtherVideoSource();
```

## Known limitations / possible next steps

- **`PlaybackRate` isn't supported.** NAudio has no built-in rate control;
  `MediaPlayer.PlaybackRate`'s setter throws. Supporting it would mean adding
  an `atempo`/`rubberband`-style FFmpeg audio filter and a matching video-side
  rate adjustment — not wired up here.
- **`Frame` is a single shared, mutable `SKBitmap`**, written on the decode
  thread (`Engine_VideoFrameReady`) and read on the UI thread
  (`OnPaintSurface`) with no explicit synchronization or double-buffering.
  In practice this is usually fine — a torn frame is rare and self-correcting
  a frame later — but it's worth knowing about if you see occasional visual
  glitches, and it's the first thing to fix if this stops being a demo.
- **CPU-only video path.** Every frame goes decode → system memory → `SKBitmap`
  → GPU texture upload on each paint. Fine for typical resolutions/frame rates;
  for 4K/HDR/high-refresh content, wiring FFmpeg's hardware-decoded frames
  directly into Skia's GL (or Vulkan) context would remove that per-frame copy.
- **No buffering UI.** `IVideoSource` in this version doesn't surface
  buffering state to the control, so `PlaybackState.Buffering` is effectively
  unused today.
