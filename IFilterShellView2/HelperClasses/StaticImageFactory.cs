using IFilterShellView2.Extensions;
using IFilterShellView2.Native;
using IFilterShellView2.Program;
using System.Collections.Generic;
using System.IO;
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

        public static readonly Dictionary<string, BitmapSource> ImageDictIconsOfExtensions =
            new Dictionary<string, BitmapSource>();



        public static BitmapSource GetPidlBitmapSourceFromDictOrSys(string PidlName, uint FileAttributes)
        {
            if (NativeUtilities.IsAttributeOfFolder(FileAttributes))
            {
                return ImageList[0];
            }
            else
            {
                string Extension = Path.GetExtension(PidlName);

                if (!ImageDictIconsOfExtensions.TryGetValue(Extension, out BitmapSource IconBitmapSource))
                {
                    string FilePath = Path.Combine(Context.Instance.LocationUrlBeforeBrowse, PidlName);
                    IconBitmapSource = NativeUtilities.GetIconBitmapSource(FilePath, false);
                    ImageDictIconsOfExtensions[Extension] = IconBitmapSource;
                    return IconBitmapSource;
                }
                else
                {
                    return IconBitmapSource;
                }
            }
        }
    }
}
