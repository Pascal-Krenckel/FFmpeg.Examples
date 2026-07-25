# FFmpeg.Examples

Example projects demonstrating how to use **FFmpegDotNet**, a modern .NET wrapper around FFmpeg built on top of FFmpeg.AutoGen.

Each example is a small, self-contained application that focuses on a specific multimedia task using the high-level APIs provided by FFmpegDotNet. The goal is to provide practical reference implementations that can be easily adapted for your own projects.

> Every example includes its own detailed README explaining the code, workflow, and APIs involved.

---

## Prerequisites

Before running any example, make sure you have:

- .NET SDK 8.0 or later
- The latest version of **FFmpegDotNet**
- FFmpeg binaries available on your system (or via the `FFmpegDotNet.bin.winx64` package)

For installation instructions and library documentation, see the main **FFmpegDotNet** repository.

---

## Available Examples

### 🎬 Decoding Video

Demonstrates how to:

- Open a media file
- Decode video frames
- Seek to a random position
- Convert decoded frames to RGB
- Save the result as a bitmap

This example introduces the high-level `MediaSource` API for video decoding.

---

### 🔊 Decoding Audio

Demonstrates how to:

- Open an audio or video file
- Decode audio frames
- Convert audio samples to a common sample format
- Save decoded PCM data as a WAV file

This example shows how FFmpegDotNet simplifies audio decoding while automatically handling demuxing and decoder configuration.

---

### 🎥 Encoding Video

Demonstrates how to:

- Create a video encoder
- Generate synthetic frames
- Encode them into H.264
- Write the encoded stream into an MP4 container

This example illustrates the encoding pipeline using the high-level API.

---

## Repository Structure

```
FFmpeg.Examples
│
├── DecodingAudio/
├── DecodingVideo/
├── EncodingVideo/
└── ...
```

Each project is completely independent and can be opened and run individually.

---

## Purpose

These examples are intended to:

- Learn the FFmpegDotNet API
- Understand common FFmpeg workflows
- Provide copy-and-paste starting points
- Demonstrate recommended usage patterns

Rather than exposing FFmpeg's low-level C API directly, the examples use the managed object-oriented abstractions provided by FFmpegDotNet.

---

## Related Projects

- **FFmpegDotNet** – High-level .NET wrapper around FFmpeg
- **FFmpegDotNet.bin.winx64** – Redistributable FFmpeg binaries for Windows
- **FFmpeg.AutoGen** – Low-level C# bindings used internally

---

## License

This repository is licensed under the same license as FFmpegDotNet.