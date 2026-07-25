# Decoding a Random Video Frame

This example demonstrates how to use **FFmpegDotNet** to open a video, seek to a random position, decode a single video frame, convert it to an RGB image, and save it as a bitmap.

The example uses the high-level `MediaSource` API, which automatically configures the required demuxer and decoder, making video decoding straightforward.

> **Note**
> Decoded video frames are typically stored in YUV pixel formats. Since `System.Drawing.Bitmap` expects RGB data, the frame is converted to `BGR24` before it is saved.

If you prefer using **SkiaSharp**, see the **FFmpegDotNet.Skia** package, which provides convenient conversion from `AVFrame` to `SKBitmap`.

## Requirements

Install the following packages:

1. **FFmpegDotNet**
2. **FFmpegDotNet.bin.winx64** (or another FFmpeg binary package appropriate for your platform)
3. **System.Drawing.Common** (or `System.Drawing` depending on your target framework)

## What the Example Does

1. Initializes the FFmpeg loader.
2. Opens the media file with `MediaSource`.
3. Finds the best video stream.
4. Disables all other streams to improve performance.
5. Calculates a random timestamp within the video.
6. Seeks to that timestamp.
7. Decodes the next video frame.
8. Converts the frame from its native pixel format (typically YUV) to `BGR24`.
9. Creates a `Bitmap` that references the converted frame's buffer.
10. Saves the bitmap to the specified output file.

## Running the Example

```text
Example.exe <input-video> <output-image>
```

Example:

```text
Example.exe sample.mp4 randomFrame.bmp
```

## Important Notes

### Automatic Decoder Setup

`MediaSource` automatically:

* Detects the input format.
* Selects the appropriate decoder.
* Creates and configures the required codec contexts.

If you need a specific decoder (for example, a hardware decoder), assign `MediaSource.GetCodec` before opening the codec.

### Stream Selection

Although a media file may contain audio, subtitles, and other streams, this example only decodes video. All other streams are discarded to avoid unnecessary processing.

### Seeking

The example uses `SeekExactly()` to seek to a randomly selected timestamp. After seeking, the first decoded frame is read and written to disk.

### Pixel Format Conversion

Most video formats are decoded into YUV pixel formats, which cannot be used directly by `System.Drawing.Bitmap`.

`SwsContext.Convert()` converts the decoded frame into `BGR24`, matching the memory layout expected by `PixelFormat.Format24bppRgb`.

### Frame Lifetime

The `Bitmap` created in this example directly references the pixel buffer owned by the destination `AVFrame`. No pixel data is copied.

Because of this, **both the `AVFrame` and the `AVBuffer`s it references must remain alive** while the bitmap is in use. Disposing the frame (or otherwise releasing its buffers) will invalidate the bitmap's backing memory.

In this example, the bitmap is saved before `destFrame` is disposed, so the buffer remains valid for the lifetime of the bitmap.
