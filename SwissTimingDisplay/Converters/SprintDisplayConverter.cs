using System;
using System.Globalization;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class SprintDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value?.ToString() ?? string.Empty;
            return name
                .Replace("Distance", "")
                .Replace("Hurdles", " Hurdles");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
