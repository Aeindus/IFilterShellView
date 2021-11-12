using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IFilterShellView_X.Shell.Interfaces
{


    [ComImport, Guid("000214F2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface CIEnumIDList
    {
        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult Next(uint celt, out IntPtr rgelt, out uint pceltFetched);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult Skip([In] uint celt);

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult Reset();

        [PreserveSig]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        NativeWin32.HResult Clone([MarshalAs(UnmanagedType.Interface)] out CIEnumIDList ppenum);
    }

}
