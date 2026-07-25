# Decoding Audio and Video

In this example we will manually decode a media file using **FFmpegDotNet** instead of the higher-level `MediaSource` class. This gives full control over demuxing, decoder initialization, packet handling, and frame processing while closely following FFmpeg's native decoding API.

## Steps

1. Create a `DemuxerContext`.
2. Find the audio and/or video streams.
3. Create a `CodecContext` for each stream.
4. Initialize the `CodecContext` using the stream's codec parameters.
5. Read packets from the media container.
6. Send packets to the appropriate decoder.
7. Receive decoded frames.
8. Drain the decoders after reaching the end of the file.

> **Note**
>
> In FFmpegDotNet, FFmpeg's `FormatContext` is split into `DemuxerContext` and `MuxerContext` to provide a cleaner API for reading and writing media files.

---

## Opening the Media File

```csharp
using DemuxerContext mediaFile = DemuxerContext.Open(inputFile, findStreamInfo: true);
```

`DemuxerContext.Open()` opens the media container and creates the appropriate demuxer.

Passing `findStreamInfo: true` instructs FFmpeg to analyze the file and discover information such as:

- available streams
- codecs
- frame rate
- duration
- channel layout
- sample rate

Depending on the container format, FFmpeg may read several packets before enough information is available.

---

## Finding Streams

```csharp
int videoStreamIndex = mediaFile.FindBestStream(MediaType.Video);
int audioStreamIndex = mediaFile.FindBestStream(MediaType.Audio);
```

A media file may contain multiple audio, video, subtitle, or data streams.

`FindBestStream()` selects the stream FFmpeg considers the best candidate for the requested media type.

If no suitable stream exists, the method returns `-1`.

---

## Creating the Decoder

Each stream contains **codec parameters**, but these parameters are **not** the decoder itself.

```csharp
CodecID codecId = stream.CodecId;
Codec codec = Codec.FindDecoder(codecId)!.Value;
```

A **Codec ID** identifies the compression format (for example H.264, HEVC, AV1 or AAC).

Multiple decoder implementations may exist for the same codec. For example, AV1 supports decoders such as:

- `libdav1d`
- `libaom-av1`
- `av1`
- `av1_cuvid`
- `av1_qsv`
- `av1_amf`

`Codec.FindDecoder()` selects the most appropriate decoder available on the current system.

Once a decoder has been selected, create a `CodecContext`.

```csharp
CodecContext codecContext = CodecContext.Open(codec, codecParameters);
```

The codec context stores all runtime state required during decoding.

If hardware acceleration is desired, this is also where a hardware device should be configured before opening the decoder.

---

## Time Bases

FFmpeg stores timestamps as integer values together with a **time base**.

```
seconds = pts × timeBase
```

For decoding, the stream's time base is usually assigned to the codec context.

```csharp
codecContext.PacketTimeBase = stream.TimeBase;
codecContext.TimeBase = stream.TimeBase;
```

When a frame is received, the example assigns the codec's time base to the frame.

```csharp
frame.TimeBase = codecContext.TimeBase;
```

Current FFmpeg versions generally leave `AVFrame.TimeBase` unset when decoding, so assigning it manually makes timestamp calculations much easier.

---

# FFmpeg's Decoding API

FFmpeg uses an asynchronous **send/receive** API for decoding.

Unlike older FFmpeg versions, decoding is no longer performed by calling a single function that converts one packet into one frame.

Instead, packets are sent to the decoder, while decoded frames are retrieved separately.

The complete workflow is:

1. Read packets from the `DemuxerContext`.
2. Send packets to the `CodecContext`.
3. Receive decoded frames from the `CodecContext`.
4. Repeat until the end of the file.
5. Drain the decoder to retrieve delayed frames.

One packet may produce:

- no frames
- one frame
- multiple frames

Likewise, some frames require data from multiple packets before they can be decoded.

This allows FFmpeg to efficiently decode codecs that internally buffer frames, such as H.264, HEVC, and AV1.

