using IFilterShellView.Extensions;
using IFilterShellView.Native;
using IFilterShellView.Program;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace IFilterShellView.HelperClasses
{
    public static class StaticImageFactory
    {
        public static readonly List<BitmapImage> ImageList = new List<BitmapImage>()
        {
            ResourceExtensions.LoadBitmapFromResource("icon_folder.ico"),
            ResourceExtensions.LoadBitmapFromResource("icon_file.ico"),
            ResourceExtensions.LoadBitmapFromResource("image_command.png"),
            ResourceExtensions.LoadBitmapFromResource("image_history.png"),
            ResourceExtensions.LoadBitmapFromResource("image_filter.png"),
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
                    string FilePath = Path.Combine(Context.Instance.LocationUrlOnStart, PidlName);
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
