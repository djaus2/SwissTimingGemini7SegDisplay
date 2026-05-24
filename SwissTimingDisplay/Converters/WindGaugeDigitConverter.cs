using System;
using System.Globalization;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class WindGaugeDigitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
            {
                return -1;
            }

            if (!int.TryParse(parameter?.ToString(), out var digitIndex))
            {
                return -1;
            }

            if (digitIndex < 0)
            {
                return -1;
            }

            // Extract the Nth digit, skipping non-digit characters
            var digitCount = 0;
            foreach (var c in s)
            {
                if (c == '-')
                {
                    if (digitIndex == 0)
                    {
                        return 10; // Minus sign at start
                    }
                    continue;
                }

                if (c == '+')
                {
                    if (digitIndex == 0)
                    {
                        return 11; // Plus sign at start
                    }
                    continue;
                }

                if (char.IsDigit(c))
                {
                    digitCount++; // Increment before checking
                    if (digitCount == digitIndex)
                    {
                        return c - '0';
                    }
                }
            }

            return -1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
