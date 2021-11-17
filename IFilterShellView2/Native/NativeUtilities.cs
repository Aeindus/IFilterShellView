using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IFilterShellView2.Native
{
    public static class NativeUtilities
    {
        public static BitmapImage GetIconBitmapSource(string strPath, bool bSmall)
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


            // JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            MemoryStream memoryStream = new MemoryStream();
            BitmapImage bitmapImage = new BitmapImage();

            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);
            memoryStream.Position = 0;

            bitmapImage.BeginInit();
            //bitmapImage.DecodePixelWidth = 250;
            //bitmapImage.DecodePixelHeight = 250;
            bitmapImage.CacheOption = BitmapCacheOption.None;
            bitmapImage.StreamSource = new MemoryStream(memoryStream.ToArray());
            bitmapImage.EndInit();

            memoryStream.Close();
            
            // Prevent leaks and optimize for thread calls
            bitmapImage.Freeze();

            return bitmapImage;
        }

        public static bool IsAttributeOfFolder(uint dwFileAttributes)
        {
            return (dwFileAttributes & NativeWin32.FILE_ATTRIBUTE_DIRECTORY) == NativeWin32.FILE_ATTRIBUTE_DIRECTORY;
        }
    }
}
