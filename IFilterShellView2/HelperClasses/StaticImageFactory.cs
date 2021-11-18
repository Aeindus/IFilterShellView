using IFilterShellView2.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Imaging;

namespace IFilterShellView2.HelperClasses
{
    public static class StaticImageFactory
    {
        public static readonly List<BitmapImage> ImageList = new List<BitmapImage>()
        {
            ResourceExtensions.LoadBitmapFromResource("ic_folder.ico"),
            ResourceExtensions.LoadBitmapFromResource("ic_file.ico"),
            ResourceExtensions.LoadBitmapFromResource("ICommandIcon.png"),
            ResourceExtensions.LoadBitmapFromResource("IHistoryIcon.png"),
        };
    }
}