For more information see the official FFmpeg documentation:

https://ffmpeg.org/doxygen/trunk/group__lavc__encdec.html

---

## Handling `TryAgain`

The decoder communicates its current state using `AVResult32.TryAgain`.

### `SendPacket()` returns `TryAgain`

The decoder's internal packet queue is full.

You must first retrieve one or more decoded frames before trying to send the packet again.

```text
SendPacket(packet)
        ↓
TryAgain
        ↓
ReceiveFrame(frame)
        ↓
SendPacket(packet)
```

### `ReceiveFrame()` returns `TryAgain`

No decoded frame is currently available.

The decoder requires additional input packets before it can produce another frame.

This commonly happens with video codecs because a single frame may depend on multiple compressed packets.

```text
ReceiveFrame(frame)
        ↓
TryAgain
        ↓
ReadPacket(packet)
        ↓
SendPacket(packet)
        ↓
ReceiveFrame(frame)
```

---

## Decoding Loop (Variant 1)

This version continuously reads packets and feeds them into the decoder.

Whenever the decoder's packet queue becomes full, decoded frames are retrieved before sending the packet again.

```csharp
while (!(result = FormatContext.ReadPacket(packet)).IsError)
{
    while ((result = CodecContext.SendPacket(packet)) == AVResult32.TryAgain)
    {
        CodecContext.ReceiveFrame(frame).ThrowIfError();
        HandleFrame(frame);
    }

    result.ThrowIfError();
}
```

This is the simplest decoding loop and mirrors the implementation shown in this example.

---

## Decoding Loop (Variant 2)

Instead of always reading packets first, this version continuously requests frames from the decoder.

Whenever the decoder requires more input, another packet is read and submitted.

```csharp
while (result != AVResult32.EndOfFile)
{
    while ((result = CodecContext.ReceiveFrame(frame)) == AVResult32.TryAgain)
    {
        if ((result = FormatContext.ReadPacket(packet)) == AVResult32.EndOfFile)
            break;

        result.ThrowIfError();
        CodecContext.SendPacket(packet).ThrowIfError();
    }

    result.ThrowIfError();
    HandleFrame(frame);
}
```

`MediaSource` internally follows this approach. Since packets are submitted immediately, decoded frames can simply be returned whenever they become available.

---

# Draining the Decoder

Reaching the end of the input file does **not** necessarily mean every decoded frame has already been returned.

Many codecs buffer frames internally (for example B-frames).

To retrieve these delayed frames, the decoder must be switched into **draining mode**.

Draining is initiated by sending a **null packet**.

```csharp
CodecContext.SendPacket(null);

while (!(result = CodecContext.ReceiveFrame(frame)).IsError)
{
    HandleFrame(frame);
}

if (result != AVResult32.EndOfFile)
    result.ThrowIfError();
```

After a decoder has entered draining mode, **no additional packets may be sent**.

---

# Seeking

When seeking with `DemuxerContext.Seek()`, the decoder may still contain packets or partially decoded frames from the previous playback position.

Before decoding from the new position, flush the decoder to discard all buffered data.

Otherwise, frames from the previous position may still be returned.

---

# Multiple Streams

A media container can contain multiple streams.

For example:

- one video stream
- multiple audio streams
- subtitles
- attachments
- metadata streams

Each audio/video stream requires its own `CodecContext`.

The `AVPacket.StreamIndex` property identifies which stream a packet belongs to, allowing the packet to be forwarded to the correct decoder.

---

# Summary

This example demonstrates the complete decoding workflow using FFmpegDotNet:

- Open a media container.
- Discover the available streams.
- Select the appropriate decoder for each stream.
- Create and initialize a `CodecContext`.
- Read compressed packets.
- Decode packets into frames using the send/receive API.
- Process decoded audio and video frames.
- Drain the decoders to retrieve delayed frames.
- Flush decoders when seeking.
- Manage multiple decoders when working with multiple streams.

This workflow forms the foundation for media players, transcoders, thumbnail generators, frame extraction tools, and other media processing applications.