using FFmpeg.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace DecodingAudioExample;


public class WaveFileWriter
{
    /*
    [Master RIFF chunk]
        FileTypeBlocID  (4 bytes) : Identifier « RIFF »  (0x52, 0x49, 0x46, 0x46)
        FileSize        (4 bytes) : Overall file size minus 8 bytes
        FileFormatID    (4 bytes) : Format = « WAVE »  (0x57, 0x41, 0x56, 0x45)
 
    [Chunk describing the data format]
        FormatBlocID    (4 bytes) : Identifier « fmt␣ »  (0x66, 0x6D, 0x74, 0x20)
        BlocSize        (4 bytes) : Chunk size minus 8 bytes, which is 16 bytes here  (0x10)
        AudioFormat     (2 bytes) : Audio format (1: PCM integer, 3: IEEE 754 float)
        NbrChannels     (2 bytes) : Number of channels
        Frequency       (4 bytes) : Sample rate (in hertz)
        BytePerSec      (4 bytes) : Number of bytes to read per second (Frequency * BytePerBloc).
        BytePerBloc     (2 bytes) : Number of bytes per block (NbrChannels * BitsPerSample / 8).
        BitsPerSample   (2 bytes) : Number of bits per sample
 
    [Chunk containing the sampled data]
        DataBlocID      (4 bytes) : Identifier « data »  (0x64, 0x61, 0x74, 0x61)
        DataSize        (4 bytes) : SampledData size
        SampledData
     */

    public static void WriteFile(string file, AudioFifo pcmData, int sampleRate)
    {
        int pcmDataSize = pcmData.Format.GetBytesPerSample() * pcmData.Count * pcmData.Channels;
        int fileSize = 4 + 8 + 16 + 8 + pcmDataSize;
        int fmtBlockSize = 16;
        ushort bytePerBloc = (ushort)(pcmData.Channels * pcmData.Format.GetBitsPerSample() / 8);
        int bytePerSec = sampleRate * bytePerBloc;
        ushort bitsPerSample = (ushort)pcmData.Format.GetBitsPerSample();

        using BinaryWriter writer = new(File.OpenWrite(file),Encoding.UTF8,false);
        
        writer.Write(['R', 'I', 'F', 'F']);
        writer.Write(fileSize);
        writer.Write(['W','A', 'V', 'E']);

        writer.Write(['f', 'm', 't', ' ']);
        writer.Write(fmtBlockSize);
        writer.Write((ushort)1); // audio format pcm, use 3 if you want to store float values
        writer.Write((ushort)pcmData.Channels);
        writer.Write(sampleRate);
        writer.Write(bytePerSec);
        writer.Write(bytePerBloc);
        writer.Write(bitsPerSample);

        writer.Write(['d', 'a', 't', 'a']);
        writer.Write(pcmDataSize);

        byte[] readData = new byte[pcmData.Format.GetBytesPerSample() * pcmData.Channels * 8096]; // read 8k samples per call
        while(pcmData.Count > 0)
        {
            var samplesPerChannel = pcmData.Read(readData);
            samplesPerChannel.ThrowIfError();
            if (samplesPerChannel == 0)
                throw new Exception("No samples read, maybe the buffer is to small."); // should never happen since the buffer has space for 8k samples
            writer.Write(readData.AsSpan(0, samplesPerChannel*pcmData.Channels * pcmData.Format.GetBytesPerSample()));
        }
    }
}


public struct WaveInfo
{
    public short Channels { get; set; }
    public int Frequency { get; set; }
    public int BytesPerSec { get; set; }
    public int BitsPerSample { get; set; }
}