using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView.Shell.Interfaces
{

    [ComImport, Guid("1AC3D9F0-175C-11d1-95BE-00609797EA4F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface CIPersistFolder2
    {
        void GetClassID(out Guid pClassID);
        void Initialize(IntPtr pidl);
        void GetCurFolder(out IntPtr pidl);
    }

}
