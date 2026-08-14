using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoPlayer.Controls
{
    /// <summary>
    /// Computes the width of a slider's "played" fill bar so its right edge lands
    /// exactly on the thumb's center, regardless of the thumb's size.
    ///
    /// This is deliberately independent of WPF's Track/RepeatButton layout math:
    /// Track sizes DecreaseRepeatButton to reach the thumb's LEFT edge, not its
    /// center, and coaxing a correct visual out of that via margins on the
    /// RepeatButton's template turned out to be fragile in practice. Computing
    /// the pixel width directly from Value/Minimum/Maximum/ActualWidth (the same
    /// inputs Track itself uses) sidesteps that entirely.
    ///
    /// values: [0] Value, [1] Minimum, [2] Maximum, [3] ActualWidth of the slider.
    /// parameter: the thumb's width (as a string, e.g. "12") - must match the
    /// actual Width set on the slider's Thumb style.
    /// </summary>
    public sealed class SliderFillWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4
                || values[0] is not double value
                || values[1] is not double minimum
                || values[2] is not double maximum
                || values[3] is not double actualWidth)
            {
                return 0.0;
            }

            if (maximum <= minimum || actualWidth <= 0)
                return 0.0;

            double thumbWidth = 12.0;
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                thumbWidth = parsed;
            if (parameter is double d)
                thumbWidth = d;

            double fraction = (value - minimum) / (maximum - minimum);
            fraction = Math.Clamp(fraction, 0.0, 1.0);

            double usableWidth = Math.Max(0.0, actualWidth - thumbWidth);
            return (thumbWidth / 2.0) + fraction * usableWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}