using IFilterShellView.HelperClasses;
using System;
using System.Globalization;
using System.Windows.Data;

namespace IFilterShellView.Converter
{
    public class ExtensionToBitmapSourceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string PidlName = (string)values[0];
            uint FileAttributes = (uint)values[1];

            return StaticImageFactory.GetPidlBitmapSourceFromDictOrSys(PidlName, FileAttributes);
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
