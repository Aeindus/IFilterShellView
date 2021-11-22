using System;
using System.Collections.Generic;
using System.Text;

namespace IFilterShellView.Program
{
    public class CEventManager : IDisposable
    {

        private WinEventProcHook EventHook;
        private bool Disposed;

        public bool ResubscribeToExtCloseEvent(IntPtr hWnd, Action execute)
        {
            EventHook?.FreeResources();

            try
            {
                EventHook = new WinEventProcHook(hWnd, execute);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
        public void ResetSubscription()
        {
            EventHook?.FreeResources();
            EventHook = null;
        }


        private void Dispose(bool dispose)
        {
            if (Disposed) return;

            // Free Unamnaged Ressources
            EventHook?.FreeResources();
            EventHook = null;

            if (dispose)
            {
                if (this != null) GC.SuppressFinalize(this);
            }

            Disposed = true;
        }
        public void Dispose() => Dispose(true);
        ~CEventManager() => Dispose(false);
    }
}
