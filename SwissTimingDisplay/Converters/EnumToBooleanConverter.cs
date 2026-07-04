using System;
using System.Globalization;
using System.Windows.Data;

namespace SwissTimingDisplay.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valStr = System.Convert.ToString(value);
            var paramStr = System.Convert.ToString(parameter);
            if (valStr is null || paramStr is null)
            {
                return false;
            }

            return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
            {
                var paramStr = System.Convert.ToString(parameter);
                if (paramStr is null)
                {
                    return Binding.DoNothing;
                }
                return Enum.Parse(targetType, paramStr);
            }
            return Binding.DoNothing;
        }
    }
}
