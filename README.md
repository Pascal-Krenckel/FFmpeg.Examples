# FFmpeg.Examples

Example projects demonstrating how to use **FFmpegDotNet** for multimedia processing in .NET.

The examples showcase different abstraction levels provided by FFmpegDotNet:

* **High-level APIs** for common multimedia workflows
* **Low-level APIs** for direct control over FFmpeg's decoding and encoding pipeline

The goal of this repository is to provide practical examples that demonstrate how FFmpegDotNet can be used while also showing how the underlying FFmpeg concepts work.

---

# Examples

## 🎥 Decoding

Examples demonstrating how to read media files and decode streams.

---

## 1. Decoding Video

**High-level video decoding example**

This example demonstrates how to use the `MediaSource` API to decode a video frame.

The example:

* Opens a media file
* Automatically configures the required demuxer and decoder
* Seeks to a random position
* Decodes a video frame
* Converts the frame into an RGB image
* Saves the result as a bitmap

This demonstrates how FFmpegDotNet simplifies common decoding scenarios by handling the underlying FFmpeg setup automatically.

---

## 2. Decoding Audio

**High-level audio decoding example**

This example demonstrates decoding audio streams using the high-level API.

The example:

* Opens an audio file
* Decodes audio frames
* Converts samples into a usable format
* Writes the decoded audio data into a WAV file

It shows how FFmpegDotNet can be used to process audio without manually managing the individual FFmpeg components.

---

## 3. Decoding Under The Hood

**Low-level decoding example**

This example demonstrates how the FFmpeg decoding pipeline works internally.

Instead of using the high-level `MediaSource` abstraction, the example manually handles the individual FFmpeg building blocks:

* Opening the input format
* Finding streams
* Creating codec contexts
* Reading packets
* Sending packets to decoders
* Receiving decoded frames

This example is intended for users who want to understand the lower-level FFmpeg workflow or need full control over the decoding process.

---

# Encoding

## 4. Encoding

**Low-level encoding example**

This example demonstrates how to create media files by manually building an encoding pipeline.

The example covers the main FFmpeg encoding workflow:

* Creating an output format context
* Creating streams
* Configuring an encoder
* Allocating frames
* Sending frames to the encoder
* Receiving encoded packets
* Writing packets into the output container

This example shows how FFmpegDotNet exposes the underlying FFmpeg API while still providing a managed .NET interface.

---

# Options

## 5. Options

This example demonstrates how FFmpeg options can be configured using FFmpegDotNet.

FFmpeg uses a flexible option system to configure many components, including:

* Formats
* Codecs
* Filters
* Other FFmpeg modules

This example shows how these options can be passed through the managed API while keeping the same flexibility as the native FFmpeg API.

---

# Planned Examples

## Filters

Examples demonstrating FFmpeg filter graphs.

Planned examples include:

* Creating filter graphs
* Connecting filter inputs and outputs
* Processing decoded frames
* Retrieving filtered frames

---

## Transcoding

Examples demonstrating complete media conversion pipelines.

A transcoding pipeline combines multiple FFmpeg stages:

```
Demux → Decode → Filter → Encode → Mux
```

Planned examples include:

* High-level transcoding using the `Transcoder` abstraction
* Low-level transcoding with manual pipeline management

---

# Running the Examples

1. Clone this repository.

2. Restore NuGet packages.

3. Ensure FFmpeg native libraries are available.

4. Open and run the desired example project.

Each example directory contains a README with additional details about the implementation and concepts demonstrated.

---

# Related Projects

* **FFmpegDotNet**
  The main .NET wrapper library for FFmpeg.

* **FFmpeg.Skia**
  Integration between FFmpegDotNet and SkiaSharp for converting decoded video frames into images.

* **FFmpegDotNet.bin.winx64**
  Package containing FFmpeg native binaries for Windows.

---

# Purpose

These examples are intended to demonstrate the different levels of abstraction available in FFmpegDotNet.

Simple applications can use the high-level APIs, while advanced users can work directly with the underlying FFmpeg components to build custom multimedia pipelines.
