# VideoPlayerControl (SKGLElement-based)

A lookless WPF control for video playback, rendered with SkiaSharp's
`SKGLElement` (hardware-accelerated OpenGL surface). Decoding is delegated
to an `IVideoSource` you implement — this package only contains the control
and the contract, per the request.

## Files

| File | Purpose |
|---|---|
| `IVideoSource.cs` | The seam between control and decoder. `PlaybackState`, `VideoStretch`, `VideoFrame`, and the `IVideoSource` interface. |
| `VideoPlayerControl.cs` | The control: dependency properties, routed events, commands, template wiring, and the paint/letterbox logic. |
| `Themes/Generic.xaml` | Default `ControlTemplate` — SKGLElement + a minimal transport bar. |
| `PlaybackStateToGlyphConverter.cs` | Tiny converter used by the default template. |

## Design decisions

**Lookless control, not UserControl.** `VideoPlayerControl : Control` with
`Themes/Generic.xaml` means consumers can retemplate the whole chrome
(or supply none) while the SKGLElement + transport parts stay contractually
named (`PART_SkiaElement`, `PART_PlayPauseButton`, `PART_TimelineSlider`,
`PART_VolumeSlider`). This mirrors how `MediaElement`/`Slider`/`ScrollViewer`
are built in WPF, so it composes normally with styling systems.

**The control never decodes anything.** All of that lives behind
`IVideoSource`. The control's only jobs are:
- Own bindable playback state (`Position`, `Duration`, `Volume`, `PlaybackRate`, `Stretch`, ...).
- Drive a `CompositionTarget.Rendering` loop *only while `PlaybackState == Playing`*,
  invalidating the SKGLElement each frame. Paused/stopped/closed states don't
  spin the render loop — no wasted composition passes.
- On `PaintSurface`, ask the source for the current frame
  (`TryGetCurrentFrame`) and letterbox/pillarbox/crop it onto the canvas
  according to `Stretch`, then dispose the frame.
- Translate UI gestures (slider drag, keyboard, commands) into calls on
  `IVideoSource`, and translate the source's events back into DP changes —
  with re-entrancy guards (`_isSyncingPosition`, `_isUserScrubbing`) so
  "source says position changed" doesn't loop back into "seek to position."

**Frames are `SKImage`, not `SKBitmap`.** `SKGLElement` gives you a GPU
`GRContext` in `PaintSurface`'s `SKPaintGLSurfaceEventArgs.Surface.Context`.
The control hands that context to the source once
(`IVideoSource.Initialize(GRContext)`) so a real implementation can create
`SKImage`s that reference GPU textures directly (e.g. via
`SKImage.FromTexture`, wrapping a decoded NV12/RGBA texture from hardware
decode) instead of copying frame bytes through system memory every tick.
A CPU-only decoder can still conform to the interface trivially via
`SKImage.FromBitmap`.

**Commands over code-behind event handlers.** `PlayCommand`, `PauseCommand`,
`TogglePlayPauseCommand` (Space), `ToggleMuteCommand` (M),
`StepForwardCommand`/`StepBackwardCommand` (arrow keys) are static
`RoutedUICommand`s with default gestures, so keyboard shortcuts and
external UI (a menu, a global hotkey handler) can drive the control without
reaching into its internals.

**`PlaybackState` is control-owned, not a passthrough of the source's
enum**, deliberately: it's set optimistically when `Play()`/`Pause()` are
called (so a Play button flips state instantly) and corrected by the
source's `BufferingStarted/Ended`, `MediaEnded`, and `MediaFailed` events.
This keeps the UI responsive without waiting on a round-trip to the decoder
for simple state, while still reflecting buffering/ended/failed accurately.

**Things intentionally left out of the design**, since they're
implementation/model concerns, not control-surface concerns:
audio device selection, subtitle rendering, HDR tone-mapping, DRM,
network buffering policy, thumbnail/scrub-preview generation. Each of these
would show up as additions to `IVideoSource` (or a sibling interface) rather
than changes to the control.

## Usage

```xml
<Window
    xmlns:vp="clr-namespace:VideoPlayer.Controls;assembly=VideoPlayer.Controls">

    <vp:VideoPlayerControl
        x:Name="Player"
        UriSource="{Binding CurrentVideoUri}"
        Stretch="Uniform"
        AutoPlay="True"
        Volume="{Binding Volume, Mode=TwoWay}"
        MediaFailed="Player_MediaFailed"/>
</Window>
```

```csharp
// Wire a decoder implementation once, e.g. in App startup:
VideoPlayerControl.VideoSourceFactory = () => new FfmpegVideoSource();

// Or assign per-instance:
Player.VideoSource = new FfmpegVideoSource();
Player.UriSource = new Uri(@"C:\clips\sample.mp4");
```

### Mock IVideoSource for exercising the control without a real decoder

Useful for storyboard/UI work before the decoding backend exists:

```csharp
public sealed class SolidColorTestSource : IVideoSource
{
    private readonly System.Windows.Threading.DispatcherTimer _clock = new()
        { Interval = TimeSpan.FromMilliseconds(16) };
    private TimeSpan _position;
    private SKColor _color = SKColors.CornflowerBlue;
    private GRContext _context;

    public PlaybackState State { get; private set; } = PlaybackState.Closed;
    public TimeSpan Position => _position;
    public TimeSpan Duration { get; } = TimeSpan.FromSeconds(30);
    public System.Windows.Size NaturalSize { get; } = new(1920, 1080);
    public bool IsSeekable => true;
    public double PlaybackRate { get; set; } = 1.0;
    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; }

    public event EventHandler<EventArgs> MediaOpened;
    public event EventHandler<EventArgs> MediaEnded;
    public event EventHandler<VideoSourceErrorEventArgs> MediaFailed;
    public event EventHandler<EventArgs> PositionChanged;
    public event EventHandler<EventArgs> BufferingStarted;
    public event EventHandler<EventArgs> BufferingEnded;

    public void Initialize(GRContext context) => _context = context;

    public Task OpenAsync(Uri source, CancellationToken ct = default)
    {
        State = PlaybackState.Paused;
        MediaOpened?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Play()
    {
        State = PlaybackState.Playing;
        _clock.Tick += (_, _) =>
        {
            _position += TimeSpan.FromMilliseconds(16 * PlaybackRate);
            if (_position >= Duration) { _position = Duration; Stop(); MediaEnded?.Invoke(this, EventArgs.Empty); }
            PositionChanged?.Invoke(this, EventArgs.Empty);
        };
        _clock.Start();
    }

    public void Pause() { State = PlaybackState.Paused; _clock.Stop(); }
    public void Stop() { State = PlaybackState.Stopped; _clock.Stop(); _position = TimeSpan.Zero; }
    public void Close() { Stop(); State = PlaybackState.Closed; }

    public Task SeekAsync(TimeSpan position, CancellationToken ct = default)
    {
        _position = position;
        PositionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public bool TryGetCurrentFrame(out VideoFrame frame)
    {
        // Cycle hue over time just so you can see it's live.
        _color = SKColor.FromHsv((float)(_position.TotalSeconds * 12 % 360), 60, 90);

        var info = new SKImageInfo(1920, 1080);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(_color);
        frame = new VideoFrame(surface.Snapshot(), _position);
        return true;
    }

    public void Dispose() => _clock.Stop();
}
```

## Not implemented here (by design, per the request)

- Any actual demuxer/decoder/codec integration.
- Audio output/mixing.
- The `IVideoSource` implementation itself — only the contract it must satisfy.
