using FFmpeg;
using FFmpeg.Codecs;
using FFmpeg.Filters;
using FFmpeg.Filters.VideoFilters;
using FFmpeg.Images;
using FFmpeg.Utils;

// Short introduction, please see https://ffmpeg.org/ffmpeg-filters.html and https://trac.ffmpeg.org/wiki/FilteringGuide for further information

/*
 * Important classes:
 * FilterGraph: contains the filter graph
 * * Allocate static
 * * (Try)Create static: Creates a filtergraph from the given filter string
 * * FindFilter: Finds a filter context by its given name 
 * * Filters: List of all filter contexts
 * * InputFilters: List of all buffer source filter contexts (audio and video)
 * * OutputFilters: List of all buffer sink filter contexts (audio and video)
 * * Link/Insert: Links to filters or inserts a filter between two others defined by the given link
 * * Parse(AndLink): Parses a filter string and links them
 * * Config: Check validity and configure all the links and formats in the graph. 
 * * Dump: Dump a graph into a human-readable string representation.
 * * * Call after config too make sure the graph is valid, otherwise you might ffmpeg might throw a ExecutionEngineException (null ptr dereference)
 * 
 * FilterContext:
 * * Allocate static: Creates a filter context
 * * Create static: creates a filter context and initializes it
 * * Init: Initializes the filter context with the given parameters
 * * SendFrame/ReceiveFrame for BufferSource/Sink allows you to send and receive AVFrame similar to CodecContext
 * 
 */
// Every filter is described by a FilterGraph
{
    using var graph = FilterGraph.Allocate();
    // you can esily parse and link filter into your graph
    // FilterGraph.Create(...) => FilterGraph.Allocate().ParseAndLink(...)
    // The following filter changes the fps to 25 and pixel format to yuv420p
    // Both will be linked together, fps has a missing input link and will be in input and format in output

    graph.ParseAndLink(out var input, "[in] fps=fps=25,format=pix_fmts=yuv420p [out]", out var output).ThrowIfError();

    // Every FilterContext has its unique name. Usually ffmpeg names them if parsed: [Parsed_<filter>_<No>]


    Console.WriteLine("Inputs");
    foreach (var i in input)
        Console.WriteLine($"{i.Name}: {i.Filter!.Name}"); // should be in: fps_parsed_0
    Console.WriteLine("Outputs");
    foreach (var o in output)
        Console.WriteLine($"{o.Name}: {o.Filter!.Name}"); // should be out: format_parsed_0


    // Since the is one input pad unlinked, we need to add a source.
    // Source filter either generate data
    // There is also buffer (video) and abuffer (audio) which take video/ audio frames to send them through the graph.

    // the next line creates a filter named src of type buffer
    // var vBuffer = graph.CreateFilter("src", Filter.VideoBufferSource, "width=1920:height=1080:pix_fmt=rgb24:time_base=1/60");

    // for some filter FFmpegDotNet has already functions to make the filter creation easier, these are part of the FilterContext class

    var vBuffer = VideoBufferSource.Create("src", 1920, 1080, PixelFormat.YUV420P, new(1, 60), graph);
    var sink = FilterContext.Create("sink", Filter.GetFilterByName("buffersink"), default(string), graph);
    var test = FilterContext.Allocate("not Init and linked", Filter.GetFilterByName("waveform"), graph);
    // Both functions create the appropriate buffer/sink type (audio or video=

    // you can check the options
    foreach (var opt in test.GetOptions())
        if (opt.Type != FFmpeg.Options.OptionType.Constant)
            Console.WriteLine($"{opt.Name} ({opt.Type}): {opt.HelpText}");


    test.Delete();
    // delete test filter

    graph.Link(vBuffer, input[0].Filter!).ThrowIfError();
    graph.Link(output[0].Filter!, sink).ThrowIfError();

    // dispose of the FilterInOutList
    input.Dispose();
    output.Dispose();

    // With this the filter chain is finished
    // This will configure all internal filter settings like video_size and check validity, etc
    graph.Config().ThrowIfError();

    // If the graph in not configured this my throw an EngineException as there will be some null_ptr dereferenciation in ffmpeg's library
    Console.WriteLine(graph.Dump());
}
