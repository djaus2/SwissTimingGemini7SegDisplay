using System;
using System.Globalization;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class UnderscoreToSpaceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString()?.Replace("_", " ") ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
