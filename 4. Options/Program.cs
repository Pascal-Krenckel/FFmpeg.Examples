using FFmpeg.Codecs;
using FFmpeg.Collections;
using FFmpeg.Options;


Codec? codec = Codec.FindEncoder("libsvtav1");
if (codec == null)
{
    Console.WriteLine("libsvt-av1 not found");
    return -1;
}

// Allocate the codec context. We do not open the encoder yet.
using CodecContext encoderContext = CodecContext.Allocate(codec);

Console.WriteLine("{0}\n================================================", codec.Value.LongName);
Console.WriteLine("Supported pixel formats: {0}", string.Join(", ", codec.Value.SupportedPixelFormats.ToArray()));
Console.WriteLine("Supported frame rates: {0}", string.Join(", ", codec.Value.SupportedFramerates.ToArray()));
Console.WriteLine("Supported profiles: {0}", string.Join(", ", codec.Value.Profiles));

Console.WriteLine("\nOptions\n-------");

// Skip constant values. They can be used as option values,
// e.g. encoderContext.SetOption("optionName", "constant");
foreach (Option? option in encoderContext.GetOptions()
    .Where(o => o.Type is not OptionType.Constant and not OptionType.ConstantArray)
    .OrderBy(o => o.Name))
{
    Console.WriteLine($"{option.Name} ({option.Type}): {option.HelpText}");
}

// Equivalent to the FFmpeg command line:
//
//   -crf 30 -preset 6
//   -svtav1-params keyint=10s:tune=0:enable-overlays=1:scd=1:scm=0
//
// Options can be set individually or by using a dictionary.

encoderContext.SetOption("crf", 30);
encoderContext.SetOption("preset", 6);
encoderContext.SetOption("svtav1-params", "keyint=10s:tune=0:enable-overlays=1:scd=1:scm=0");

// Dictionary approach.
//
// AVDictionary maps directly to FFmpeg's native dictionary and avoids the
// temporary allocation required when passing an IDictionary<string, string>.
//
//
// This time, we'll pass the svtav1 parameters as a separate dictionary.
Dictionary<string, string> options = new()
{
    ["crf"] = "30",
    ["preset"] = "6",
    ["unknownOption"] = "BLUB"
};

AVDictionary svtav1 = new()
{
    ["keyint"] = "10s",
    ["tune"] = "0",
    ["enable-overlays"] = "1",
    ["scd"] = "1",
    ["scm"] = "0"
};

// Unknown options are not treated as an error.
// Successfully applied options are removed from the dictionary,
// while unsupported options remain.
encoderContext.SetOption(options);
encoderContext.SetOption("svtav1-params", svtav1);

Console.WriteLine("\n\nThe following options could not be set: " +
    string.Join(',', options.Select(kv => $"[{kv.Key}] = {kv.Value}")));

Console.WriteLine($"Unknown option: {encoderContext.SetOption("unknownOption Name", "7")}");
Console.WriteLine($"Invalid option value: {encoderContext.SetOption("crf", "BLUB")}");

// Before opening the encoder, we must configure its basic properties.
// At a minimum, this includes the frame size, pixel format, and time base.

// Some encoders also expose these settings as AVOptions (for example,
// "video_size"), which can be used instead of setting the properties.

encoderContext.SetOption("video_size", (1920, 1080));

// Alternatively, the frame size can be configured directly.
encoderContext.Width = 1920;
encoderContext.Height = 1080;

// Use the first supported pixel format.
encoderContext.PixelFormat = codec.Value.SupportedPixelFormats[0];

// 1/60 second time base (60 FPS).
// The encoder may adjust this to a more appropriate value when opened.
encoderContext.TimeBase = new(1, 60);

Console.WriteLine("\n\n\n\n\n============= OPENING THE CODEC ==============");

// All required parameters have been configured, so the encoder can now be opened.
encoderContext.Open(null).ThrowIfError();

return 0;
