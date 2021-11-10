using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace IFilterShellView.Extensions
{
    public static class WindowExtensions
    {
        /// <summary>
        /// Activates a WPF window even if the window is activated on a separate thread
        /// </summary>
        /// <param name="window"></param>
        public static void ActivateWindow(Window window)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();

            uint threadId1 = GetWindowThreadProcessId(NativeWin32.GetForegroundWindow(), IntPtr.Zero);
            uint threadId2 = GetWindowThreadProcessId(hwnd, IntPtr.Zero);

            if (threadId1 != threadId2)
            {
                NativeWin32.AttachThreadInput(threadId1, threadId2, true);
                NativeWin32.SetForegroundWindow(hwnd);
                NativeWin32.AttachThreadInput(threadId1, threadId2, false);
            }
            else NativeWin32.SetForegroundWindow(hwnd);
        }


        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);
    }
}
