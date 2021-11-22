using IFilterShellView.HelperClasses;
using IFilterShellView.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace IFilterShellView.Extensions
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


        private static List<IntPtr> EnumChildWindows(IntPtr hwnd)
        {
            List<IntPtr> childHandles = new List<IntPtr>();

            GCHandle gcChildhandlesList = GCHandle.Alloc(childHandles);
            IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildhandlesList);

            try
            {
                NativeWin32.EnumWindowProc fnEnumWindow = new NativeWin32.EnumWindowProc(EnumWindow);
                NativeWin32.EnumChildWindows(hwnd, fnEnumWindow, pointerChildHandlesList);
            }
            finally
            {
                gcChildhandlesList.Free();
            }

            return childHandles;
        }

        private static bool EnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            GCHandle gcChildhandlesList = GCHandle.FromIntPtr(lParam);

            if (gcChildhandlesList == null || gcChildhandlesList.Target == null)
            {
                return false;
            }

            List<IntPtr> childHandles = gcChildhandlesList.Target as List<IntPtr>;
            childHandles.Add(hWnd);

            return true;
        }

        public static IntPtr FindChildWindowByClassName(IntPtr hwnd, string SearchClassName)
        {
            List<IntPtr> ChildWindows = EnumChildWindows(hwnd);

            if (ChildWindows == null) return IntPtr.Zero;

            StringBuilder ClassName = new StringBuilder(256);

            IntPtr HwndOfClass = ChildWindows.FirstOrDefault(hwnd =>
            {
                if (NativeWin32.GetClassName(hwnd, ClassName, ClassName.Capacity) == 0) return false;
                if (ClassName.Equals(SearchClassName)) return true;
                return false;
            });

            return HwndOfClass;
        }

    }
}
