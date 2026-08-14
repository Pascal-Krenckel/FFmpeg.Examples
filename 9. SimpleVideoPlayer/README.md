# 9. Simple Video Player

This example demonstrates how to drive FFmpegDotNet's **`PlaybackEngine`** class
from a real-time, interactive GUI application, rather than the batch
decode/encode pipelines shown in the earlier examples.

Unlike examples 1–3 and 5–6, which decode or encode a file start-to-finish as
fast as possible, a player has to run in real time: it has to pace itself to
wall-clock time, keep audio and video in sync, and respond to user input
(pause, seek, volume) at any moment mid-stream. `PlaybackEngine` is
FFmpegDotNet's high-level answer to that problem — it owns buffering, clocking,
and frame delivery so the application only has to render what it's given and
forward transport commands.

The example:

* Opens a media file and plays it back with synchronized audio and video.
* Renders decoded video frames with **SkiaSharp** (`SKGLElement`) in a WPF control.
* Plays decoded audio with **NAudio 3.0** in WASAPI exclusive mode.
* Uses the audio device as the playback clock, so video timing follows what's
  actually audible rather than a free-running timer.
* Exposes play / pause / stop / seek / volume / mute / loop / playback-rate as
  a small, UI-framework-agnostic contract (`IVideoSource`) that the WPF control
  depends on instead of depending on `PlaybackEngine` or NAudio directly.

```text
┌─────────────────────────┐
│   VideoPlayerControl    │  ← WPF: transport UI, SKGLElement paint loop
└────────────┬────────────┘
             │ depends on
             ▼
      IVideoSource            ← the only thing the UI knows about
             ▲
             │ implemented by
┌────────────┴──────────────┐
│   NAudio.MediaPlayer      │
└──┬─────────────────────┬──┘
   │                     │
   ▼                     ▼
PlaybackEngine      NAudioPlayer<WasapiPlayer>
(demux + decode)     (IMediaClock + audio out)
   │                     │
   │ VideoFrameReady     │ drives engine.Clock
   ▼                     ▼
 SKBitmap Frame      WASAPI (exclusive mode)
```

## Why `IVideoSource` instead of using `PlaybackEngine` directly?

Every other example in this repository calls `PlaybackEngine` (or the lower
FFmpeg building blocks) straight from `Main`. That's fine for a console
program that only ever does one thing. A player control is different: the WPF
layer needs to bind to playback state, raise events, and be testable/reusable
without pulling FFmpeg or WASAPI into the picture.

So the example splits into two halves:

* **`IVideoSource`** — a small interface (`Open`, `Play`, `Pause`, `Stop`,
  `Seek`, `Position`, `Duration`, `Volume`, `Frame`, plus a handful of events)
  that describes what *any* backend needs to provide to be playable.
* **`NAudio.MediaPlayer`** — the concrete implementation of `IVideoSource` for
  this example, built on `PlaybackEngine` + NAudio.

`VideoPlayerControl` only ever talks to `IVideoSource`. It has no idea
`PlaybackEngine` or NAudio exist. That's what makes it possible to swap the
backend later (a different audio stack, a hardware-decode pipeline, a mock
source for UI testing) without touching the control at all.

## Audio is the clock

`NAudioPlayer<T>` implements `PlaybackEngine`'s `IMediaClock` on top of the
WASAPI player, and `MediaPlayer.Open` assigns it to `engine.Clock`:

```csharp
engine.Clock = audioPlayer;
```

Its `Position` is `AudioPlayer.GetPositionTimeSpan() + ptsOffset` — i.e.
wherever WASAPI has actually gotten to, not a separate stopwatch running
alongside it. This is the standard approach for AV sync: an audio glitch is
far more noticeable than a video frame landing a little early or late, so
`PlaybackEngine` paces video output to match the audio clock rather than the
reverse. `ClockChanged` bubbles up through `MediaPlayer.PositionChanged`,
which the control listens to in order to keep its `Position` property and the
scrub bar in sync while playing.

## Format negotiation happens once, at open

Not every source's native sample format is something WASAPI's exclusive mode
will accept as-is. `MediaPlayer.Open` asks the device what it supports and,
if the source's format isn't directly usable, either takes the closest match
WASAPI offers or asks for a supported exclusive format outright — then builds
an `aformat`/`aresample` FFmpeg filter string so `PlaybackEngine` hands NAudio
audio it can actually play:

```csharp
string aformat = $"aformat=sample_fmts={waveFormat.SampleFormat.ToFFmpegString()}:channel_layouts={waveFormat.ChannelLayout}";
string aresample = $"aresample={waveFormat.SampleRate}";
```

Doing this once at open time — rather than converting per-buffer at
playback time — keeps the real-time audio path simple.

## Rendering path

`PlaybackEngine` raises `VideoFrameReady` as frames become available;
`MediaPlayer` reads the frame and copies it into a single persistent
`SKBitmap` exposed as `IVideoSource.Frame`:

```csharp
private void Engine_VideoFrameReady(object? sender, EventArgs e)
{
    using var frame = engine?.ReadVideo();
    frame?.CopyTo(Frame);
}
```

`VideoPlayerControl` draws that bitmap directly in its `SKGLElement.PaintSurface`
handler. This is the simple, CPU-side path — decode → system memory →
`SKBitmap` → GPU texture upload on every paint — rather than a zero-copy path
that would hand FFmpeg's hardware-decoded frames straight to Skia's GPU
context. That's a legitimate next step for this example (see FFmpegDotNet's
hardware-decode support and `SkiaSharp`'s GL/Vulkan backends), just not one
this example takes on, to keep the wiring easy to follow.

## Running the example

This example is a WPF application rather than a console tool, so there's no
`input`/`output` pair to pass on the command line. Run it like any WPF
project:

```bash
dotnet run --project "9. SimpleVideoPlayer"
```

Then open a video or audio file — `VideoPlayerControl.UriSource` is the entry
point either way:

```csharp
Player.UriSource = new Uri(@"C:\clips\sample.mp4");
```

If the sample's `MainWindow` doesn't already wire up an **Open** command, a
minimal one is just an `OpenFileDialog` setting `UriSource` to the chosen
path — `VideoPlayerControl.VideoSourceFactory` already defaults to
`NAudio.MediaPlayer`, so nothing else needs to be constructed by hand.

## Known limitations

* **`PlaybackRate` isn't implemented.** NAudio has no built-in rate control;
  the setter throws. Supporting it would mean adding an FFmpeg audio filter
  (e.g. `atempo`) and a matching adjustment on the video side.
* **`Frame` is a single shared, mutable `SKBitmap`**, written on the decode
  thread and read on the UI thread with no explicit double-buffering. In
  practice this rarely causes a visible glitch, but it's a corner deliberately
  left simple for this example rather than hardened for production use.
* **CPU-only video path** — see "Rendering path" above.

> **Note:** As with the other examples in this repository, the goal here is
> to show how `PlaybackEngine` fits into a complete, working application, not
> to be a production-grade media player. A shipping player would likely add
> hardware-accelerated decode/render, proper frame double-buffering, and
> playback-rate support.