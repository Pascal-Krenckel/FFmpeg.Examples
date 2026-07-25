# Decoding Audio to WAV

This example demonstrates how to use **FFmpegDotNet** to open an audio file, decode its audio stream, convert it into a standard PCM format, and save it as a RIFF/WAVE (`.wav`) file.

The example uses the high-level `MediaSource` API, which automatically configures the required demuxer and decoder, making audio decoding straightforward.

> **Note**
> Audio streams are commonly stored using compressed formats (such as AAC, MP3, or Opus), planar sample layouts, and varying sample rates. Since WAV PCM requires a specific uncompressed format, the decoded audio may need to be converted using `SwrContext` before writing.

## Requirements

Install the following packages:

1. **FFmpegDotNet**
2. **FFmpegDotNet.bin.winx64** (or another FFmpeg binary package appropriate for your platform)

## What the Example Does

1. Initializes the FFmpeg loader.
2. Opens the media file with `MediaSource`.
3. Finds the best audio stream.
4. Disables all other streams to avoid unnecessary decoding.
5. Determines the output audio format:
   - Maximum sample rate: `44100 Hz`
   - Maximum channels: `2`
   - Sample format: `UInt8` or `Int16`
6. Decodes audio frames from the input stream.
7. Converts audio frames if the input format does not match the WAV output format.
8. Buffers decoded samples using `AudioFifo`.
9. Flushes remaining samples from the resampler.
10. Writes the PCM data to a WAV file.

## Running the Example

```text
Example.exe <input-audio> <output-wave>