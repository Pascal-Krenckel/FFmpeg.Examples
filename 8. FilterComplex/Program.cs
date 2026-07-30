using FFmpeg;
using FFmpeg.Filters;
using FFmpeg.Utils;
using FFmpeg.Filters.VideoFilters;
using FFmpeg.Codecs;

{
    Console.WriteLine("\n\n\nComplex Example\n=========");
    // Lets use a filtergraph to split display the rgb, r,g,b values of the input video and set the frames per second to 30.
    // fps->scale->format[rgb]->split->lutrgb for each channel, ->xstack->format[yuv420p]
    // this can easily done with the cmd ffmpeg tool, which is preferable, but as an example its fine here:

    // scale=w=iw/2:h=ih/2,
    // [in] fps=fps=30,format=pix_fmts=rgb24,split=4[a][b][c][d];[b]lutrgb=g=0:b=0[x];[c]lutrgb=r=0:b=0[y];[d]lutrgb=r=0:g=0[z];[a][x][y][z]xstack=inputs=4:layout=0_0|0_h0|w0_0|w0_h0,format=pix_fmts=yuv420p [out]

    string filterStr = "[in]fps=fps=30,scale=w=iw/2:h=ih/2,format=pix_fmts=rgb24,split=4[a][b][c][d];[b]lutrgb=g=0:b=0[x];[c]lutrgb=r=0:b=0[y];[d]lutrgb=r=0:g=0[z];[a][x][y][z]xstack=inputs=4:layout=0_0|0_h0|w0_0|w0_h0,format=pix_fmts=yuv420p[out]";
    
    // it's always a good idea to use filter strings, parse them and later just link the src and sink buffers.

    using MediaSource src = MediaSource.Open(args[0]);
    using MediaSink dst = MediaSink.Create(args[1])!;

    int videoStream = src.FindBestStream(FFmpeg.Utils.MediaType.Video);
    int audioStream = src.FindBestStream(FFmpeg.Utils.MediaType.Audio);
    foreach (var stream in src.Streams)
        stream.Discard = FFmpeg.Formats.DiscardFlags.All;
    src.Streams[videoStream].Discard = FFmpeg.Formats.DiscardFlags.Default;
    if (audioStream >= 0)
        src.Streams[audioStream].Discard = FFmpeg.Formats.DiscardFlags.Default;


    using FilterGraph complexFilter = FilterGraph.Allocate();
    var inputFilter = VideoBufferSource.Create("src", src.Streams[videoStream], complexFilter);
    var outputFilter = VideoBufferSink.Create("dst", complexFilter);
    using FilterInOutList inputList = new();
    inputList.Add("out", outputFilter, 0);
    using FilterInOutList outputList = new();
    outputList.Add("in", inputFilter, 0);

    complexFilter.ParseAndLink(inputList, filterStr, outputList);



    complexFilter.Config().ThrowIfError(); // filterOut should now have it's parameters set. We can now access them. Prior we would get a EngineExecutionException

    foreach (var filter in complexFilter.Filters)
    {
        Console.WriteLine($"{filter.Name}");
        foreach (var inputLink in filter.InputFilterLinks.Concat(filter.OutputFilterLinks))
            Console.WriteLine($"\t{inputLink.SourceContext.Name} ({inputLink.SourcePadIndex}) --> {inputLink.DestinationContext.Name} {inputLink.DestinationPadIndex}");
    }

    Console.WriteLine(complexFilter.Dump());

    Codec codec = Codec.FindEncoder("libsvtav1")!.Value;
    using CodecContext encoderContext = CodecContext.Allocate(codec);
    encoderContext.SetOption("crf", 30);
    encoderContext.SetOption("preset", 9);
    encoderContext.SetOption("svtav1-params", "keyint=10s:tune=0");
    encoderContext.Width = outputFilter.Width;
    encoderContext.Height = outputFilter.Height;
    encoderContext.PixelFormat = outputFilter.PixelFormat;
    encoderContext.TimeBase = outputFilter.TimeBase;
    encoderContext.Open(null).ThrowIfError(); // we open the context ourself, it's easier to debug if there was an error
    dst.AddStream(encoderContext);

    if (audioStream >= 0) // just set audio to copy:
        dst.AddStream(src.Streams[audioStream]);

    AVResult32 result;

    using var packet = AVPacket.Allocate();
    using var frame = AVFrame.Allocate();

    while (!(result = src.ReadPacket(packet)).IsError)
    { // we need to read packets, since we do not want to decode audio packets
        if (packet.StreamIndex == audioStream)
        {
            packet.StreamIndex = 1; // our dst audio stream is 1
            dst.WritePacket(packet).ThrowIfError();
        }
        else if (packet.StreamIndex == videoStream)
        {
            result = src.Decode(packet, frame);
            if (result.IsTryAgain)
                continue;
            result.ThrowIfError();
            inputFilter.SendFrame(frame, keepRef: false).ThrowIfError(); // we do not need to keep the ref on the internal buffer
            frame.Unreference();
            while (!(result = outputFilter.ReceiveFrame(frame)).IsError)
            {
                dst.WriteFrame(frame, 0).ThrowIfError(); // our dst vider stream is 0
            }
            if (!result.IsTryAgain)
                result.ThrowIfError();
        }
    }

    if (result != AVResult32.EndOfFile)
        result.ThrowIfError();

    // drain the filter
    inputFilter.Drain().ThrowIfError();
    while (!(result = outputFilter.ReceiveFrame(frame)).IsError)
    {
        dst.WriteFrame(frame, 0).ThrowIfError(); // our dst vider stream is 0
    }
    if (result != AVResult32.EndOfFile)
        result.ThrowIfError();

    // everything was written not lets write the trailer so we can see if something went wring
    dst.WriteTrailer().ThrowIfError();
    dst.Dispose();

    Console.WriteLine("Finished");

    _ = Console.ReadLine();

}
