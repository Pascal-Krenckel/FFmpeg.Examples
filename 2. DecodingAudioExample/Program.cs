using FFmpeg;
using FFmpeg.Utils;
using FFmpeg.Audio;
using FFmpeg.Formats;
using FFmpeg.Codecs;
using System.Transactions;
using DecodingAudioExample;


string file = args[0]; // The file to the audio
string outputFile = args[1]; // The file that will contain a random sourceFrame.

// This will load the dll. You can specify ';' seperated directories.
// The order is: specified dirs, BaseDirectory [/ffmpeg [/{os}-{arch}]], {PATH}
// This will be automatically called the first time you use any ffmpeg-functions. So call it yourself first if you want to specify directories.
FFmpegLoader.Initialize();


// Since the parameters are usually stored in the container we do not need to specify InputFormat and FormatOptions.
// You can use a HWDevice for hardware-decoding, test out which work for you as not all hwdevices are supported on all platforms.
// see https://trac.ffmpeg.org/wiki/HWAccelIntro
using MediaSource audio = MediaSource.Open(file);
// set <MediaSource>.GetCodec before anything else if you want to use a specific codec, otherwise ffmpeg will choose one, which is usually the best option.
// MediaSource will automatically set up all CodecContext so you don't have to care about these.

// get best audio stream, it returns the streamId, which should be its index.
int audioStream = audio.FindBestStream(MediaType.Audio);
if (audioStream < 0)
{
    Console.Error.WriteLine("No videostream detected");
    return audioStream;
}

// Discard all data from all streams and then enable only the audio stream. We do not care about any other stream in this example.
foreach (AVStream stream in audio.Streams)
    stream.Discard = DiscardFlags.All;
audio.Streams[audioStream].Discard = DiscardFlags.Default;
var codecParams = audio.Streams[audioStream].CodecParameters; 

// get output settings based on stream data
int sampleRate = Math.Min(44100, codecParams.SampleRate);
int channels = Math.Min(2, codecParams.Channels);
SampleFormat format = codecParams.SampleFormat.AsPacked() == SampleFormat.UInt8 ? SampleFormat.UInt8 : SampleFormat.Int16; 
// RIFF/WAVE PCM is always packed, but the AudioFifo doesn't care luckily. It will convert into the packed/planar format automatically.

bool needConversion = sampleRate != codecParams.SampleRate || channels != codecParams.Channels
     || format != codecParams.SampleFormat.AsPacked();

// As we use PCM (uncompressed) audio we keep the sampleRate as 44.1k HZ, the channels as 2 and the format as 8 or 16 bit integer
// IEEE float is also support wave RIFF/WAVE with the format value of 3 (IEEE float) but we will only use PCM (value 1).


using AVFrame sourceFrame = AVFrame.Allocate(); // Allocate frame we will decode into
using AVFrame convertedFrame = AVFrame.Allocate(); // Allocate frame, we will convert the data if it is not Int16 or Int8, mono or stereo
using SwrContext converter = new();
using AudioFifo pcmBuffer = new(format,channels);

// !!! The sample count in AudioFifo and AVFrame are always samples per channel and not total samples !!!

while(true )
{
    var result = audio.ReadAndDecodeAVFrame(sourceFrame);
    if(result == AVResult32.EndOfFile) break;
    result.ThrowIfError();

    if (needConversion)
    {
        convertedFrame.Unreference(); // reset the frame, so that the buffer gets freed
        convertedFrame.SampleFormat = format;
        convertedFrame.ChannelLayout.SetReferencedObject(ChannelLayout.CreateDefault(channels)); // defaul channel layout has no allocated memory
        convertedFrame.SampleRate = sampleRate;

        // Convert returns the number of samples written into convertedFrame. Since frame did not have a buffer Convert should created a buffer for all samples.
       
        converter.Convert(sourceFrame, convertedFrame).ThrowIfError();
        pcmBuffer.Write(convertedFrame).ThrowIfError();
    }
    else
        pcmBuffer.Write(sourceFrame).ThrowIfError();
}

if(needConversion && converter.GetOutputSampleCount() > 0) // there might be a few samples that weren't written into the converted buffer, because the sample rate didn't match.
{
    convertedFrame.Unreference(); // reset the frame, so that the buffer gets freed
    convertedFrame.SampleFormat = format;
    convertedFrame.ChannelLayout.SetReferencedObject(ChannelLayout.CreateDefault(channels)); // defaul channel layout has no allocated memory
    convertedFrame.SampleRate = sampleRate;
    
    // pass null as source to drain converter.
    converter.Convert((AVFrame?)null, convertedFrame).ThrowIfError();
    pcmBuffer.Write(convertedFrame).ThrowIfError();
}

Console.WriteLine("Total amount of samples: {0:#,0}", pcmBuffer.Count);

WaveFileWriter.WriteFile(outputFile,pcmBuffer,sampleRate);

Console.WriteLine("Finished, listen to the wave file to check if it worked.");
return 0;
