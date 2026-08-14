using FFmpeg.Audio;
using FFmpeg.AutoGen;
using FFmpeg.Unsafe;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace _9._SimpleVideoPlayer.NAudio;

public static class WaveFormatExtensions
{
    public static SampleFormat GetSampleFormat(WaveFormat waveFormat)
    {
        ArgumentNullException.ThrowIfNull(waveFormat, nameof(waveFormat));
        if (waveFormat.Encoding == WaveFormatEncoding.Pcm)
        {
            switch (waveFormat.BitsPerSample)
            {
                case 8:
                    return SampleFormat.UInt8;
                case 16:
                    return SampleFormat.Int16;
                case 32:
                    return SampleFormat.Int32;
                case 64:
                    return SampleFormat.Int64;
                default:
                    throw new NotSupportedException();
            }
        }
        else if (waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            switch (waveFormat.BitsPerSample)
            {
                case 32:
                    return SampleFormat.Float32;
                case 64:
                    return SampleFormat.Float64;
                default:
                    throw new NotSupportedException();
            }
        }
        throw new NotSupportedException();
    }

    public static ChannelLayout GetChannelLayout(WaveFormat waveFormat)
    {
        if(waveFormat is WaveFormatExtensible ext && ext.ChannelMask != 0)
            return new ChannelLayout((ulong)ext.ChannelMask);
        return ChannelLayout.CreateDefault(waveFormat.Channels);
    }


    extension(WaveFormat waveFormat)
    {
        public SampleFormat SampleFormat => GetSampleFormat(waveFormat);

        public ChannelLayout ChannelLayout => GetChannelLayout(waveFormat);

        public static WaveFormat Create(SampleFormat format, int sampleRate, int channels)
        {
            switch (format)
            {
                case SampleFormat.Float32:
                case SampleFormat.Float32Planar:
                    return WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
                // don't know wether it works, didn't test it
                case SampleFormat.Float64:
                case SampleFormat.Float64Planar:
                    int avgBytesPerSecond = (int)((long)format.GetBitsPerSample() * sampleRate * channels / 8);
                    return WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, sampleRate, channels, avgBytesPerSecond, 1, format.GetBitsPerSample());
                // int:
                default:
                    return new(sampleRate, format.GetBitsPerSample(), channels);
            }
        }

        public static WaveFormat Create(SampleFormat format, int sampleRate, ChannelLayout channels)
        { // bits * channels???
            if (channels.Mask != 0 && channels.Mask < int.MaxValue)
                return new WaveFormatExtensible(sampleRate, format.GetBitsPerSample(), channels.Channels, 
                    format is SampleFormat.Float32 or SampleFormat.Float32Planar or SampleFormat.Float64 or SampleFormat.Float64Planar,
                    format.GetBitsPerSample(),
                    (int)channels.Mask);
            return Create(format,sampleRate, channels.Channels);
        }

    }
}
