using System;
using System.Globalization;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public sealed class BibNoDigitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
            {
                return -1;
            }

            if (!int.TryParse(s.TrimEnd('.'), out var bibNo) || bibNo < 0)
            {
                return -1;
            }

            if (!int.TryParse(parameter?.ToString(), out var position))
            {
                return -1;
            }

            // position: 0=hundreds, 1=tens, 2=ones
            var hundreds = bibNo / 100;
            var tens = (bibNo / 10) % 10;
            var ones = bibNo % 10;

            var digit = position switch
            {
                0 => hundreds,
                1 => tens,
                2 => ones,
                _ => -1
            };

            if (digit < 0)
            {
                return -1;
            }

            // Blank leading zeros
            if (position == 0 && hundreds == 0)
            {
                return -1;
            }

            if (position == 1 && hundreds == 0 && tens == 0)
            {
                return -1;
            }

            return digit;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
