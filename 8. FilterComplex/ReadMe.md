# 8. Complex Filtering

This example demonstrates a complete video-processing pipeline using a more complex FFmpeg filter graph.

Unlike the previous example, which focused on constructing a filter graph, this example connects the graph to a real media source and sink. It:

* Opens an input media file.
* Decodes its video stream.
* Processes the video through a complex filter graph.
* Displays the original image together with separate red, green, and blue channels.
* Changes the frame rate to 30 FPS.
* Encodes the filtered video using SVT-AV1.
* Copies the audio stream without re-encoding it.
* Writes the result to an output file.

The resulting video consists of four images arranged in a 2×2 grid:

```text
┌───────────────┬───────────────┐
│   Original    │      Red      │
│               │    channel    │
├───────────────┼───────────────┤
│     Green     │      Blue     │
│    channel    │    channel    │
└───────────────┴───────────────┘
```

The example uses a filter graph equivalent to:

```text
fps → scale → format(rgb24) → split
                              ├─ original ──────┐
                              ├─ red only ──────┤
                              ├─ green only ────┤ → xstack → format(yuv420p)
                              └─ blue only ─────┘
```

## The filter string

The complete filter description is:

```text
[in]
  fps=fps=30,
  scale=w=iw/2:h=ih/2,
  format=pix_fmts=rgb24,
  split=4[a][b][c][d];

[b]lutrgb=g=0:b=0[x];
[c]lutrgb=r=0:b=0[y];
[d]lutrgb=r=0:g=0[z];

[a][x][y][z]
  xstack=inputs=4:layout=0_0|0_h0|w0_0|w0_h0,
  format=pix_fmts=yuv420p
[out]
```

The filter graph first converts the input to RGB and splits it into four branches.

Three of the branches are passed through `lutrgb` to remove two of the three color channels:

* `x` contains only red.
* `y` contains only green.
* `z` contains only blue.

The four images are then combined using `xstack` and finally converted back to `yuv420p` for encoding.

## Why use a filter string?

Complex filter graphs can be constructed manually by creating every `FilterContext` and linking every filter individually. However, this quickly becomes cumbersome for graphs with many branches.

For example, this graph contains:

* `fps`
* `scale`
* `format`
* `split`
* three `lutrgb` filters
* `xstack`
* another `format`

Rather than creating and linking all of these filters manually, the example lets FFmpeg parse the filter description:

```csharp
complexFilter.ParseAndLink(inputList, filterStr, outputList);
```

For complex graphs, using a filter string is generally much easier to read and maintain.

The application only needs to create the buffer source and buffer sink and connect their open pads to the corresponding open ends of the parsed filter graph.
## Connecting the filter graph

The source and destination are created first:

```csharp
var inputFilter = VideoBufferSource.Create(
    "src",
    src.Streams[videoStream],
    complexFilter);

var outputFilter = VideoBufferSink.Create(
    "dst",
    complexFilter);
```

The important part here is how the `FilterInOutList`s are constructed:

```csharp
using FilterInOutList inputList = new();
inputList.Add("out", outputFilter, 0);

using FilterInOutList outputList = new();
outputList.Add("in", inputFilter, 0);
```

At first glance, the names `inputList` and `outputList` can seem counterintuitive. The important thing to understand is that these names describe the **boundary of the filter graph**, not the input or output role of the individual `FilterContext`.

### The filter graph as a graph section

A `FilterGraph` can be viewed as a section, or subgraph, of a larger filter graph:

```text
                   Filter Graph
                ┌─────────────────┐
                │                 │
        ───────►│        ?        │──────► 
                │                 │
                └─────────────────┘
```

* **Inputs** are all links **into** the graph.
* **Outputs** are all links **out of** the graph.

This distinction is important because the individual filters have their own input and output pads, which describe the direction of data flow through those filters.

For example, the `VideoBufferSource` has only one pad, its output pad. If that pad is not yet linked, the open link is **leaving the filter graph**:

