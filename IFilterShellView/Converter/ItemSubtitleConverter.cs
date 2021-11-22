using System;
using System.Globalization;
using System.Windows.Data;

namespace IFilterShellView.Converter
{
    public class ItemSubtitleConverter : IMultiValueConverter
    {
        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime ftCreationTime = (DateTime)value[0];
            ulong FileSize = (ulong)value[1];
            uint dwFileAttributes = (uint)value[2];

            var sftCreationTime = ftCreationTime.ToString(Properties.Settings.Default.DateFormat);
            var isFolder = Native.NativeUtilities.IsAttributeOfFolder(dwFileAttributes);

            if (isFolder) return string.Format("Created on {0}", sftCreationTime);

            return string.Format("Created on {0} with {1} bytes", sftCreationTime, FileSize);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
