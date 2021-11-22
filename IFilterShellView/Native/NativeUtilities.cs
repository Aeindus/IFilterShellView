using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IFilterShellView.Native
{
    public static class NativeUtilities
    {
        public static BitmapSource GetIconBitmapSource(string strPath, bool bSmall)
        {
            NativeWin32.SHFILEINFO fileInfo;
            fileInfo.hIcon = IntPtr.Zero;
            fileInfo.iIcon = 0;
            fileInfo.dwAttributes = 0;
            fileInfo.szDisplayName = "";
            fileInfo.szTypeName = "";

            int sizeOfFileInfo = Marshal.SizeOf(fileInfo);
            uint flags = NativeWin32.SHGFI_ICON | NativeWin32.SHGFI_USEFILEATTRIBUTES;

            if (bSmall)
                flags |= NativeWin32.SHGFI_SMALLICON;
            else
                flags |= NativeWin32.SHGFI_LARGEICON;

            NativeWin32.SHGetFileInfo(strPath, 256, out fileInfo, (uint)sizeOfFileInfo, flags);
            BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        fileInfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
            NativeWin32.DestroyIcon(fileInfo.hIcon);
            bitmapSource.Freeze();

            return bitmapSource;
        }

        public static void ShowFileProperties(string Filename)
        {
            NativeWin32.SHELLEXECUTEINFO info = new NativeWin32.SHELLEXECUTEINFO();
            info.cbSize = Marshal.SizeOf(info);
            info.lpVerb = "properties";
            info.lpFile = Filename;
            info.nShow = (int)NativeWin32.ShowCommands.SW_SHOW;
            info.fMask = (int)NativeWin32.ShellExecuteMaskFlags.SEE_MASK_INVOKEIDLIST;
            NativeWin32.ShellExecuteEx(ref info);
        }

        public static bool IsAttributeOfFolder(uint dwFileAttributes)
        {
            return (dwFileAttributes & NativeWin32.FILE_ATTRIBUTE_DIRECTORY) == NativeWin32.FILE_ATTRIBUTE_DIRECTORY;
        }
    }
}
