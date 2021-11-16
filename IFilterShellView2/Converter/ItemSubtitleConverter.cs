using System;
using System.Globalization;
using System.Windows.Data;

namespace IFilterShellView2.Converter
{
    public class ItemSubtitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return "Created on " + ((DateTime)value).ToString(Properties.Settings.Default.DateFormat);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
