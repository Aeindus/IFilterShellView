using System;
using System.Globalization;
using System.Windows.Data;

namespace IFilterShellView.Converter
{
    public class StringToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string StringValue)
            {
                return StringValue.ToUpper(culture);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
