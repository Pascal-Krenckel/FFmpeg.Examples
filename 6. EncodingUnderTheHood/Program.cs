using FFmpeg;
using FFmpeg.Formats;
using FFmpeg.Codecs;
using SkiaSharp;
using FFmpeg.Utils;
using FFmpeg.Skia;

using MuxerContext muxer = MuxerContext.Open("test.mp4")!;

Codec? svtav1 = Codec.FindEncoder("libsvtav1");
using CodecContext encoder = CodecContext.Allocate(svtav1);

encoder.PixelFormat = svtav1!.Value.GetBestPixelFormat(FFmpeg.Images.PixelFormat.RGB24);
encoder.Width = 1920;
encoder.Height = 1080;
encoder.TimeBase = new(1, 60);
encoder.SetOption("crf", 30);
encoder.SetOption("preset", 9);
encoder.SetOption("svtav1-params", "keyint=10s:tune=0");
encoder.Open(null).ThrowIfError();

// Create a stream from the encoder.
// The stream's codec parameters, time base, codec type, and other
// properties are initialized from the supplied CodecContext.
_ = muxer.AddStream(encoder);

// After all streams have been added, write the container header.
// This writes the metadata required by the output format.
// Container-specific options can be supplied here if needed.
muxer.WriteHeader().ThrowIfError();

// Writing the header may update stream properties such as the time base.
// Cache the packet time base for convenient timestamp rescaling later.
encoder.PacketTimeBase = muxer.Streams[0].TimeBase;

// now that the header is written lets write the content like in the previous example.
int counter = 0;
AVResult32 result;
int totalFrames = 60 * 60;
using AVPacket packet = AVPacket.Allocate();
using AVFrame frame = AVFrame.Allocate();

// Initialize the frame so bitmap data can be copied into it.
frame.PixelFormat = encoder.PixelFormat;
frame.Width = encoder.Width;
frame.Height = encoder.Height;
frame.CreateBuffer();

foreach (var bitmap in CreateClock(encoder.Width, encoder.Height, 500, TimeSpan.FromMinutes(1), 60))
{
    // Ensure the frame owns a writable buffer.
    // If the buffer is shared, a new one is allocated automatically.
    frame.MakeWriteable().ThrowIfError();
    bitmap.CopyTo(frame);
    bitmap.Dispose();
    // Set the frame timestamps
    frame.TimeBase = new(1, 60);
    frame.PresentationTimestamp = counter++;

    encoder.SendFrame(frame).ThrowIfError();
    // Read every packet currently produced by the encoder.
    while (!(result = encoder.ReceivePacket(packet)).IsError)
    {
        // Associate the packet with the output stream.
        packet.StreamIndex = 0;
        // Rescale packet timestamps from the encoder's time base to the
        // stream's time base expected by the muxer. This is an example, MuxerContext.Write... does this automatically.
        packet.RescaleTS(encoder.PacketTimeBase);

        // Write the packet.
        // The muxer interleaves packets as required by the container format.
        muxer.WritePacketInterleaved(packet).ThrowIfError();
    }
    if (!result.IsTryAgain)
        result.ThrowIfError(); // IsTryAgain is not considered an error and wouldn't throw

    Console.CursorLeft = 0;
    Console.Write($"{(double)counter / totalFrames:P0} finished");
}


Console.WriteLine("Drain encoder");

// Flush the encoder by sending a null frame.
// This causes the encoder to emit any delayed packets.
// DrainEncoder might return TryAgain if the internal buffer is full
// Since we always empty the buffer after sending a packet that can't happen
// !!! TryAgain is not considered an error !!!
encoder.DrainEncoder().ThrowIfError();

while (!(result = encoder.ReceivePacket(packet)).IsError)
    muxer.WritePacketInterleaved(packet).ThrowIfError();

if (result != AVResult32.EndOfFile)
    result.ThrowIfError();


// Finish the file by writing the trailer.
// This flushes the muxer and writes any container-specific end-of-file data.
muxer.WriteTrailer();
Console.WriteLine("Finished");

static IEnumerable<SKBitmap> CreateClock(int width, int height, float radius, TimeSpan duration, double fps = 60)
{
    double frames = fps * duration.TotalSeconds;
    using SKPaint paint = new() { Color = SKColors.Green, Style = SKPaintStyle.Fill, IsAntialias = true };

    SKPoint mid = new(width / 2f, height / 2f);
    SKRect rect = SKRect.Create(mid - new SKPoint(radius, radius), new(2 * radius, 2 * radius));


    for (int i = 0; i < frames; i++)
    {

        double angle = i / frames * 360; // draw arc is in degrees

        SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawArc(rect, -90, (float)angle, true, paint);
        yield return bitmap;
    }
}
