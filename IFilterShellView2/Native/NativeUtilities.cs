using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IFilterShellView2.Native
{
    public static class NativeUtilities
    {
        public static BitmapSource GetIconBitmapSource(string strPath, bool bSmall)
        {
            NativeWin32.SHFILEINFO info;
            info.hIcon = IntPtr.Zero;
            info.iIcon = 0;
            info.dwAttributes = 0;
            info.szDisplayName = "";
            info.szTypeName = "";

            int cbFileInfo = Marshal.SizeOf(info);
            uint flags;

            if (bSmall)
                flags = NativeWin32.SHGFI_ICON | NativeWin32.SHGFI_SMALLICON | NativeWin32.SHGFI_USEFILEATTRIBUTES;
            else
                flags = NativeWin32.SHGFI_ICON | NativeWin32.SHGFI_LARGEICON | NativeWin32.SHGFI_USEFILEATTRIBUTES;

            NativeWin32.SHGetFileInfo(strPath, 256, out info, (uint)cbFileInfo, flags);

            IntPtr iconHandle = info.hIcon;
            
            BitmapSource img = Imaging.CreateBitmapSourceFromHIcon(
                        iconHandle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

            NativeWin32.DestroyIcon(iconHandle);
            return img;
        }

        public static bool IsAttributeOfFolder(uint dwFileAttributes)
        {
            return (dwFileAttributes & NativeWin32.FILE_ATTRIBUTE_DIRECTORY) == NativeWin32.FILE_ATTRIBUTE_DIRECTORY;
        }
    }
}
