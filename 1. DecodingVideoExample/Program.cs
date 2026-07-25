#pragma warning disable CA1416 // Plattformkompatibilität überprüfen

using FFmpeg;
using FFmpeg.Formats;
using FFmpeg.Images;
using FFmpeg.Utils;
using System.Drawing;

string file = args[0]; // The file to the video
string outputFile = args[1]; // The file that will contain a random frame.

// This will load the dll. You can specify ';' seperated directories.
// The order is: specified dirs, BaseDirectory [/ffmpeg [/{os}-{arch}]], {PATH}
// This will be automatically called the first time you use any ffmpeg-functions. So call it yourself first if you want to specify directories.
FFmpegLoader.Initialize();


// Since the parameters are usually stored in the container we do not need to specify InputFormat and FormatOptions.
// You can use a HWDevice for hardware-decoding, test out which work for you as not all hwdevices are supported on all platforms.
// see https://trac.ffmpeg.org/wiki/HWAccelIntro
using MediaSource video = MediaSource.Open(file);
// set <MediaSource>.GetCodec before anything else if you want to use a specific codec, otherwise ffmpeg will choose one, which is usually the best option.
// MediaSource will automatically set up all CodecContext so you don't have to care about these.

// get best video stream, it returns the streamId, which should be its index.
int videoStream = video.FindBestStream(MediaType.Video);
if (videoStream < 0)
{
    Console.Error.WriteLine("No videostream detected");
    return videoStream;
}

// Discard all data from all streams and then enable only the video stream. We do not care about any other stream in this example.
foreach (AVStream stream in video.Streams)
    stream.Discard = DiscardFlags.All;
video.Streams[videoStream].Discard = DiscardFlags.Default;

// Get the duration of stream and calculate a random seek time for our frame.
TimeSpan duration = video.Streams[videoStream].Duration * video.Streams[videoStream].TimeBase;
TimeSpan seekTime = new(Random.Shared.NextInt64(0, duration.Ticks));
Console.WriteLine("Seek Time: {0}", seekTime);

AVResult32 avResult = video.SeekExactly(seekTime, videoStream);
if (avResult.IsError) // MediaSource will handle to important ErrorCodes like AVResult.TryAgain
{
    Console.Error.WriteLine("Seeking resulted in an error: {0}", avResult);
    return avResult;
}

using AVFrame frame = AVFrame.Allocate(); // Allocate a frame, we do not need to set any properties, they will be set by the decoder.
avResult = video.ReadAndDecodeAVFrame(frame);
if (avResult.IsError) // same as before
{
    Console.Error.WriteLine("Reading and decoding run into an error: {0}", avResult);
    return avResult;
}

// frame should now contain the video frame. However the format will probably be yuv420, which is not compatible with System.Drawing.Bitmap.
// We need to convert it into an RGB image. Alternativly you can use the FFmpegDotNet.Skia package to easily convert an AVFrame into an SKBitmap.

using AVFrame destFrame = AVFrame.Allocate();
destFrame.PixelFormat = FFmpeg.Images.PixelFormat.BGR24; // !!! System.Drawing uses reveresed order, so this will be Format24bppRgb.  !!!
destFrame.Width = frame.Width; // we want the same size
destFrame.Height = frame.Height; // now we will not provide a buffer, this will be handled by our scaler.
                                 // our bmp will use the same buffer as the one create for AVFrame inside our scaler
                                 // Otherwise we have to make sure that the buffer is large enough. (CreateBuffer would create a buffer large enough)
                                 // AVFrame does not support user managed buffers (external buffers)

// For simplicity we will use SwsContext.Convert, which will create the context convert the frame and then dispose of the context
// If you want to reuse the context use new SwsContext(...) or SwsContext.Allocate.
// SwsContext.Allocate is a new method, that you should only use if you always convert AVFrames.
// It will get the properties from the AVFrames directly and not initialize the context.
// Otherwise the properties cannot be changed. Thats also why it doesn't work with the other overloads.
SwsContext.Convert(frame, destFrame, SwsAlgorithm.Bicubic());

// !!! Referencing a AVFrames buffer is dangerous as it might be freed automatically when using the AVFrame !!!
// !!! In out case however, the frame is not used anymore and not freed, so the buffer stays allocated !!!
// !!! System.Drawing uses reveresed order, so this will be BGR24.  !!!
using Bitmap bmp = new(destFrame.Width, destFrame.Height, destFrame.LineSize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, destFrame.Data[0]);
bmp.Save(outputFile);

Console.WriteLine("Finished, please check that the output is correct.");
Console.ReadLine();
return 0;
