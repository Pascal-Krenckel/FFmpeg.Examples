using FFmpeg.MediaPlayer;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Animation;

namespace _9._SimpleVideoPlayer.NAudio;

public sealed class NAudioPlayer<T> : IMediaClock, IDisposable where T : IWavePlayer,IWavePosition 
{
    public T AudioPlayer { get; }
    public PlaybackEngine MediaPlayer { get; }

    public NAudioPlayer(T audioPlayer, PlaybackEngine mediaPlayer)
    {
        this.AudioPlayer = audioPlayer;
        audioPlayer.PlaybackStopped += AudioPlayer_PlaybackStopped;
        MediaPlayer = mediaPlayer;
    }

    private void AudioPlayer_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        ptsOffset = MediaPlayer.Duration;
    }

    private TimeSpan ptsOffset = TimeSpan.Zero;

    public TimeSpan Position => AudioPlayer.GetPositionTimeSpan() + ptsOffset;

    public double Rate => 1;

    public bool IsRunning => AudioPlayer.PlaybackState == PlaybackState.Playing;

    public event EventHandler? ClockChanged;

    public void Seeked(TimeSpan timespan)
    {
        AudioPlayer.Stop();
        ptsOffset = timespan;
        ClockChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        AudioPlayer.Play();
        ClockChanged?.Invoke(this,EventArgs.Empty);
    }


    public void Pause() { AudioPlayer.Pause(); ClockChanged?.Invoke(this, EventArgs.Empty); }

    public void Dispose() => AudioPlayer.Dispose();
}
