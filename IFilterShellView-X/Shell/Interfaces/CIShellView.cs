using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IFilterShellView_X.Shell.Interfaces
{

    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface CIShellView
    {
        // IOleWindow
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult GetWindow(
            out IntPtr phwnd);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult ContextSensitiveHelp(
            bool fEnterMode);

        // IShellView
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult TranslateAccelerator(
            IntPtr pmsg);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult EnableModeless(
            bool fEnable);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult UIActivate(
            uint uState);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult Refresh();

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult CreateViewWindow(
            [MarshalAs(UnmanagedType.IUnknown)] object psvPrevious,
            IntPtr pfs,
            [MarshalAs(UnmanagedType.IUnknown)] object psb,
            IntPtr prcView,
            out IntPtr phWnd);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult DestroyViewWindow();

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult GetCurrentInfo(
            out IntPtr pfs);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult AddPropertySheetPages(
            uint dwReserved,
            IntPtr pfn,
            uint lparam);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult SaveViewState();

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult SelectItem(
            IntPtr pidlItem,
            uint uFlags);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult GetItemObject(
            ShellViewGetItemObject uItem,
            ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }


    public enum ShellViewGetItemObject
    {
        Background = 0x00000000,
        Selection = 0x00000001,
        AllView = 0x00000002,
        Checked = 0x00000003,
        TypeMask = 0x0000000F,
        ViewOrderFlag = unchecked((int)0x80000000)
    }

    public static class SVSI
    {
        public static uint SVSI_DESELECTOTHERS = 0x00000004;
        public static uint SVSI_DESELECT = 0;
        public static uint SVSI_SELECT = 1;
        public static uint SVSI_ENSUREVISIBLE = 8;
        public static uint SVSI_FOCUSED = 16;
    }
}
