# FFmpeg.Examples

Example projects demonstrating how to use **FFmpegDotNet** for multimedia processing in .NET.

The examples showcase different abstraction levels provided by FFmpegDotNet:

* **High-level APIs** for common multimedia workflows
* **Low-level APIs** for direct control over FFmpeg's decoding, encoding, and processing pipeline

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

# ⚙️ Options

## 4. Options

This example demonstrates how FFmpeg options can be configured using FFmpegDotNet.

FFmpeg uses a flexible option system to configure many components, including:

* Formats
* Codecs
* Filters
* Other FFmpeg modules

This example shows how these options can be passed through the managed API while keeping the same flexibility as the native FFmpeg API.

---

# 🎞️ Encoding

Examples demonstrating how to generate and encode media.

---

## 5. Encoding

**High-level video encoding example**

This example demonstrates how to create a video using the high-level `MediaSink` API.

The example generates video frames, encodes them using the SVT-AV1 encoder (`libsvtav1`), and writes the result to an output file.

It demonstrates how `MediaSink` simplifies the encoding workflow by handling the underlying FFmpeg components and encoding process.

This example is intended for users who want to create encoded media without having to manually manage every step of the FFmpeg encoding pipeline.

---

## 6. Encoding Under The Hood

**Low-level video encoding example**

This example demonstrates how to manually build an FFmpeg encoding pipeline using FFmpegDotNet.

Instead of using the high-level `MediaSink` abstraction, the example directly manages the individual components involved in encoding:

* Creating a `MuxerContext`
* Finding and initializing a video encoder
* Configuring the encoder
* Creating an output stream
* Writing the container header
* Generating `AVFrame` instances
* Sending frames to the encoder
* Receiving encoded packets
* Rescaling packet timestamps
* Writing packets to the muxer
* Draining the encoder
* Writing the container trailer

The example closely follows FFmpeg's native send/receive encoding API and is intended for users who want to understand what happens underneath the high-level encoding abstraction.

It also demonstrates important FFmpeg concepts such as encoder buffering, packet timestamps, stream time bases, and the distinction between codec parameters and encoder state.

---

# 🎛️ Filtering

Examples demonstrating how to construct and use FFmpeg filter graphs.

---

## 7. Filters

**Basic filter graph example**

This example introduces FFmpeg filter graphs and demonstrates how to construct and configure them using FFmpegDotNet.

The example covers:

* Creating a `FilterGraph`
* Creating filter contexts
* Parsing and linking a filter description
* Creating video buffer sources and sinks
* Linking the buffer source and sink to the filter graph
* Inspecting filter options
* Configuring the filter graph
* Dumping the graph in a human-readable format

The example uses a simple filter chain to change the frame rate and pixel format of a video:

```text
Input → FPS → Format → Output
```

This example focuses on the structure and API of filter graphs rather than processing a complete media file.

---

## 8. Complex Filtering

**Complete video decode → filter → encode pipeline**

This example demonstrates how a complex FFmpeg filter graph can be integrated into a complete multimedia processing pipeline.

The example:

* Opens an input media file
* Decodes its video stream
* Processes the video through a complex filter graph
* Changes the frame rate to 30 FPS
* Splits the image into separate red, green, and blue channels
* Combines the resulting images into a 2×2 grid
* Encodes the filtered video using SVT-AV1
* Copies the audio stream without re-encoding it
* Writes the result to an output file

The filter graph is:

```text
fps → scale → format(rgb24) → split
                              ├─ original ──────┐
                              ├─ red only ──────┤
                              ├─ green only ────┤ → xstack → format(yuv420p)
                              └─ blue only ─────┘
```

The example demonstrates how a filter graph can be treated as a reusable processing stage between decoding and encoding:

```text
Demux → Decode → Filter → Encode → Mux
```

It also demonstrates how complex filter graphs can be created from filter strings using `ParseAndLink`, rather than manually creating and connecting every individual filter.

---

# 🎬 Playback

Examples demonstrating real-time, interactive playback rather than one-shot decode/encode pipelines.

---

## 9. Simple Video Player

**Real-time playback example (WPF)**

This example demonstrates how to drive `PlaybackEngine` from an interactive
GUI application, where — unlike every prior example — the pipeline has to run
in real time, stay in sync with an audio device, and respond to transport
commands (play/pause/seek/volume) at any moment.

The example:

* Opens a media file and plays it back with synchronized audio and video
* Renders decoded video frames with SkiaSharp (`SKGLElement`) in a WPF control
* Plays decoded audio with NAudio 3.0 in WASAPI exclusive mode, using the
  audio device itself as `PlaybackEngine`'s playback clock
* Wraps `PlaybackEngine` and NAudio behind a small `IVideoSource` interface,
  so the WPF control and its transport UI don't depend on FFmpeg or NAudio
  directly

This example pairs FFmpegDotNet with **FFmpegDotNet.Skia** (for frame
delivery), **SkiaSharp.Views.WPF** (for GPU-accelerated rendering), and
**NAudio 3.0** (for audio output) — the first example in this repository to
combine FFmpegDotNet with a UI framework and an audio backend.

See `9. SimpleVideoPlayer/README.md` for the full architecture and design notes.

---

# Planned Examples

## Transcoding

Examples demonstrating complete media conversion pipelines.

A transcoding pipeline combines multiple FFmpeg stages:

```text
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

Every example takes its input file as a command-line argument, and
example 9's project already ships a `launchSettings.json` profile pointing at
a bundled sample clip, so F5/`dotnet run` plays something out of the box. The
difference is what happens afterward: examples 1–8 process the file and exit,
while example 9 opens a window and keeps running/playing.

Each example directory contains a README with additional details about the implementation and concepts demonstrated.

---

# Related Projects

* **FFmpegDotNet**
  The main .NET wrapper library for FFmpeg.

* **FFmpegDotNet.Skia**
  Integration between FFmpegDotNet and SkiaSharp for converting decoded video frames into images and generating video frames.

* **FFmpegDotNet.bin.winx64**
  Package containing FFmpeg native binaries for Windows.

---

# Purpose

These examples are intended to demonstrate the different levels of abstraction available in FFmpegDotNet.

Simple applications can use the high-level APIs, while advanced users can work directly with the underlying FFmpeg components to build custom multimedia pipelines.

The examples are also intended to be read progressively: the high-level examples demonstrate what can be accomplished with minimal FFmpeg knowledge, while the corresponding **Under The Hood** examples expose the individual FFmpeg components and the processing steps that the high-level APIs manage internally. Example 9 sits at a different point on that spectrum entirely — it shows FFmpegDotNet doing real-time work inside a live application, rather than a batch job that runs start to finish.