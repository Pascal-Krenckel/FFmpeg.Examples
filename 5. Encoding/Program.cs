using FFmpeg;
using FFmpeg.Codecs;
using FFmpeg.Formats;
using FFmpeg.Skia;
using FFmpeg.Utils;
using SkiaSharp;
using System.Security.Cryptography;

Codec codec = Codec.FindEncoder("libsvtav1")!.Value;
// Get the best pixel format based on the skia pix fmt. Not all SKColorTypes have a pixel format though.
var pixFmt = codec.GetBestPixelFormat(SKColorType.Rgba8888.ToPixelFormat());

/**
 * -crf 30, -preset 6, 
-svtav1-params keyint=10s:tune=0

FPS: 60
Video-Length: 1min
 */

// Allocate the codec context. We do not open the encoder yet.
using CodecContext encoderContext = CodecContext.Allocate(codec);
encoderContext.SetOption("crf", 30);
encoderContext.SetOption("preset", 9);
encoderContext.SetOption("svtav1-params", "keyint=10s:tune=0");
encoderContext.Width = 1920;
encoderContext.Height = 1080;
encoderContext.PixelFormat = pixFmt;
encoderContext.TimeBase = new(1, 60); // 1/60s => 60fps
encoderContext.Open(null).ThrowIfError(); // we open the context ourself, it's easier to debug if there was an error
Console.WriteLine();
// media sink handles most of the stuff, we just need to add a stream, write the header (which will open the streams), send the frames and close the file
using MediaSink mp4File = MediaSink.Create("test.mp4")!;
_ = mp4File.AddStream(encoderContext);
int counter = 0;
int totalFrames = 60 * 60;

// WriteHeader, CodecContext opening, WriteTrailer will be called automatically if we don't to it, so everything is fine hear.
foreach(var bitmap in CreateClock(encoderContext.Width,encoderContext.Height, 500, TimeSpan.FromMinutes(1), 60))
{
    using AVFrame frame = bitmap.ToAVFrame(pixFmt);
    frame.TimeBase = new(1, 60);
    frame.PresentationTimestamp = counter++;
    mp4File.WriteFrame(frame, 0).ThrowIfError();
    bitmap.Dispose();
    int left = Console.CursorLeft;
    Console.CursorLeft = 0;
    Console.Write($"{(double)counter / totalFrames:P0} finished");
}

Console.WriteLine("Finializing the file");
// Drain encoder, write trailer and dispose of the object
mp4File.Close();



// We could reuse the SKBitmap for every frame, but in this small example we don't care.
static IEnumerable<SKBitmap> CreateClock(int width, int height,float radius, TimeSpan duration, double fps = 60)
{
    double frames = fps * duration.TotalSeconds;
    using SKPaint paint = new() {  Color = SKColors.Green, Style = SKPaintStyle.Fill, IsAntialias = true };

    SKPoint mid = new(width / 2f, height / 2f);
    SKRect rect = SKRect.Create(mid - new SKPoint(radius,radius), new(2*radius, 2*radius));


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