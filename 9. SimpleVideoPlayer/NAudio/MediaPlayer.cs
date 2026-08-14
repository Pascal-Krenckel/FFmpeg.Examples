using FFmpeg.Audio;
using FFmpeg.MediaPlayer;
using FFmpeg.Skia;
using FFmpeg.Utils;
using NAudio.Wave;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Threading;
using VideoPlayer.Controls;

namespace _9._SimpleVideoPlayer.NAudio;

public sealed class MediaPlayer : IDisposable, IVideoSource
{
    FFmpeg.MediaPlayer.PlaybackEngine? engine;
    NAudioPlayer<WasapiPlayer>? audioPlayer;
    public Dispatcher Dispatcher { get; set; } = Dispatcher.CurrentDispatcher;
    public global::VideoPlayer.Controls.PlaybackState State { get; private set; } = global::VideoPlayer.Controls.PlaybackState.Closed;

    public TimeSpan Position => engine?.Clock?.Position ?? TimeSpan.Zero;

    public TimeSpan Duration => engine?.Duration ?? TimeSpan.Zero;

    public bool IsSeekable => engine?.CanSeek ?? false;

    public double PlaybackRate { get => engine?.Clock?.Rate ?? 0; set => throw new InvalidOperationException(); } // NAudio does not support this, so we would have to use filters
    public double Volume
    {
        get => audioPlayer?.AudioPlayer?.Volume ?? 0; set => audioPlayer?.AudioPlayer?.Volume = (float)value;
    }

    public SKBitmap Frame { get; } = new();

    public event EventHandler<EventArgs>? MediaOpened;
    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<VideoSourceErrorEventArgs>? MediaFailed;
    public event EventHandler<EventArgs>? PositionChanged;

    public void Close()
    {
        audioPlayer?.Dispose();
        engine?.Dispose();
        audioPlayer = null;
        engine = null;
        State = global::VideoPlayer.Controls.PlaybackState.Closed;
    }
    public void Dispose() => Close();
    public void Open(string source)
    {
        Close();
        engine = PlaybackEngine.Open(source);
        if (engine.AudioStream != null)
        {
            var wasapiPlayer = new WasapiPlayerBuilder().Build();
            audioPlayer = new(wasapiPlayer,engine);
            var waveFormat = WaveFormat.Create(engine.SampleFormat, engine.SampleRate, 2); // wasapi does not supprt 6 channels and IsFormatSupported doesn't work
            if (!wasapiPlayer.IsFormatSupported(waveFormat, out var closestMatch))
                if (closestMatch != null)
                    waveFormat = closestMatch;
                else
                    waveFormat = wasapiPlayer.GetSupportedExclusiveFormat(waveFormat);
            string aformat = $"aformat=sample_fmts={waveFormat.SampleFormat.ToFFmpegString()}:channel_layouts={waveFormat.ChannelLayout}";
            string aresample = $"aresample={waveFormat.SampleRate}";
            string filter = string.Empty;
            if (waveFormat.SampleFormat != engine.SampleFormat || waveFormat.ChannelLayout != engine.ChannelLayout)
                filter = aformat;
            if(waveFormat.SampleRate != engine.SampleRate)
                filter += string.IsNullOrEmpty(filter) ? "" : "," + aresample;
            engine.SetAudioFilter(filter);
            engine.Clock = audioPlayer;
            MediaPlayerWaveProvider waveProvider = new(engine);
            wasapiPlayer.Init(waveProvider);

        }
        engine.PlayerStateChanged += Engine_PlayerStateChanged;
        engine.Faulted += Engine_Faulted;
        engine.Finished += Engine_Finished;
        engine.Clock.ClockChanged += Clock_ClockChanged;
        engine.MaxBufferDuration = TimeSpan.FromMinutes(1);
        engine.EnableVideoEvents(true);
        engine.VideoFrameReady += Engine_VideoFrameReady;
        Invoke(MediaOpened, EventArgs.Empty);
    }


    private void Invoke<TEventArgs>(EventHandler<TEventArgs>? eventHandler,TEventArgs e)
    {
        if (eventHandler == null)
            return;
        if (Dispatcher != null)
            Dispatcher.Invoke(() => eventHandler?.Invoke(this, e));
        else
            eventHandler?.Invoke(this, e);
    }
    private void Engine_VideoFrameReady(object? sender, EventArgs e)
    {
        using var frame = engine?.ReadVideo();
        frame?.CopyTo(Frame);
    }
    private void Clock_ClockChanged(object? sender, EventArgs e) => Invoke(PositionChanged, e);

    private void Engine_Finished(object? sender, EventArgs e)
    {
        State = global::VideoPlayer.Controls.PlaybackState.Ended;
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void Engine_Faulted(object? sender, Exception e) => Invoke(MediaFailed, new(e, e.Message));
    private void Engine_PlayerStateChanged(object? sender, PlayerState e) => SetPlayerState(engine!.State);
    private void SetPlayerState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Playing:
                this.State = global::VideoPlayer.Controls.PlaybackState.Playing;              
                break;
            case PlayerState.Paused:
                State = global::VideoPlayer.Controls.PlaybackState.Paused; 
                break;
               case PlayerState.Stopped:
                if (engine!.Clock.Position == TimeSpan.Zero)
                    State = global::VideoPlayer.Controls.PlaybackState.Stopped;                
                break;
            case PlayerState.Faulted:
                State = global::VideoPlayer.Controls.PlaybackState.Failed;
                break;
        }
    }

    public void Pause()
    {
        engine?.Pause();
    }
    public void Play()
    {
        engine?.Play();
    }
    public void Seek(TimeSpan position)
    {
        engine?.Seek(position);
    }
    public void Stop()
    {
        engine?.Stop();
    }
}
