# Encoding Video

In this example we will manually encode video using **FFmpegDotNet** instead of using a higher-level encoding pipeline. This gives full control over codec selection, encoder initialization, frame generation, packet handling, and container writing while closely following FFmpeg's native encoding API.

The example generates frames using **SkiaSharp**, converts them into `AVFrame` instances, encodes them using the selected video encoder, and writes the resulting packets into an MP4 container.

## Steps

1. Create a `MuxerContext`.
2. Find and initialize a video encoder.
3. Configure the encoder parameters.
4. Add an output stream using the encoder configuration.
5. Write the container header.
6. Generate video frames.
7. Send frames to the encoder.
8. Receive encoded packets.
9. Write packets to the muxer.
10. Drain the encoder after all frames have been submitted.
11. Write the container trailer.

> **Note**
>
> In FFmpegDotNet, FFmpeg's `FormatContext` is split into `DemuxerContext` and `MuxerContext` to provide a cleaner API for reading and writing media files.

---

## Creating the Muxer

```csharp
using MuxerContext muxer = MuxerContext.Open("test.mp4");
```

`MuxerContext.Open()` creates an output container based on the provided filename.

The container format is detected from the file extension. Additional format options can be supplied when required.

Unlike decoding, where streams already exist in the input file, output streams must be created manually.

---

## Creating the Encoder

A codec describes the compression algorithm, but it is not the encoder state itself.

```csharp
Codec? codec = Codec.FindEncoder("libsvtav1");
CodecContext encoder = CodecContext.Allocate(codec);
```

`FindEncoder()` selects an encoder implementation available on the current system.

For example, AV1 can be encoded by different implementations:

* `libsvtav1`
* `libaom-av1`
* `av1_nvenc`
* `av1_qsv`
* `av1_amf`

The `CodecContext` stores the runtime state required by the encoder.

---

## Configuring the Encoder

Before opening the encoder, the required parameters must be configured.

```csharp
encoder.Width = 1920;
encoder.Height = 1080;
encoder.PixelFormat = codec.GetBestPixelFormat(...);
encoder.TimeBase = new(1, 60);
```

The important video parameters are:

* width and height
* pixel format
* time base
* codec-specific options

Codec options can be supplied through `SetOption()`.

```csharp
encoder.SetOption("crf", 30);
encoder.SetOption("preset", 9);
```

Options are passed directly to the underlying FFmpeg encoder.

Once all parameters have been configured, the encoder can be opened.

```csharp
encoder.Open(null);
```

After the encoder has been opened, most configuration values should no longer be changed.

---

## Adding the Output Stream

The muxer needs a stream containing the codec parameters.

```csharp
muxer.AddStream(encoder);
```

`MuxerContext.AddStream()` creates an output stream and copies the required information from the encoder.

The stream receives information such as:

* codec parameters
* codec type
* time base
* stream index

After all streams have been added, the container header must be written.

```csharp
muxer.WriteHeader();
```

The header contains container-specific metadata required before packets can be written.

Writing the header may modify stream properties, such as the final stream time base.

---

## Time Bases

FFmpeg stores timestamps as integer values together with a time base.

```
seconds = timestamp × timeBase
```

The encoder receives timestamps in the encoder time base:

```csharp
frame.TimeBase = encoder.TimeBase;
frame.PresentationTimestamp = frameNumber;
```

The resulting packets must be converted to the stream time base before writing:

```csharp
packet.RescaleTS(encoder.PacketTimeBase);
```

The muxer expects timestamps in the stream's time base.

---

## Creating Frames

The encoder does not accept image objects directly. It requires `AVFrame` instances containing pixel data.

In this example, frames are generated using SkiaSharp:

```csharp
SKBitmap bitmap = CreateFrame();
bitmap.CopyTo(frame);
```

`SKBitmap.CopyTo(AVFrame)` performs any required pixel format conversion and scaling using FFmpeg's scaling functionality.

If the frame buffer is shared internally by FFmpeg, it must be made writable before modifying it:

```csharp
frame.MakeWriteable();
```

This creates a new buffer if required.

---

## FFmpeg's Encoding API

FFmpeg uses an asynchronous **send/receive** API for encoding.

Encoding does not directly convert one frame into one packet.

Instead:

1. Frames are sent to the encoder.
2. Encoded packets are retrieved separately.

The workflow is:

```
SendFrame(frame)
        ↓
ReceivePacket(packet)
        ↓
WritePacket(packet)
```

A single frame may produce:

* no packets
* one packet
* multiple packets

This happens because many codecs buffer frames internally for features such as B-frames.

---

## Receiving Packets

After sending a frame, packets should be read until the encoder has no more output available.

```csharp
encoder.SendFrame(frame);

while (!(result = encoder.ReceivePacket(packet)).IsError)
{
    muxer.WritePacketInterleaved(packet);
}
```

`ReceivePacket()` returns `TryAgain` when the encoder currently has no more packets available.

`TryAgain` is not an error and only means that more input is required.

---

## Writing Packets

Before writing a packet, it must be associated with its output stream.

```csharp
packet.StreamIndex = 0;
```

The packet timestamps must also be converted from the encoder time base to the stream time base.

```csharp
packet.RescaleTS(encoder.PacketTimeBase);
```

Finally, the packet can be written:

```csharp
muxer.WritePacketInterleaved(packet);
```

The interleaved writer ensures packets are written in the correct order required by the container format.

---

## Draining the Encoder

Submitting the last frame does not necessarily mean the encoder has produced all packets.

Encoders may buffer frames internally, for example when using B-frames.

To retrieve delayed packets, the encoder must be drained by sending a null frame.

```csharp
encoder.DrainEncoder();
```

After entering draining mode, the encoder emits all remaining packets.

```csharp
while (!(result = encoder.ReceivePacket(packet)).IsError)
{
    muxer.WritePacketInterleaved(packet);
}
```

After draining begins, no additional frames may be submitted.

---

## Writing the Trailer

After all packets have been written, the container must be finalized.

```csharp
muxer.WriteTrailer();
```

The trailer writes container-specific end-of-file data and finalizes the output file.

Without writing the trailer, many formats will be incomplete or unreadable.

---

## Summary

This example demonstrates the complete encoding workflow using FFmpegDotNet:

* Create an output container.
* Select and configure a video encoder.
* Add streams to the muxer.
* Generate video frames.
* Convert image data into `AVFrame` instances.
* Encode frames using the send/receive API.
* Write encoded packets into the container.
* Drain the encoder to retrieve delayed packets.
* Finalize the output file.

This workflow forms the foundation for video recorders, transcoders, streaming applications, thumbnail generators, and custom media processing tools.
