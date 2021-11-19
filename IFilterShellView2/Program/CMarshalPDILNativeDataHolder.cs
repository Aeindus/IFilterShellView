using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView2.Program
{
    public class CMarshalPDILNativeDataHolder : IDisposable
    {
        public int MAX_PATH = 256;

        public IntPtr STRRET;
        public StringBuilder BUFFER;

        private bool Disposed;

        public CMarshalPDILNativeDataHolder()
        {
            STRRET = Marshal.AllocCoTaskMem(MAX_PATH * 2 + 4);
            BUFFER = new StringBuilder(MAX_PATH);

            if (STRRET == null) throw new Exception();
        }

        private void Dispose(bool dispose)
        {
            if (Disposed) return;

            // Free Unamnaged Ressources
            if (STRRET != null)
                Marshal.FreeCoTaskMem(STRRET);

            if (dispose)
            {
                if (this != null) GC.SuppressFinalize(this);
            }

            Disposed = true;
        }

        public void Dispose() => Dispose(true);
        ~CMarshalPDILNativeDataHolder() => Dispose(false);
    }
}
