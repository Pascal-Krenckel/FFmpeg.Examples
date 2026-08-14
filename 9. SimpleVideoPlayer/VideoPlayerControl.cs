using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using _9._SimpleVideoPlayer.NAudio;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace VideoPlayer.Controls
{
    /// <summary>
    /// A lookless video player control. Rendering is done with an SKGLElement
    /// (hardware-accelerated Skia surface); playback/decoding is delegated to
    /// an injected <see cref="IVideoSource"/>.
    ///
    /// The control owns: transport state exposed as bindable DependencyProperties,
    /// commands for XAML/keyboard binding, the paint loop that pulls frames off
    /// the source and letterboxes them onto the Skia canvas, and scrub/seek
    /// coordination with the timeline slider in its template.
    ///
    /// The control does NOT own: demuxing, decoding, clocking, or audio - all of
    /// that is IVideoSource's job.
    /// </summary>
    [TemplatePart(Name = PartSkiaElement, Type = typeof(SKGLElement))]
    [TemplatePart(Name = PartPlayPauseButton, Type = typeof(ButtonBase))]
    [TemplatePart(Name = PartTimelineSlider, Type = typeof(Slider))]
    [TemplatePart(Name = PartVolumeSlider, Type = typeof(Slider))]
    public class VideoPlayerControl : Control, IDisposable
    {
        private const string PartSkiaElement = "PART_SkiaElement";
        private const string PartPlayPauseButton = "PART_PlayPauseButton";
        private const string PartTimelineSlider = "PART_TimelineSlider";
        private const string PartVolumeSlider = "PART_VolumeSlider";

        private SKGLElement? _skiaElement;
        private Slider? _timelineSlider;
        private ButtonBase? _playPauseButton;
        private Slider? _volumeSlider;

        private bool _isUserScrubbing;
        private bool _isSyncingPosition;

        static VideoPlayerControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(VideoPlayerControl),
                new FrameworkPropertyMetadata(typeof(VideoPlayerControl)));
        }

        public VideoPlayerControl()
        {            
            RegisterCommandBindings();
            Unloaded += (_, _) => Dispose();
        }

        // ------------------------------------------------------------------
        // Template wiring
        // ------------------------------------------------------------------

        public override void OnApplyTemplate()
        {
            if (_skiaElement != null)
                _skiaElement.PaintSurface -= OnPaintSurface;
            if (_playPauseButton != null)
                _playPauseButton.Click -= OnPlayPauseButtonClick;
            if (_timelineSlider != null)
            {
                _timelineSlider.PreviewMouseDown -= OnTimelineDragStarted;
                _timelineSlider.PreviewMouseUp -= OnTimelineDragCompleted;
                _timelineSlider.ValueChanged -= OnTimelineValueChanged;                
            }

            base.OnApplyTemplate();

            _skiaElement = GetTemplateChild(PartSkiaElement) as SKGLElement;
            _playPauseButton = GetTemplateChild(PartPlayPauseButton) as ButtonBase;
            _timelineSlider = GetTemplateChild(PartTimelineSlider) as Slider;
            _volumeSlider = GetTemplateChild(PartVolumeSlider) as Slider;

            _skiaElement?.PaintSurface += OnPaintSurface;

            _playPauseButton?.Click += OnPlayPauseButtonClick;

            if (_timelineSlider != null)
            {
                _timelineSlider.Minimum = 0;
                _timelineSlider.PreviewMouseDown += OnTimelineDragStarted;
                _timelineSlider.PreviewMouseUp += OnTimelineDragCompleted;
                _timelineSlider.ValueChanged += OnTimelineValueChanged;
            }

            if (_volumeSlider != null)
            {
                _volumeSlider.Minimum = 0;
                _volumeSlider.Maximum = 1;
                _ = _volumeSlider.SetBinding(RangeBase.ValueProperty,
                    new System.Windows.Data.Binding(nameof(Volume)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
            }
        }

        private void OnPlayPauseButtonClick(object sender, RoutedEventArgs e) => TogglePlayPause();

        private void OnTimelineDragStarted(object sender, MouseButtonEventArgs e) => _isUserScrubbing = true;

        private void OnTimelineDragCompleted(object sender, MouseButtonEventArgs e)
        {
            if (!_isUserScrubbing)
                return;
            _isUserScrubbing = false;
            Seek(TimeSpan.FromSeconds(_timelineSlider!.Value));
        }

        private void OnTimelineValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // do not update if the control itself set the position or the user is currently dragging.
            if (_isSyncingPosition || _isUserScrubbing)
                return;
            // the user has clicked on the slider to set the value to a specific value.
            Seek(TimeSpan.FromSeconds(e.NewValue));
        }

        // ------------------------------------------------------------------
        // Dependency properties: media source
        // ------------------------------------------------------------------

        public static readonly DependencyProperty UriSourceProperty = DependencyProperty.Register(
            nameof(UriSource), typeof(Uri), typeof(VideoPlayerControl),
            new PropertyMetadata(null, OnUriSourceChanged));

        public Uri UriSource
        {
            get => (Uri)GetValue(UriSourceProperty);
            set => SetValue(UriSourceProperty, value);
        }

        private static void OnUriSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if (e.NewValue is Uri uri)
                _ = control.OpenAsync(uri);
            else
                control.VideoSource?.Close();
        }

        public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register(
            nameof(VideoSource), typeof(IVideoSource), typeof(VideoPlayerControl),
            new PropertyMetadata(null, OnVideoSourceChanged));

        /// <summary>
        /// The media engine backing this control. Assign your own
        /// IVideoSource implementation here (or let UriSource create a
        /// default one via a factory, see VideoSourceFactory).
        /// </summary>
        public IVideoSource VideoSource
        {
            get => (IVideoSource)GetValue(VideoSourceProperty);
            set => SetValue(VideoSourceProperty, value);
        }

        private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;

            if (e.OldValue is IVideoSource oldSource)
            {
                oldSource.MediaOpened -= control.OnSourceMediaOpened;
                oldSource.MediaEnded -= control.OnSourceMediaEnded;
                oldSource.MediaFailed -= control.OnSourceMediaFailed;
                oldSource.PositionChanged -= control.OnSourcePositionChanged;
            }

            if (e.NewValue is IVideoSource newSource)
            {
                newSource.MediaOpened += control.OnSourceMediaOpened;
                newSource.MediaEnded += control.OnSourceMediaEnded;
                newSource.MediaFailed += control.OnSourceMediaFailed;
                newSource.PositionChanged += control.OnSourcePositionChanged;
            }
        }

        // ------------------------------------------------------------------
        // Dependency properties: playback state (read-only, driven by source)
        // ------------------------------------------------------------------

        private static readonly DependencyPropertyKey PlaybackStatePropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(PlaybackState), typeof(PlaybackState), typeof(VideoPlayerControl),
            new PropertyMetadata(PlaybackState.Ended, OnPlaybackStateChanged));

        public static readonly DependencyProperty PlaybackStateProperty = PlaybackStatePropertyKey.DependencyProperty;

        public PlaybackState PlaybackState
        {
            get => (PlaybackState)GetValue(PlaybackStateProperty);
            private set => SetValue(PlaybackStatePropertyKey, value);
        }

        private static void OnPlaybackStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if ((PlaybackState)e.NewValue == PlaybackState.Playing)
                control._skiaElement?.RenderContinuously = true;
            else
                control._skiaElement?.RenderContinuously = false;
        }

        private static readonly DependencyPropertyKey DurationPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(Duration), typeof(TimeSpan), typeof(VideoPlayerControl),
            new PropertyMetadata(TimeSpan.Zero));

        public static readonly DependencyProperty DurationProperty = DurationPropertyKey.DependencyProperty;

        public TimeSpan Duration
        {
            get => (TimeSpan)GetValue(DurationProperty);
            private set => SetValue(DurationPropertyKey, value);
        }

        // ------------------------------------------------------------------
        // Dependency properties: transport (read/write)
        // ------------------------------------------------------------------

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(TimeSpan), typeof(VideoPlayerControl),
            new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.None, OnPositionChanged, CoerceMediaPosition));

        /// <summary>
        /// Current playback position. Setting this seeks the underlying source
        /// (fire-and-forget - use SeekAsync directly if you need to await it).
        /// Also updated (without re-triggering a seek) as the source plays.
        /// </summary>
        public TimeSpan Position
        {
            get => (TimeSpan)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        private static object CoerceMediaPosition(DependencyObject d, object baseValue)
        {
            var control = (VideoPlayerControl)d;
            var value = (TimeSpan)baseValue;
            if (value < TimeSpan.Zero)
                return TimeSpan.Zero;
            if (control.Duration > TimeSpan.Zero && value > control.Duration)
                return control.Duration;
            return value;
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if (control._isSyncingPosition)
                return; // this update came FROM the source; don't seek again

            if (control._timelineSlider != null && !control._isUserScrubbing)
                control._timelineSlider.Value = ((TimeSpan)e.NewValue).TotalSeconds;

            control.Seek((TimeSpan)e.NewValue);
        }

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume), typeof(double), typeof(VideoPlayerControl),
            new PropertyMetadata(1.0, OnVolumeChanged, CoerceVolume));

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        private static object CoerceVolume(DependencyObject d, object baseValue) =>
            Math.Clamp((double)baseValue, 0.0, 1.0);

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if (control.VideoSource != null)
                control.VideoSource.Volume = (double)e.NewValue;
        }

        public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(
            nameof(IsMuted), typeof(bool), typeof(VideoPlayerControl),
            new PropertyMetadata(false, OnIsMutedChanged));

        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }
        private bool _muted;
        private double _volume;
        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if (control.VideoSource != null)
            {                
                control._muted = (bool)e.NewValue;
                if (control._muted)
                {
                    control._volume = control.VideoSource.Volume;
                    control.VideoSource.Volume = 0;
                }
                else
                {
                    control.VideoSource.Volume = control._volume;
                    control._volume = -1;
                }
            }
        }

        public static readonly DependencyProperty PlaybackRateProperty = DependencyProperty.Register(
            nameof(PlaybackRate), typeof(double), typeof(VideoPlayerControl),
            new PropertyMetadata(1.0, OnPlaybackRateChanged));

        public double PlaybackRate
        {
            get => (double)GetValue(PlaybackRateProperty);
            set => SetValue(PlaybackRateProperty, value);
        }

        private static void OnPlaybackRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPlayerControl)d;
            if (control.VideoSource != null)
                control.VideoSource.PlaybackRate = (double)e.NewValue;
        }

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch), typeof(VideoStretch), typeof(VideoPlayerControl),
            new FrameworkPropertyMetadata(VideoStretch.Uniform, FrameworkPropertyMetadataOptions.AffectsRender));

        public VideoStretch Stretch
        {
            get => (VideoStretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
            nameof(IsLooping), typeof(bool), typeof(VideoPlayerControl), new PropertyMetadata(false));

        public bool IsLooping
        {
            get => (bool)GetValue(IsLoopingProperty);
            set => SetValue(IsLoopingProperty, value);
        }

        public static readonly DependencyProperty AutoPlayProperty = DependencyProperty.Register(
            nameof(AutoPlay), typeof(bool), typeof(VideoPlayerControl), new PropertyMetadata(true));

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        // ------------------------------------------------------------------
        // Routed events
        // ------------------------------------------------------------------

        public static readonly RoutedEvent MediaOpenedEvent = EventManager.RegisterRoutedEvent(
            nameof(MediaOpened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(VideoPlayerControl));
        public event RoutedEventHandler MediaOpened
        {
            add => AddHandler(MediaOpenedEvent, value);
            remove => RemoveHandler(MediaOpenedEvent, value);
        }

        public static readonly RoutedEvent MediaEndedEvent = EventManager.RegisterRoutedEvent(
            nameof(MediaEnded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(VideoPlayerControl));
        public event RoutedEventHandler MediaEnded
        {
            add => AddHandler(MediaEndedEvent, value);
            remove => RemoveHandler(MediaEndedEvent, value);
        }

        public static readonly RoutedEvent MediaFailedEvent = EventManager.RegisterRoutedEvent(
            nameof(MediaFailed), RoutingStrategy.Bubble, typeof(EventHandler<MediaFailedRoutedEventArgs>), typeof(VideoPlayerControl));
        public event EventHandler<MediaFailedRoutedEventArgs> MediaFailed
        {
            add => AddHandler(MediaFailedEvent, value);
            remove => RemoveHandler(MediaFailedEvent, value);
        }

        // ------------------------------------------------------------------
        // Commands
        // ------------------------------------------------------------------

        public static readonly RoutedUICommand PlayCommand = new("Play", nameof(PlayCommand), typeof(VideoPlayerControl));
        public static readonly RoutedUICommand PauseCommand = new("Pause", nameof(PauseCommand), typeof(VideoPlayerControl));
        public static readonly RoutedUICommand StopCommand = new("Stop", nameof(StopCommand), typeof(VideoPlayerControl));
        public static readonly RoutedUICommand TogglePlayPauseCommand = new("TogglePlayPause", nameof(TogglePlayPauseCommand), typeof(VideoPlayerControl),
            [new KeyGesture(Key.Space)]);
        public static readonly RoutedUICommand ToggleMuteCommand = new("ToggleMute", nameof(ToggleMuteCommand), typeof(VideoPlayerControl));
        public static readonly RoutedUICommand StepForwardCommand = new("StepForward", nameof(StepForwardCommand), typeof(VideoPlayerControl),
            [new KeyGesture(Key.Right)]);
        public static readonly RoutedUICommand StepBackwardCommand = new("StepBackward", nameof(StepBackwardCommand), typeof(VideoPlayerControl),
            [new KeyGesture(Key.Left)]);

        private void RegisterCommandBindings()
        {
            _ = CommandBindings.Add(new CommandBinding(PlayCommand, (_, _) => Play(), (_, e) => e.CanExecute = CanPlay()));
            _ = CommandBindings.Add(new CommandBinding(PauseCommand, (_, _) => Pause(), (_, e) => e.CanExecute = CanPause()));
            _ = CommandBindings.Add(new CommandBinding(StopCommand, (_, _) => Stop(), (_, e) => e.CanExecute = VideoSource != null));
            _ = CommandBindings.Add(new CommandBinding(TogglePlayPauseCommand, (_, _) => TogglePlayPause()));
            _ = CommandBindings.Add(new CommandBinding(ToggleMuteCommand, (_, _) => IsMuted = !IsMuted));
            _ = CommandBindings.Add(new CommandBinding(StepForwardCommand, (_, e) => Step(GetStepAmount(e.Parameter))));
            _ = CommandBindings.Add(new CommandBinding(StepBackwardCommand, (_, e) => Step(-GetStepAmount(e.Parameter))));
            this.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.M)
                ToggleMuteCommand.Execute(null, this);
        }
        private static TimeSpan GetStepAmount(object parameter) =>
            parameter is TimeSpan ts ? ts : TimeSpan.FromSeconds(10);

        private bool CanPlay() => VideoSource != null && PlaybackState != PlaybackState.Playing;
        private bool CanPause() => VideoSource != null && PlaybackState == PlaybackState.Playing;

        // ------------------------------------------------------------------
        // Public playback API
        // ------------------------------------------------------------------

        public System.Threading.Tasks.Task OpenAsync(Uri source)
        {
            VideoSource ??= VideoSourceFactory?.Invoke()
                ?? throw new InvalidOperationException(
                    $"{nameof(VideoSource)} is not set and no {nameof(VideoSourceFactory)} was provided.");

            return Task.Run(() => VideoSource.Open(source.ToString()));
        }

        public System.Threading.Tasks.Task OpenAsync(string source)
        {
            VideoSource ??= VideoSourceFactory?.Invoke()
                ?? throw new InvalidOperationException(
                    $"{nameof(VideoSource)} is not set and no {nameof(VideoSourceFactory)} was provided.");
            var capture = VideoSource;
            return Task.Run(() => capture.Open(source));
        }

        public void Play()
        {
            VideoSource?.Play();
            PlaybackState = PlaybackState.Playing;
        }

        public void Pause()
        {
            VideoSource?.Pause();
            PlaybackState = PlaybackState.Paused;
        }

        public void Stop()
        {
            VideoSource?.Stop();
            PlaybackState = PlaybackState.Stopped;
            Position = TimeSpan.Zero;
        }

        public void TogglePlayPause()
        {
            if (PlaybackState == PlaybackState.Playing)
                Pause();
            else
                Play();
        }

        public void Step(TimeSpan delta) => Position += delta;

        public void Seek(TimeSpan position)
        {
            if (VideoSource == null)
                return;
            VideoSource.Seek(position);
        }

        /// <summary>
        /// Optional factory used by OpenAsync(Uri) when VideoSource hasn't been
        /// assigned explicitly - lets you register "how do I build a decoder"
        /// once (e.g. in App.xaml.cs / DI container) instead of per control.
        /// </summary>
        public static Func<IVideoSource>? VideoSourceFactory { get; set; } = () => new _9._SimpleVideoPlayer.NAudio.MediaPlayer();

        // ------------------------------------------------------------------
        // Source event handlers -> control state
        // ------------------------------------------------------------------

        private void OnSourceMediaOpened(object? sender, EventArgs e)
        {
            Duration = VideoSource.Duration;
            if (_timelineSlider != null)
                _timelineSlider.Maximum = Duration.TotalSeconds;

            RaiseEvent(new RoutedEventArgs(MediaOpenedEvent, this));

            if (AutoPlay)
                Play();
        }

        private void OnSourceMediaEnded(object? sender, EventArgs e)
        {
            if (IsLooping)
            {
                Seek(TimeSpan.Zero);
                Play();
            }
            else
            {
                PlaybackState = PlaybackState.Ended;
            }

            RaiseEvent(new RoutedEventArgs(MediaEndedEvent, this));
        }

        private void OnSourceMediaFailed(object? sender, VideoSourceErrorEventArgs e)
        {
            PlaybackState = PlaybackState.Failed;
            RaiseEvent(new MediaFailedRoutedEventArgs(MediaFailedEvent, this, e.Exception, e.Message));
        }

        private void OnSourcePositionChanged(object? sender, EventArgs e)
        {
            _isSyncingPosition = true;
            try
            {
                Position = VideoSource.Position;
                if (_timelineSlider != null && !_isUserScrubbing)
                    _timelineSlider.Value = Position.TotalSeconds;
            }
            finally
            {
                _isSyncingPosition = false;
            }
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void OnPaintSurface(object? sender, SkiaSharp.Views.Desktop.SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Black);

            if (VideoSource == null)
                return;

            if (VideoSource.Frame.IsEmpty)
                return;
            OnSourcePositionChanged(this, EventArgs.Empty);
            var destRect = ComputeDestinationRect(e.Surface.Canvas.DeviceClipBounds, VideoSource.Frame.Info.Size, Stretch);
            canvas.DrawBitmap(VideoSource.Frame, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell));

        }

        /// <summary>
        /// Letterbox/pillarbox/crop math, kept as a static, independently
        /// testable method rather than buried in the paint callback.
        /// </summary>
        internal static SKRect ComputeDestinationRect(SKRect clipBounds, SKSize size, VideoStretch stretch)
        {

            switch (stretch)
            {
                case VideoStretch.Fill:
                    return clipBounds;

                case VideoStretch.None:
                    return SKRect.Create(clipBounds.MidX - size.Width / 2, clipBounds.MidY - size.Height / 2, size.Width, size.Height);


                case VideoStretch.UniformToFill:
                    return clipBounds.AspectFill(size);

                case VideoStretch.Uniform:
                default:
                    return clipBounds.AspectFit(size);
            }
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        public void Dispose()
        {
            if (_skiaElement != null)
                _skiaElement.PaintSurface -= OnPaintSurface;
            VideoSource?.Dispose();
        }
    }

    public sealed class MediaFailedRoutedEventArgs : RoutedEventArgs
    {
        public Exception Exception { get; }
        public string Message { get; }

        public MediaFailedRoutedEventArgs(RoutedEvent routedEvent, object source, Exception exception, string message)
            : base(routedEvent, source)
        {
            Exception = exception;
            Message = message;
        }
    }
}
