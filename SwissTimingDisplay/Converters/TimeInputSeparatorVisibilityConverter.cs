using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class TimeInputSeparatorVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
            {
                return Visibility.Collapsed;
            }

            var param = parameter?.ToString() ?? string.Empty;
            var parts = param.Split(':');
            if (parts.Length != 2)
            {
                return Visibility.Collapsed;
            }

            var type = parts[0];
            if (!int.TryParse(parts[1], out var desiredDigitsBefore))
            {
                return Visibility.Collapsed;
            }

            char sepChar = type.Equals("colon", StringComparison.OrdinalIgnoreCase) ? ':'
                : type.Equals("dot", StringComparison.OrdinalIgnoreCase) ? '.'
                : '\0';

            if (sepChar == '\0')
            {
                return Visibility.Collapsed;
            }

            var digitsSeen = 0;
            foreach (var ch in s)
            {
                if (char.IsDigit(ch))
                {
                    digitsSeen++;
                    continue;
                }

                if (ch == sepChar)
                {
                    if (digitsSeen == desiredDigitsBefore)
                    {
                        return Visibility.Visible;
                    }

                    continue;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
