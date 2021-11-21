using IFilterShellView2.Native;
using System;
using System.Diagnostics;
using System.Windows;

namespace IFilterShellView2.Program
{
    internal class WinEventProcHook
    {
        private NativeWin32.WinEventProc Fn_NativeEventCallback;
        private IntPtr HwinEventHook;

        private readonly Action Execute;
        private readonly IntPtr Hwnd;


        public WinEventProcHook(IntPtr hWnd, Action execute)
        {
            uint dwThreadId = NativeWin32.GetWindowThreadProcessId(hWnd, out uint dwProcessId);

            if (dwThreadId == 0) throw new Exception();

            Fn_NativeEventCallback = new NativeWin32.WinEventProc(NativeEventCallback);

            HwinEventHook = NativeWin32.SetWinEventHook(
                NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY,
                NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY,
                IntPtr.Zero,
                Fn_NativeEventCallback,
                dwProcessId,
                dwThreadId,
                NativeWin32.SetWinEventHookFlags.WINEVENT_OUTOFCONTEXT
                );

            if (HwinEventHook == IntPtr.Zero) throw new Exception();

            Hwnd = hWnd;
            Execute = execute;

            Debug.WriteLine("[+] Created new instance of GlobalEventHook");
        }

        public void FreeResources()
        {
            // Free Unamnaged Ressources
            if (HwinEventHook != IntPtr.Zero)
            {
                _ = NativeWin32.UnhookWinEvent(HwinEventHook);
                HwinEventHook = IntPtr.Zero;

                Debug.WriteLine("[-] Disposed instance of GlobalEventHook");
            }
        }

        private void NativeEventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, long idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            const long OBJID_WINDOW = 0;

            if (iEvent == NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY &&
                 hWnd == Hwnd && idObject == OBJID_WINDOW)
            {
                if (Execute != null) Application.Current.Dispatcher.Invoke(Execute);
            }
        }
    }
}