```text
VideoBufferSource ─►
```

Conversely, the `VideoBufferSink` has only one pad, its input pad. If that pad is not yet linked, the open link is **entering the filter graph**:

```text
 ─►  VideoBufferSink
```


```text
                             Filter Graph
                        ┌────────────────────┐
                        │                    │
       ───────► VideoBufferSink    ?    VideoBufferSrc ──────► 
                        │                    │
                        └────────────────────┘
```

The `FilterInOutList` terminology describes **the direction of links relative to the graph section**, while the input/output pads of a `FilterContext` describe the direction of data flow through that individual filter.

### Stable input and output semantics

This distinction gives `FilterInOutList` a stable semantic meaning:

> **Input = links entering the filter graph.**
> **Output = links leaving the filter graph.**

This remains true regardless of which portion of the graph is currently being parsed or linked.

FFor example, `ParseAndLink` can be called multiple times while progressively constructing the graph:

```csharp
var filters = filterStr.Split(';');

foreach (var filter in filters)
    complexFilter.ParseAndLink(input, filter, output);
```

The important point is that the meaning of `input` and `output` does not depend on which part of the graph is currently being parsed. They are always defined from the perspective of the `FilterGraph`:

* `input` contains links entering the graph.
* `output` contains links leaving the graph.

Consequently, there is no need to swap the two lists when parsing different parts of the graph. The same definition remains valid whether the filter graph is empty, partially constructed, or fully built.


## Configuring the graph

After parsing and linking, the graph must be configured:

```csharp
complexFilter.Config().ThrowIfError();
```

Configuration is important because FFmpeg determines properties such as the resulting:

* width
* height
* pixel format
* time base
* link formats
* filter-specific parameters

Only after configuration can the output filter reliably expose its resulting properties:

```csharp
encoderContext.Width = outputFilter.Width;
encoderContext.Height = outputFilter.Height;
encoderContext.PixelFormat = outputFilter.PixelFormat;
encoderContext.TimeBase = outputFilter.TimeBase;
```

Trying to access properties that depend on graph configuration before calling `Config()` can result in invalid data and, in some cases, an `ExecutionEngineException` caused by FFmpeg dereferencing an invalid or null pointer.

## Inspecting the graph

The example first prints every filter and its connections:

```csharp
foreach (var filter in complexFilter.Filters)
{
    Console.WriteLine($"{filter.Name}");

    foreach (var inputLink in
        filter.InputFilterLinks.Concat(filter.OutputFilterLinks))
    {
        Console.WriteLine(
            $"\t{inputLink.SourceContext.Name} " +
            $"({inputLink.SourcePadIndex}) --> " +
            $"{inputLink.DestinationContext.Name} " +
            $"{inputLink.DestinationPadIndex}");
    }
}
```

It then prints FFmpeg's human-readable graph representation:

```csharp
Console.WriteLine(complexFilter.Dump());
```

This is particularly useful for debugging complex graphs where many filters branch and reconnect.

## Encoding the filtered video

The filtered output is encoded using SVT-AV1:

```csharp
Codec codec = Codec.FindEncoder("libsvtav1")!.Value;
using CodecContext encoderContext = CodecContext.Allocate(codec);
```

The encoder is configured using the properties obtained from the output filter:

```csharp
encoderContext.Width = outputFilter.Width;
encoderContext.Height = outputFilter.Height;
encoderContext.PixelFormat = outputFilter.PixelFormat;
encoderContext.TimeBase = outputFilter.TimeBase;
```

This is a useful pattern when the filter graph itself determines the final video format.

The encoder is then opened and added to the output:

```csharp
encoderContext.Open(null).ThrowIfError();
dst.AddStream(encoderContext);
```

## Copying the audio stream

The example does not filter or re-encode audio.

If the input contains an audio stream, it is added directly to the output:

