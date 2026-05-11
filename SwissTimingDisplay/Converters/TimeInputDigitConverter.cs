using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class TimeInputDigitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
            {
                return -1;
            }

            if (!int.TryParse(parameter?.ToString(), out var index))
            {
                return -1;
            }

            if (index < 0 || index >= s.Length)
            {
                return -1;
            }

            var c = s[index];
            if (c == '-')
            {
                return 10; // Minus sign
            }

            if (char.IsDigit(c))
            {
                return c - '0';
            }

            return -1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
