using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoPlayer.Controls
{
    /// <summary>Maps PlaybackState to a play/pause glyph for the default template's toggle button.</summary>
    public sealed class PlaybackStateToGlyphConverter : IValueConverter
    {
        public static readonly PlaybackStateToGlyphConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is PlaybackState.Playing ? "\u23F8" /* pause */ : "\u25B6" /* play */;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
