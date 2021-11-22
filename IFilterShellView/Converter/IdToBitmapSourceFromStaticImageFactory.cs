using IFilterShellView.HelperClasses;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace IFilterShellView.Converter
{
    public class IdToBitmapSourceFromStaticImageFactory : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return StaticImageFactory.ImageList[(int)value];
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
