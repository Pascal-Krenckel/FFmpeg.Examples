# 7. Filters

This example demonstrates how to create, parse, link, configure, and inspect an FFmpeg filter graph using **FFmpeg.NET**.

FFmpeg filters allow you to process audio and video between decoding and encoding. A filter graph consists of filter contexts connected by links. For example, a video stream can be passed through an `fps` filter to change its frame rate and then through a `format` filter to convert its pixel format.

For more information about FFmpeg's filtering system, see the [FFmpeg Filters Documentation](https://ffmpeg.org/ffmpeg-filters.html) and the [FFmpeg Filtering Guide](https://trac.ffmpeg.org/wiki/FilteringGuide).

## What this example demonstrates

The example creates the following filter chain:

```text
                fps=25          format=yuv420p
[in] ────────────────────────►──────────────────► [out]
```

The graph:

1. Parses a filter string containing an `fps` and `format` filter.
2. Finds the unconnected input and output links created by the parser.
3. Creates a video buffer source.
4. Creates a buffer sink.
5. Links the source to the parsed filter chain.
6. Links the parsed filter chain to the sink.
7. Configures the complete graph.
8. Dumps the configured graph in a human-readable form.

## FilterGraph

A [`FilterGraph`](https://ffmpeg.org/doxygen/trunk/structAVFilterGraph.html) represents a complete FFmpeg filter graph.

A graph can be allocated explicitly:

```csharp
using var graph = FilterGraph.Allocate();
```

Alternatively, a filter graph can be created directly from a filter string using `FilterGraph.Create` or `FilterGraph.TryCreate`.

Some of the most important `FilterGraph` members used when working with filters are:

| Member                   | Description                                           |
| ------------------------ | ----------------------------------------------------- |
| `Allocate`               | Allocates an empty filter graph.                      |
| `Create` / `TryCreate`   | Creates a filter graph from a filter description.     |
| `FindFilter`             | Finds a filter context by its name.                   |
| `Filters`                | Contains all filter contexts in the graph.            |
| `InputFilters`           | Contains buffer source/input filter contexts.         |
| `OutputFilters`          | Contains buffer sink/output filter contexts.          |
| `Link`                   | Links two filter contexts.                            |
| `Insert`                 | Inserts a filter into an existing link.               |
| `Parse` / `ParseAndLink` | Parses a filter description and adds it to the graph. |
| `Config`                 | Configures and validates the filter graph.            |
| `Dump`                   | Returns a human-readable representation of the graph. |

### Parsing a filter chain

The example parses the following filter description:

```text
[in] fps=fps=25,format=pix_fmts=yuv420p [out]
```

with:

```csharp
graph.ParseAndLink(
    out var input,
    "[in] fps=fps=25,format=pix_fmts=yuv420p [out]",
    out var output
).ThrowIfError();
```

`ParseAndLink` creates the filter contexts and links them together.

Because the first filter has an unconnected input and the last filter has an unconnected output, these links are returned through `input` and `output`.

This means that after parsing, the graph conceptually looks like this:

```text
input ──► fps ──► format ──► output
```

The returned `FilterInOut` objects describe the open ends of the graph. They can then be connected to actual source and sink filters.

## FilterContext

A `FilterContext` represents an individual filter within a filter graph.

There are several ways to create one:

```csharp
FilterContext.Allocate(...)
FilterContext.Create(...)
```

`Allocate` creates the filter context without initializing it, while `Create` also initializes it.

For example:

```csharp
var sink = FilterContext.Create(
    "sink",
    Filter.GetFilterByName("buffersink"),
    default(string),
    graph);
```

FFmpeg.NET also provides specialized helpers for commonly used filters. The example uses `VideoBufferSource.Create` to create a video buffer source:

```csharp
var vBuffer = VideoBufferSource.Create(
    "src",
    1920,
    1080,
    PixelFormat.YUV420P,
    new(1, 60),
    graph);
```

A buffer source is used to provide decoded `AVFrame`s to a filter graph. A buffer sink performs the opposite operation and allows processed frames to be retrieved from the graph.

This makes the typical video filtering pipeline look like:

```text
AVFrame
   │
   ▼
Buffer Source
   │
   ▼
Filter(s)
   │
   ▼
Buffer Sink
   │
   ▼
AVFrame
```

## Linking the graph

After creating the source and sink, they are connected to the open ends returned by `ParseAndLink`:

```csharp
graph.Link(vBuffer, input[0].Filter!).ThrowIfError();
graph.Link(output[0].Filter!, sink).ThrowIfError();
```

The resulting graph is:

```text
Buffer Source
      │
      ▼
   fps=25
      │
      ▼
format=yuv420p
      │
      ▼
 Buffer Sink
```

The source and sink are not part of the parsed filter string. They are added and linked separately.

## Filter options

Filters expose their available options through `GetOptions()`.

For example:

```csharp
foreach (var opt in test.GetOptions())
    if (opt.Type != FFmpeg.Options.OptionType.Constant)
        Console.WriteLine($"{opt.Name} ({opt.Type}): {opt.HelpText}");
```

This can be useful when working with filters whose options are not wrapped by a dedicated convenience class.

The example temporarily creates a `waveform` filter, prints its options, and then deletes it:

```csharp
var test = FilterContext.Allocate(
    "not Init and linked",
    Filter.GetFilterByName("waveform"),
    graph);

test.Delete();
```

`Allocate` is useful here because the example only wants to inspect the filter definition and does not need to initialize it.

## Configuring the graph

Once all filters have been created and linked, the graph must be configured:

```csharp
graph.Config().ThrowIfError();
```

Configuration allows FFmpeg to determine and validate things such as:

* compatible formats between filters
* video dimensions
* pixel formats
* sample formats
* time bases
* filter-specific settings
* link properties

The graph should therefore be fully linked before calling `Config`.

After configuration, the graph is ready to process frames.

## Inspecting the graph

The `Dump` method produces a human-readable representation of the filter graph:

```csharp
Console.WriteLine(graph.Dump());
```

This is particularly useful while developing and debugging complex filter graphs.

It is recommended to call `Dump` after `Config`. Apart from making the graph easier to inspect, this can also help detect incorrectly constructed graphs before attempting to process frames.

## Filter sources and sinks

FFmpeg.NET provides specialized helpers for common source and sink filters.

For video, the most important ones are:

* `VideoBufferSource` — accepts video `AVFrame`s and feeds them into the graph.
* `VideoBufferSink` — receives processed video `AVFrame`s from the graph.

Audio uses the corresponding `AudioBufferSource` and `AudioBufferSink`.

There are also source and sink filters that generate or consume data without requiring frames from an external decoder.

## Processing frames

The filter graph itself does not decode or encode media. Once configured, frames are passed through it using the buffer source and buffer sink.

The basic processing model is similar to an `CodecContext`:

```text
source.SendFrame(frame)
        │
        ▼
   Filter Graph
        │
        ▼
sink.ReceiveFrame(frame)
```

A typical application therefore looks like:

```text
Demuxer
   │
   ▼
Decoder
   │
   ▼
Buffer Source
   │
   ▼
Filter Graph
   │
   ▼
Buffer Sink
   │
   ▼
Encoder
   │
   ▼
Muxer
```

This example focuses only on constructing and configuring the filter graph. The actual frame-processing loop is demonstrated in the more complete filtering/transcoding examples.

## Important details

### Filter graphs must be configured

Do not start processing frames before calling:

```csharp
graph.Config().ThrowIfError();
```

FFmpeg performs important initialization and validation during configuration.

### Dispose `FilterInOut` lists

The objects returned by `ParseAndLink` are separate FFmpeg structures and should be disposed once they are no longer needed:

```csharp
input.Dispose();
output.Dispose();
```

The filter contexts themselves remain owned by the `FilterGraph`.

### Filter names are unique

Every `FilterContext` has a unique name within the graph. Filters created by the parser are normally assigned names such as:

```text
Parsed_fps_0
Parsed_format_1
```

The exact generated names should not be relied upon unless necessary. Explicitly created filters can be given their own names, such as `src` and `sink`.