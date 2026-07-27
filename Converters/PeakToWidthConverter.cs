using System.Globalization;
using System.Windows.Data;

namespace SamplerRecorder.Converters;

/// <summary>
/// Converts a 0..1 float value to a pixel width (multiplied by MaxWidth parameter or 100).
/// </summary>
public class PeakToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double peak = value is float f ? f : 0;
        double maxWidth = 100;
        if (parameter is string s && double.TryParse(s, out var parsed))
            maxWidth = parsed;
        return Math.Max(0, Math.Min(maxWidth, peak * maxWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
