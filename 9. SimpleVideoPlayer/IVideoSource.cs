using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SkiaSharp;

namespace VideoPlayer.Controls
{
    /// <summary>
    /// Playback state as reported by the media engine.
    /// </summary>
    public enum PlaybackState
    {
        Closed,
        Opened,
        Playing,
        Paused,
        Stopped,
        Ended,
        Failed
    }

    /// <summary>
    /// How a decoded frame should be fit into the control's render area.
    /// Mirrors System.Windows.Media.Stretch so it feels familiar, but is
    /// declared locally to avoid pulling WPF's Stretch (which has no
    /// UniformToFill-vs-None distinction you'd want changed later).
    /// </summary>
    public enum VideoStretch
    {
        None,
        Fill,
        Uniform,
        UniformToFill
    }

    public sealed class VideoSourceErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string Message { get; }

        public VideoSourceErrorEventArgs(Exception exception, string message)
        {
            Exception = exception;
            Message = message;
        }
    }

    /// <summary>
    /// Abstraction over the actual media engine: demuxing, decoding, clock/AV
    /// sync, and buffering all live behind this interface. The control never
    /// touches a container/codec API directly - it only calls this contract,
    /// which means the decoding backend (FFmpeg interop, Media Foundation,
    /// a scripted test double, ...) is a swappable implementation detail.
    /// </summary>
    public interface IVideoSource : IDisposable
    {
        PlaybackState State { get; }
        TimeSpan Position { get; }
        TimeSpan Duration { get; }
        bool IsSeekable { get; }

        double PlaybackRate { get; set; }
        double Volume { get; set; }

        void Open(string source);
        void Play();
        void Pause();
        void Stop();
        void Seek(TimeSpan position);
        void Close();

        SKBitmap Frame { get; }

        event EventHandler<EventArgs> MediaOpened;
        event EventHandler<EventArgs> MediaEnded;
        event EventHandler<VideoSourceErrorEventArgs> MediaFailed;
        event EventHandler<EventArgs> PositionChanged;
    }
}
