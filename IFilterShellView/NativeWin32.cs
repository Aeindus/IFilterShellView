using IFilterShellView.Shell.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView
{
    public static class NativeWin32
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate void WinEventProc(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, long idObject, long idChild, uint dwEventThread, uint dwmsEventTime);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);


        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);


        public static class SetWinEventHookEvents
        {
            public static uint EVENT_OBJECT_CREATE = 0x8000;
            public static uint EVENT_OBJECT_DESTROY = 0x8001;
            public static uint EVENT_OBJECT_SHOW = 0x8002;
            public static uint EVENT_OBJECT_HIDE = 0x8003;
            public static uint EVENT_OBJECT_REORDER = 0x8004;
        }
        public static class SetWinEventHookFlags
        {
            public static uint WINEVENT_INCONTEXT = 4;
            public static uint WINEVENT_OUTOFCONTEXT = 0;
            public static uint WINEVENT_SKIPOWNPROCESS = 2;
            public static uint WINEVENT_SKIPOWNTHREAD = 1;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwflags);
        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);


        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [StructLayout(LayoutKind.Explicit, Size = 264)]
        public struct STRRET
        {
            [FieldOffset(0)]
            public UInt32 uType;
            [FieldOffset(4)]
            public IntPtr pOleStr;
            [FieldOffset(4)]
            public IntPtr pStr;
            [FieldOffset(4)]
            public UInt32 uOffset;
            [FieldOffset(4)]
            public IntPtr cStr;
        }

        //[DllImport("shlwapi.dll")]
        //public static extern Int32 StrRetToBuf(ref STRRET pstr, IntPtr pidl,
        //                               StringBuilder pszBuf,
        //                               UInt32 cchBuf);


        [DllImport("Shlwapi.Dll", CharSet = CharSet.Auto)]
        public static extern Int32 StrRetToBuf(IntPtr pstr, IntPtr pidl, StringBuilder pszBuf, int cchBuf);



        public static int SHGDFIL_FINDDATA = 1;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }
        public static readonly int WIN32_FIND_DATA_SIZE = 592;

        [DllImport("Shell32.Dll", CharSet = CharSet.Auto)]
        public static extern HResult SHGetDataFromIDListW(CIShellFolder pIShellFolder, IntPtr pidl, int nFormat, out WIN32_FIND_DATA ppobj, int cb);

        [DllImport("shell32.dll")]
        public static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out()] out IntPtr pidl, uint sfgaoIn, [Out()] out uint psfgaoOut);



        /// <summary>
        /// Nice article on this https://social.msdn.microsoft.com/Forums/vstudio/en-US/9ff7c1bd-354d-495f-bf07-b58ccf458774/convert-a-file-name-to-a-shell-idlist?forum=clr
        /// !!! I must clean the pidl after I am done to avoid any leaks
        /// </summary>
        public static IntPtr ConvertItemNameToPidl(string ItemName)
        {
            IntPtr pidl = IntPtr.Zero;
            uint psfgaoOut;

            SHParseDisplayName(ItemName, IntPtr.Zero, out pidl, 0, out psfgaoOut);
            return pidl;
        }



        public enum HResult
        {
            Ok = 0x0000,
            False = 0x0001,
            InvalidArguments = unchecked((int)0x80070057),
            OutOfMemory = unchecked((int)0x8007000E),
            NoInterface = unchecked((int)0x80004002),
            Fail = unchecked((int)0x80004005),
            ElementNotFound = unchecked((int)0x80070490),
            TypeElementNotFound = unchecked((int)0x8002802B),
            NoObject = unchecked((int)0x800401E5),
            Win32ErrorCanceled = 1223,
            Canceled = unchecked((int)0x800704C7),
            ResourceInUse = unchecked((int)0x800700AA),
            AccessDenied = unchecked((int)0x80030005)
        }


        public static List<Tuple<uint, string>> FileAttributesListTupple = new List<Tuple<uint, string>>()
        {
            new Tuple<uint, string>(0x20, "FILE_ATTRIBUTE_ARCHIVE"),
            new Tuple<uint, string>(0x800, "FILE_ATTRIBUTE_COMPRESSED"),
            new Tuple<uint, string>(0x40, "FILE_ATTRIBUTE_DEVICE"),
            new Tuple<uint, string>(0x10, "FILE_ATTRIBUTE_DIRECTORY"),
            new Tuple<uint, string>(0x4000, "FILE_ATTRIBUTE_ENCRYPTED"),
            new Tuple<uint, string>(0x2, "FILE_ATTRIBUTE_HIDDEN"),
            new Tuple<uint, string>(0x8000, "FILE_ATTRIBUTE_uintEGRITY_STREAM"),
            new Tuple<uint, string>(0x80, "FILE_ATTRIBUTE_NORMAL"),
            new Tuple<uint, string>(0x2000, "FILE_ATTRIBUTE_NOT_CONTENT_INDEXED"),
            new Tuple<uint, string>(0x20000, "FILE_ATTRIBUTE_NO_SCRUB_DATA"),
            new Tuple<uint, string>(0x1000, "FILE_ATTRIBUTE_OFFLINE"),
            new Tuple<uint, string>(0x1, "FILE_ATTRIBUTE_READONLY"),
            new Tuple<uint, string>(0x400000, "FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS"),
            new Tuple<uint, string>(0x40000, "FILE_ATTRIBUTE_RECALL_ON_OPEN"),
            new Tuple<uint, string>(0x400, "FILE_ATTRIBUTE_REPARSE_POuint"),
            new Tuple<uint, string>(0x200, "FILE_ATTRIBUTE_SPARSE_FILE"),
            new Tuple<uint, string>(0x4, "FILE_ATTRIBUTE_SYSTEM"),
            new Tuple<uint, string>(0x100, "FILE_ATTRIBUTE_TEMPORARY"),
            new Tuple<uint, string>(0x10000, "FILE_ATTRIBUTE_VIRTUAL")
        };

    }
}
