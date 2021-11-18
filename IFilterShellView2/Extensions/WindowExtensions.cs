using IFilterShellView2.HelperClasses;
using IFilterShellView2.Native;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace IFilterShellView2.Extensions
{
    public static class WindowExtensions
    {

        public static IntPtr GetHWND(this Window window)
        {
            return new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        }

        public static WindowDPIFactor GetWindowDPIFactorClass(this Window window)
        {
            PresentationSource MainWindowPresentationSource = PresentationSource.FromVisual(window);
            Matrix m = MainWindowPresentationSource.CompositionTarget.TransformToDevice;
            
            WindowDPIFactor windowDPIFactor;
            windowDPIFactor.widthDPIFactor = m.M11;
            windowDPIFactor.heightDPIFactor = m.M22;

            return windowDPIFactor;
        }

        /// <summary>
        /// Activates a WPF window even if the window is activated on a separate thread
        /// </summary>
        /// <param name="window"></param>
        public static void ActivateWindow(Window window)
        {
            IntPtr hwnd = window.GetHWND();

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
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
