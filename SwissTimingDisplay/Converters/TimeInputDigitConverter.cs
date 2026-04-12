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

            var digits = s.Where(char.IsDigit).ToArray();
            if (!int.TryParse(parameter?.ToString(), out var index))
            {
                return -1;
            }

            if (index < 0 || index >= digits.Length)
            {
                return -1;
            }

            return digits[index] - '0';
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