```csharp
if (audioStream >= 0)
    dst.AddStream(src.Streams[audioStream]);
```

The main packet-processing loop therefore handles the two streams differently:

```text
Input
 │
 ├── Audio ───────────────► Stream Copy ─────────► Output
 │
 └── Video ─► Decode ─► Filter Graph ─► Encode ─► Output
```

Audio packets are written directly:

```csharp
if (packet.StreamIndex == audioStream)
{
    packet.StreamIndex = 1;
    dst.WritePacket(packet).ThrowIfError();
}
```

Video packets are decoded into frames instead:

```csharp
result = src.Decode(packet, frame);
```

The decoded frame is then sent into the filter graph:

```csharp
inputFilter.SendFrame(
    frame,
    keepRef: false
).ThrowIfError();
```

The application only needs to create the buffer source and buffer sink and connect their open pads to the corresponding open ends of the parsed filter graph.
## Receiving filtered frames

The filtered frames are retrieved from the video buffer sink:

```csharp
while (!(result = outputFilter.ReceiveFrame(frame)).IsError)
{
    dst.WriteFrame(frame, 0).ThrowIfError();
}
```

The resulting pipeline is therefore:

```text
ReadPacket
    │
    ▼
Decode
    │
    ▼
AVFrame
    │
    ▼
VideoBufferSource
    │
    ▼
Complex Filter Graph
    │
    ▼
VideoBufferSink
    │
    ▼
Encoder
    │
    ▼
Output
```

## Draining the filter graph

After all input packets have been processed, the filter graph must be drained:

```csharp
inputFilter.Drain().ThrowIfError();

while (!(result = outputFilter.ReceiveFrame(frame)).IsError)
{
    dst.WriteFrame(frame, 0).ThrowIfError();
}
```

This is necessary because filters can buffer frames internally.

Simply reaching end-of-file does not necessarily mean that every frame has already emerged from the filter graph. Sending the drain signal tells the source that no more input will arrive and allows the filters to produce any remaining output.

The same general principle applies to encoders and other buffered components: after processing all input, they must be flushed or drained as appropriate.

## Complete processing pipeline

The complete application can be summarized as:

```text
                    ┌──────────────────────┐
                    │      MediaSource     │
                    └──────────┬───────────┘
                               │
                     ┌─────────┴─────────┐
                     │                   │
                   Audio               Video
                     │                   │
                     ▼                   ▼
               Stream Copy            Decode
                     │                   │
                     │                   ▼
                     │             Buffer Source
                     │                   │
                     │                   ▼
                     │          ┌─────────────────┐
                     │          │    fps = 30     │
                     │          │      scale      │
                     │          │     format      │
                     │          │      split      │
                     │          │    /  /  \  \   │
                     │          │   RGB channels  │
                     │          │     xstack      │
                     │          │     format      │
                     │          └────────┬────────┘
                     │                   │
                     │                   ▼
                     │              Buffer Sink
                     │                   │
                     │                   ▼
                     │               SVT-AV1
                     │                   │
                     └─────────┬─────────┘
                               ▼
                         MediaSink
```

Finally, the output trailer is written:

```csharp
dst.WriteTrailer().ThrowIfError();
```

This finalizes the output container and gives the muxer an opportunity to report any errors that occurred while completing the file.

## Running the example

The example expects two command-line arguments:

```bash
dotnet run --project 8.ComplexFiltering input.mp4 output.mkv
```

Where:

* `input.mp4` is the source media file.
* `output.mkv` is the generated output file.

The resulting video contains the original, red, green, and blue versions of the input video arranged in a 2×2 grid, with the frame rate changed to 30 FPS.

> **Note:** This is intentionally an example of using the FFmpeg.NET API. For simply applying this particular filter graph to a file, the FFmpeg command-line tool is considerably more convenient. The purpose here is to demonstrate how a complex filter graph can be integrated into a complete decode → filter → encode pipeline using the C# API.
