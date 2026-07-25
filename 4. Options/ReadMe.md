# Working with AVOptions

Many FFmpeg objects expose an **AVClass**, which provides runtime information about the object. Classes such as `CodecContext`, `FormatContext`, `FilterContext`, and many others use this mechanism to describe themselves and expose configurable options.

These **AVOptions** are FFmpeg's generic configuration system. They allow applications to query supported options at runtime and configure codecs, filters, muxers, demuxers, and other components without requiring component-specific APIs.

In this example we create a `CodecContext` for the **SVT-AV1** encoder (`libsvtav1`), enumerate all supported options, and configure the encoder using the AVOptions API.

The configuration is equivalent to the following FFmpeg command-line options:

```text
-crf 30
-preset 6
-svtav1-params keyint=10s:tune=0:enable-overlays=1:scd=1:scm=0
```

For more information about the encoder, see the official FFmpeg documentation:

https://ffmpeg.org/ffmpeg-codecs.html#libsvtav1

---

# Steps

1. Find the `libsvtav1` encoder.
2. Allocate a `CodecContext`.
3. Enumerate all supported AVOptions.
4. Configure encoder options.
5. Configure required codec properties.
6. Open the encoder.

---

# Finding the Encoder

```csharp
Codec? codec = Codec.FindEncoder("libsvtav1");
```

Unlike decoding, multiple encoder implementations may exist for the same codec.

`Codec.FindEncoder()` locates the requested encoder by name and returns `null` if it is not available in the current FFmpeg build.

---

# Allocating a Codec Context

```csharp
using CodecContext encoderContext = CodecContext.Allocate(codec);
```

`CodecContext.Allocate()` allocates an encoder context but does **not** initialize it.

This allows all required options and codec properties to be configured before the encoder is opened.

---

# Enumerating AVOptions

Every object exposing an `AVClass` can enumerate its available options.

```csharp
foreach (var option in encoderContext.GetOptions())
{
    ...
}
```

Each option contains information such as:

- name
- type
- help text
- default value
- valid range
- flags

This allows applications to build configuration dialogs or discover supported options dynamically without hardcoding codec-specific settings.

---

# Setting Options

Options can be configured individually.

```csharp
encoderContext.SetOption("crf", 30);
encoderContext.SetOption("preset", 6);
```

or by passing an `IDictionary<string, string>`.

```csharp
encoderContext.SetOption(options);
```

Options that are successfully applied are removed from the dictionary, while unsupported options remain.

This makes it easy to detect misspelled or unsupported option names.

Nested option groups such as `svtav1-params` can also be configured using an `AVDictionary`.

```csharp
encoderContext.SetOption("svtav1-params", svtav1);
```

Using `AVDictionary` avoids the temporary allocation required when converting an `IDictionary<string, string>` into FFmpeg's native dictionary format.

---

# Configuring the Encoder

In addition to AVOptions, several codec properties must be configured before opening the encoder.

```csharp
encoderContext.Width = 1920;
encoderContext.Height = 1080;
encoderContext.PixelFormat = codec.Value.SupportedPixelFormats[0];
encoderContext.TimeBase = new(1, 60);
```

Some of these values are also exposed as AVOptions.

For example, the frame size can alternatively be configured using:

```csharp
encoderContext.SetOption("video_size", (1920, 1080));
```

Whether a property is available as an AVOption depends on the specific FFmpeg component.

---

# Opening the Encoder

Once all required properties and options have been configured, the encoder can be opened.

```csharp
encoderContext.Open(null).ThrowIfError();
```

Opening validates all supplied options and initializes the encoder.

After the encoder has been opened, most options become read-only and can no longer be modified.

---

# Summary

This example demonstrates how to use FFmpegDotNet's AVOptions API to configure FFmpeg components at runtime.

It shows how to:

- Discover available options exposed by an `AVClass`.
- Enumerate option metadata.
- Configure options individually or using dictionaries.
- Pass nested option dictionaries using `AVDictionary`.
- Configure required codec properties.
- Initialize an encoder after all settings have been applied.

The same AVOptions API is used throughout FFmpeg for codecs, demuxers, muxers, filters, resamplers, scalers, and many other components, making it a powerful and consistent way to configure FFmpeg objects.