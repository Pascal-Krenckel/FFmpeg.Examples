using FFmpeg;
using FFmpeg.Utils;
using FFmpeg.Formats;
using FFmpeg.Codecs;

string inputFile = args[0];

using DemuxerContext mediaFile = DemuxerContext.Open(inputFile, findStreamInfo: true);
// Opens a DemuxerContext and tries to find the stream info, this might read a few frames based on the container type.

int videoStreamIndex = mediaFile.FindBestStream(MediaType.Video);
int audioStreamIndex = mediaFile.FindBestStream(MediaType.Audio);

CodecContext? videoCodec = null;
CodecContext? audioCodec = null;

if (videoStreamIndex >= 0)
{
    var videoStream = mediaFile.Streams[videoStreamIndex];
    var codecParams = videoStream.CodecParameters;
    CodecID codecId = videoStream.CodecId;
    double frameRate = mediaFile.GuessFrameRate(videoStreamIndex);
    Console.WriteLine($"Stream[{videoStreamIndex}]: {codecId}, {codecParams.Width}x{codecParams.Height} @ {frameRate:N0} fps");

    // the AVStream contains the CodecId and the CodecParamets
    // Since there are multiple Decoders for the same CodecId like for av1: libdav1d libaom-av1 av1 av1_cuvid av1_qsv av1_amf
    // The CodecId decribes the codec of the stream, the Codec describes the specific encoder/decoder
    // Codec.FindCodec can either take the name of the encoder or the codec id and ffmpeg will look for the best codec
    Codec vCodec = Codec.FindDecoder(codecId)!.Value;

    // We can allocate the CodecContext and open the CodecContext later or open it right now. We need to set all parameters befor we open the codec.
    // In our case we just pass the videoStream codec parameters
    // you might want to pass a hwDevice for hwAccel decoding
    videoCodec = CodecContext.Open(vCodec, codecParams);
    videoCodec.PacketTimeBase = videoStream.TimeBase;
    videoCodec.TimeBase = videoStream.TimeBase; // <- for encoding this must to be set, for decoding we will use this to set the frame parameters accordingly
                                                // Then we could calculate the actual pts in sec when handling the frame


}

if (audioStreamIndex >= 0)
{
    var audioStream = mediaFile.Streams[audioStreamIndex];
    var codecParams = audioStream.CodecParameters;
    CodecID codecId = audioStream.CodecId;
    Console.WriteLine($"Stream[{audioStreamIndex}]: {codecId}, {codecParams.SampleFormat} x {codecParams.ChannelLayout} ({codecParams.Channels}) @ {codecParams.BitRate / 1024:N0} kbit/s");

    // the AVStream contains the CodecId and the CodecParamets
    // Since there are multiple Decoders for the same CodecId like for av1: libdav1d libaom-av1 av1 av1_cuvid av1_qsv av1_amf
    // The CodecId decribes the codec of the stream, the Codec describes the specific encoder/decoder
    // Codec.FindCodec can either take the name of the encoder or the codec id and ffmpeg will look for the best codec
    Codec aCodec = Codec.FindDecoder(codecId)!.Value;

    // We can allocate the CodecContext and open the CodecContext later or open it right now. We need to set all parameters befor we open the codec.
    // In our case we just pass the videoStream codec parameters
    // you might want to pass a hwDevice for hwAccel decoding
    audioCodec = CodecContext.Open(aCodec, codecParams);
    audioCodec.PacketTimeBase = audioStream.TimeBase;
    audioCodec.TimeBase = audioStream.TimeBase; // <- for encoding this must to be set, for decoding we will use this to set the frame parameters accordingly
                                                // Then we could calculate the actual pts in sec when handling the frame

}


using AVPacket packet = AVPacket.Allocate();
using AVFrame frame = AVFrame.Allocate();
AVResult32 result;

// Decoding Loop
while (!(result = mediaFile.ReadPacket(packet)).IsError)
{
    bool isVideoStream = packet.StreamIndex == videoStreamIndex;
    bool isAudioStream = packet.StreamIndex == audioStreamIndex;
    if (!isVideoStream && !isAudioStream)
        continue;
    CodecContext codecCtx = isVideoStream ? videoCodec! : audioCodec!;

    while ((result = codecCtx.SendPacket(packet)) == AVResult32.TryAgain)
    {
        codecCtx.ReceiveFrame(frame).ThrowIfError(); // should not return an error code
        frame.TimeBase = codecCtx.TimeBase; // FFmpegs description:
                                            // In the future, this field may be set on frames output by decoders or filters, but its value will be by default ignored on input to encoders or filters. 
                                            // So we will set this ourselfs
        if (packet.StreamIndex == videoStreamIndex)
            HandleVideoFrame(frame);
        else
            HandleAudioFrame(frame);
    }
    result.ThrowIfError();
}

if (result != AVResult32.EndOfFile)
    result.ThrowIfError();

// Drain Decoders
if(videoCodec != null)
{
    Console.WriteLine("Draining mode initiated");
    videoCodec.DrainDecoder().ThrowIfError();
    while (!(result = videoCodec.ReceiveFrame(frame)).IsError)
        HandleVideoFrame(frame);
    if (result != AVResult32.EndOfFile)
        result.ThrowIfError();
}

if (audioCodec != null)
{
    Console.WriteLine("Draining mode initiated");
    audioCodec.DrainDecoder().ThrowIfError();
    while (!(result = audioCodec.ReceiveFrame(frame)).IsError)
        HandleAudioFrame(frame);
    if (result != AVResult32.EndOfFile)
        result.ThrowIfError();
}

audioCodec?.Dispose();
videoCodec?.Dispose();


static void HandleVideoFrame(AVFrame frame)
{
    Console.WriteLine("We received a video frame with pts: {0} ", (TimeSpan)(frame.GetPresentationTimestamp() * frame.TimeBase));
}

static void HandleAudioFrame(AVFrame frame)
{
    Console.WriteLine("We received an audio frame with pts: {0} ", (TimeSpan)(frame.GetPresentationTimestamp() * frame.TimeBase));
}