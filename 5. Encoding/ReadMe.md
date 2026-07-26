# Encoding Video with MediaSink

This example demonstrates how to create a video using **FFmpegDotNet's** high-level `MediaSink` API.

A simple animation is generated with **SkiaSharp**, converted into `AVFrame` objects, encoded using the **SVT-AV1** encoder (`libsvtav1`), and written to an MP4 file.

Unlike FFmpeg's native API, `MediaSink` automatically manages packet handling, timestamp rescaling, muxing, encoder flushing, and trailer writing, allowing applications to focus on producing frames.

---

# Steps

1. Find an encoder.
2. Configure a `CodecContext`.
3. Create a `MediaSink`.
4. Add a video stream.
5. Generate frames.
6. Convert each frame to an `AVFrame`.
7. Assign presentation timestamps.
8. Write frames to the media file.
9. Finalize the file.

---

# Creating the Encoder

The example uses the **SVT-AV1** encoder.

```csharp
Codec codec = Codec.FindEncoder("libsvtav1")!.Value;
```

A `CodecContext` is then configured with the desired encoding parameters.

```csharp
using CodecContext encoderContext = CodecContext.Allocate(codec);

encoderContext.Width = 1920;
encoderContext.Height = 1080;
encoderContext.PixelFormat = pixFmt;
encoderContext.TimeBase = new(1, 60);

encoderContext.Open(null).ThrowIfError();
```

The encoder is opened explicitly before creating the output file. While `MediaSink` can open encoders automatically, doing it manually makes configuration errors easier to diagnose.

---

# Creating the Output File

```csharp
using MediaSink mp4File = MediaSink.Create("test.mp4")!;
```

`MediaSink` creates the output container and automatically selects an appropriate muxer based on the file extension.

The configured encoder is then added as a video stream.

```csharp
mp4File.AddStream(encoderContext);
```

Multiple streams can be added to the same `MediaSink`, making it possible to create files containing audio, video, subtitles, or attachments.

---

# Generating Frames

The example generates a one-minute animation of a clock using **SkiaSharp**.

```csharp
foreach (var bitmap in CreateClock(...))
{
    ...
}
```

Each iteration produces an `SKBitmap` containing one animation frame.

---

# Converting to AVFrame

The generated bitmap is converted into an `AVFrame`.

```csharp
using AVFrame frame = bitmap.ToAVFrame(pixFmt);
```

The helper methods provided by **FFmpeg.Skia** handle pixel format conversion between SkiaSharp and FFmpeg.

The example also chooses the most suitable FFmpeg pixel format supported by the encoder.

```csharp
var pixFmt = codec.GetBestPixelFormat(
    SKColorType.Rgba8888.ToPixelFormat());
```

---

# Presentation Timestamps

Every encoded frame requires a presentation timestamp (PTS).

```csharp
frame.TimeBase = new(1, 60);
frame.PresentationTimestamp = counter++;
```

Since the encoder uses a time base of **1/60**, incrementing the timestamp by one for each frame produces a constant frame rate of **60 FPS**.

---

# Writing Frames

Frames are written directly to the media sink.

```csharp
mp4File.WriteFrame(frame, 0).ThrowIfError();
```

Unlike FFmpeg's low-level API, no manual packet handling is required.

Internally, `MediaSink`:

- sends frames to the encoder
- receives encoded packets
- rescales timestamps
- writes packets to the output container

This greatly simplifies the encoding workflow.

---

# Finalizing the File

After all frames have been written, close the media sink.

```csharp
mp4File.Close();
```

Closing the sink automatically:

- drains the encoder
- writes delayed packets
- writes the container trailer
- closes all streams

Without this step, the output file may be incomplete or corrupted.

---

# Summary

This example demonstrates a complete video encoding workflow using FFmpegDotNet's high-level API.

It shows how to:

- Configure a video encoder.
- Create an output media file.
- Add a video stream.
- Generate animation frames using SkiaSharp.
- Convert `SKBitmap` objects into `AVFrame`s.
- Assign presentation timestamps.
- Encode and mux frames with `MediaSink`.
- Finalize the output file.

The `MediaSink` API hides much of FFmpeg's packet management while still exposing full control over encoder configuration, making it suitable for applications that need to generate videos programmatically.