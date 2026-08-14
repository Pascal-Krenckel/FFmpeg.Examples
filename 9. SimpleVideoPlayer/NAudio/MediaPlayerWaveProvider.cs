using FFmpeg.Audio;
using FFmpeg.MediaPlayer;
using NAudio.Wave;

namespace _9._SimpleVideoPlayer.NAudio;

public class MediaPlayerWaveProvider : IWaveProvider
{
    readonly FFmpeg.MediaPlayer.PlaybackEngine player;
    
    public MediaPlayerWaveProvider(FFmpeg.MediaPlayer.PlaybackEngine player)
    {
        this.player = player;
        WaveFormat = WaveFormat.Create(player.SampleFormat,player.SampleRate,player.Channels);

    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<byte> buffer)
    {
        // NAudio IWaveProvider does not support async
        player.WaitForAudio().Wait();
        int samples = player.ReadAudio(buffer, out _);
        FFmpeg.Logging.Logger.WriteLine(FFmpeg.Logging.LogLevel.Debug, $"[MediaPlayerWaveProvider.Read] Read {samples:#,0} samples");
        return samples * player.Channels * player.SampleFormat.GetBytesPerSample();
    }

}
