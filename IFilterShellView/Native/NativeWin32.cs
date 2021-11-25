using IFilterShellView.Shell.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView.Native
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

		[Serializable, StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;        // x position of upper-left corner
			public int Top;         // y position of upper-left corner
			public int Right;       // x position of lower-right corner
			public int Bottom;      // y position of lower-right corner

			public Rectangle ToRectangle()
			{
				return Rectangle.FromLTRB(Left, Top, Right, Bottom);
			}
		}


		[DllImport("user32.dll")]
		public static extern bool SetProcessDPIAware();

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		public static uint SWP_NOSIZE = 0x0001;
		public static uint SWP_NOZORDER = 0x0004;
		public static uint SWP_NOMOVE = 0x0002;
		public static uint SWP_NOACTIVATE = 0x0010;


		[DllImport("user32.dll")]
		public static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);



		public const Int32 MONITOR_DEFAULTTOPRIMARY = 0x00000001;
		public const Int32 MONITOR_DEFAULTTONEAREST = 0x00000002;

		[DllImport("user32.dll")]
		public static extern IntPtr MonitorFromWindow(IntPtr handle, Int32 flags);


		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

		// size of a device name string
		private const int CCHDEVICENAME = 32;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct MONITORINFOEX
		{
			public int Size;
			public RECT Monitor;
			public RECT WorkArea;
			public uint Flags;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
			public string DeviceName;
			public void Init()
			{
				this.Size = 40 + 2 * CCHDEVICENAME;
				this.DeviceName = string.Empty;
			}
		}



		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnableWindow(IntPtr hWnd, bool bEnable);



		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

		[StructLayout(LayoutKind.Sequential)]
		public struct SHELLEXECUTEINFO
		{
			public int cbSize;
			public uint fMask;
			public IntPtr hwnd;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpVerb;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpFile;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpParameters;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpDirectory;
			public int nShow;
			public IntPtr hInstApp;
			public IntPtr lpIDList;
			[MarshalAs(UnmanagedType.LPTStr)]
			public string lpClass;
			public IntPtr hkeyClass;
			public uint dwHotKey;
			public IntPtr hIcon;
			public IntPtr hProcess;
		}

		public enum ShowCommands : int
		{
			SW_HIDE = 0,
			SW_SHOWNORMAL = 1,
			SW_NORMAL = 1,
			SW_SHOWMINIMIZED = 2,
			SW_SHOWMAXIMIZED = 3,
			SW_MAXIMIZE = 3,
			SW_SHOWNOACTIVATE = 4,
			SW_SHOW = 5,
			SW_MINIMIZE = 6,
			SW_SHOWMINNOACTIVE = 7,
			SW_SHOWNA = 8,
			SW_RESTORE = 9,
			SW_SHOWDEFAULT = 10,
			SW_FORCEMINIMIZE = 11,
			SW_MAX = 11
		}

		[Flags]
		public enum ShellExecuteMaskFlags : uint
		{
			SEE_MASK_DEFAULT = 0x00000000,
			SEE_MASK_CLASSNAME = 0x00000001,
			SEE_MASK_CLASSKEY = 0x00000003,
			SEE_MASK_IDLIST = 0x00000004,
			SEE_MASK_INVOKEIDLIST = 0x0000000c,   // Note SEE_MASK_INVOKEIDLIST(0xC) implies SEE_MASK_IDLIST(0x04)
			SEE_MASK_HOTKEY = 0x00000020,
			SEE_MASK_NOCLOSEPROCESS = 0x00000040,
			SEE_MASK_CONNECTNETDRV = 0x00000080,
			SEE_MASK_NOASYNC = 0x00000100,
			SEE_MASK_FLAG_DDEWAIT = SEE_MASK_NOASYNC,
			SEE_MASK_DOENVSUBST = 0x00000200,
			SEE_MASK_FLAG_NO_UI = 0x00000400,
			SEE_MASK_UNICODE = 0x00004000,
			SEE_MASK_NO_CONSOLE = 0x00008000,
			SEE_MASK_ASYNCOK = 0x00100000,
			SEE_MASK_HMONITOR = 0x00200000,
			SEE_MASK_NOZONECHECKS = 0x00800000,
			SEE_MASK_NOQUERYCLASSSTORE = 0x01000000,
			SEE_MASK_WAITFORINPUTIDLE = 0x02000000,
			SEE_MASK_FLAG_LOG_USAGE = 0x04000000,
		}



		#region SH_File 
		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool DestroyIcon(IntPtr hIcon);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHFILEINFO
		{
			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};
		public enum FolderType
		{
			Closed,
			Open
		}
		public enum IconSize
		{
			Large,
			Small
		}
		public const uint SHGFI_ICON = 0x000000100;
		public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
		public const uint SHGFI_OPENICON = 0x000000002;
		public const uint SHGFI_SMALLICON = 0x000000001;
		public const uint SHGFI_LARGEICON = 0x000000000;
		public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
		#endregion





		public delegate bool EnumWindowProc(IntPtr hwnd, IntPtr lParam);

		[DllImport("user32")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr lParam);


		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);


		[DllImport("user32.dll", SetLastError = true)]
		public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

		[DllImport("user32.dll")]
		public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);



        #region SendInput area - used to bypass SetForegroundWindow restrictions
        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
		public struct INPUT
		{
			internal uint type;
			internal InputUnion U;
			internal static int Size
			{
				get => Marshal.SizeOf(typeof(INPUT));
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct InputUnion
		{
			[FieldOffset(0)]
			internal MOUSEINPUT mi;
			[FieldOffset(0)]
			internal KEYBDINPUT ki;
			[FieldOffset(0)]
			internal HARDWAREINPUT hi;
		}
		
        [StructLayout(LayoutKind.Sequential)]
		internal struct MOUSEINPUT
		{
			internal int dx;
			internal int dy;
			internal int mouseData;
			internal MOUSEEVENTF dwFlags;
			internal uint time;
			internal UIntPtr dwExtraInfo;
		}
		
        [Flags]
		internal enum MOUSEEVENTF : uint
		{
			ABSOLUTE = 0x8000,
			HWHEEL = 0x01000,
			MOVE = 0x0001,
			MOVE_NOCOALESCE = 0x2000,
			LEFTDOWN = 0x0002,
			LEFTUP = 0x0004,
			RIGHTDOWN = 0x0008,
			RIGHTUP = 0x0010,
			MIDDLEDOWN = 0x0020,
			MIDDLEUP = 0x0040,
			VIRTUALDESK = 0x4000,
			WHEEL = 0x0800,
			XDOWN = 0x0080,
			XUP = 0x0100
		}
		
        [StructLayout(LayoutKind.Sequential)]
		internal struct KEYBDINPUT
		{
			internal VirtualKeyShort wVk;
			internal ScanCodeShort wScan;
			internal KEYEVENTF dwFlags;
			internal int time;
			internal UIntPtr dwExtraInfo;
		}
		
        [Flags]
		internal enum KEYEVENTF : uint
		{
			EXTENDEDKEY = 0x0001,
			KEYUP = 0x0002,
			SCANCODE = 0x0008,
			UNICODE = 0x0004
		}

		internal enum VirtualKeyShort : short
		{
			LBUTTON = 0x01,
			RBUTTON = 0x02,
			CANCEL = 0x03,
			MBUTTON = 0x04,
			XBUTTON1 = 0x05,
			XBUTTON2 = 0x06,
			BACK = 0x08,
			TAB = 0x09,
			CLEAR = 0x0C,
			RETURN = 0x0D,
			SHIFT = 0x10,
			CONTROL = 0x11,
			MENU = 0x12,
			PAUSE = 0x13,
			CAPITAL = 0x14,
			KANA = 0x15,
			HANGUL = 0x15,
			JUNJA = 0x17,
			FINAL = 0x18,
			HANJA = 0x19,
			KANJI = 0x19,
			ESCAPE = 0x1B,
			CONVERT = 0x1C,
			NONCONVERT = 0x1D,
			ACCEPT = 0x1E,
			MODECHANGE = 0x1F,
			SPACE = 0x20,
			PRIOR = 0x21,
			NEXT = 0x22,
			END = 0x23,
			HOME = 0x24,
			LEFT = 0x25,
			UP = 0x26,
			RIGHT = 0x27,
			DOWN = 0x28,
			SELECT = 0x29,
			PRINT = 0x2A,
			EXECUTE = 0x2B,
			SNAPSHOT = 0x2C,
			INSERT = 0x2D,
			DELETE = 0x2E,
			HELP = 0x2F,
			KEY_0 = 0x30,
			KEY_1 = 0x31,
			KEY_2 = 0x32,
			KEY_3 = 0x33,
			KEY_4 = 0x34,
			KEY_5 = 0x35,
			KEY_6 = 0x36,
			KEY_7 = 0x37,
			KEY_8 = 0x38,
			KEY_9 = 0x39,
			KEY_A = 0x41,
			KEY_B = 0x42,
			KEY_C = 0x43,
			KEY_D = 0x44,
			KEY_E = 0x45,
			KEY_F = 0x46,
			KEY_G = 0x47,
			KEY_H = 0x48,
			KEY_I = 0x49,
			KEY_J = 0x4A,
			KEY_K = 0x4B,
			KEY_L = 0x4C,
			KEY_M = 0x4D,
			KEY_N = 0x4E,
			KEY_O = 0x4F,
			KEY_P = 0x50,
			KEY_Q = 0x51,
			KEY_R = 0x52,
			KEY_S = 0x53,
			KEY_T = 0x54,
			KEY_U = 0x55,
			KEY_V = 0x56,
			KEY_W = 0x57,
			KEY_X = 0x58,
			KEY_Y = 0x59,
			KEY_Z = 0x5A,
			LWIN = 0x5B,
			RWIN = 0x5C,
			APPS = 0x5D,
			SLEEP = 0x5F,
			NUMPAD0 = 0x60,
			NUMPAD1 = 0x61,
			NUMPAD2 = 0x62,
			NUMPAD3 = 0x63,
			NUMPAD4 = 0x64,
			NUMPAD5 = 0x65,
			NUMPAD6 = 0x66,
			NUMPAD7 = 0x67,
			NUMPAD8 = 0x68,
			NUMPAD9 = 0x69,
			MULTIPLY = 0x6A,
			ADD = 0x6B,
			SEPARATOR = 0x6C,
			SUBTRACT = 0x6D,
			DECIMAL = 0x6E,
			DIVIDE = 0x6F,
			F1 = 0x70,
			F2 = 0x71,
			F3 = 0x72,
			F4 = 0x73,
			F5 = 0x74,
			F6 = 0x75,
			F7 = 0x76,
			F8 = 0x77,
			F9 = 0x78,
			F10 = 0x79,
			F11 = 0x7A,
			F12 = 0x7B,
			F13 = 0x7C,
			F14 = 0x7D,
			F15 = 0x7E,
			F16 = 0x7F,
			F17 = 0x80,
			F18 = 0x81,
			F19 = 0x82,
			F20 = 0x83,
			F21 = 0x84,
			F22 = 0x85,
			F23 = 0x86,
			F24 = 0x87,
			NUMLOCK = 0x90,
			SCROLL = 0x91,
			LSHIFT = 0xA0,
			RSHIFT = 0xA1,
			LCONTROL = 0xA2,
			RCONTROL = 0xA3,
			LMENU = 0xA4,
			RMENU = 0xA5,
			BROWSER_BACK = 0xA6,
			BROWSER_FORWARD = 0xA7,
			BROWSER_REFRESH = 0xA8,
			BROWSER_STOP = 0xA9,
			BROWSER_SEARCH = 0xAA,
			BROWSER_FAVORITES = 0xAB,
			BROWSER_HOME = 0xAC,
			VOLUME_MUTE = 0xAD,
			VOLUME_DOWN = 0xAE,
			VOLUME_UP = 0xAF,
			MEDIA_NEXT_TRACK = 0xB0,
			MEDIA_PREV_TRACK = 0xB1,
			MEDIA_STOP = 0xB2,
			MEDIA_PLAY_PAUSE = 0xB3,
			LAUNCH_MAIL = 0xB4,
			LAUNCH_MEDIA_SELECT = 0xB5,
			LAUNCH_APP1 = 0xB6,
			LAUNCH_APP2 = 0xB7,
			OEM_1 = 0xBA,
			OEM_PLUS = 0xBB,
			OEM_COMMA = 0xBC,
			OEM_MINUS = 0xBD,
			OEM_PERIOD = 0xBE,
			OEM_2 = 0xBF,
			OEM_3 = 0xC0,
			OEM_4 = 0xDB,
			OEM_5 = 0xDC,
			OEM_6 = 0xDD,
			OEM_7 = 0xDE,
			OEM_8 = 0xDF,
			OEM_102 = 0xE2,
			PROCESSKEY = 0xE5,
			PACKET = 0xE7,
			ATTN = 0xF6,
			CRSEL = 0xF7,
			EXSEL = 0xF8,
			EREOF = 0xF9,
			PLAY = 0xFA,
			ZOOM = 0xFB,
			NONAME = 0xFC,
			PA1 = 0xFD,
			OEM_CLEAR = 0xFE
		}
		internal enum ScanCodeShort : short
		{
			LBUTTON = 0,
			RBUTTON = 0,
			CANCEL = 70,
			MBUTTON = 0,
			XBUTTON1 = 0,
			XBUTTON2 = 0,
			BACK = 14,
			TAB = 15,
			CLEAR = 76,
			RETURN = 28,
			SHIFT = 42,
			CONTROL = 29,
			MENU = 56,
			PAUSE = 0,
			CAPITAL = 58,
			KANA = 0,
			HANGUL = 0,
			JUNJA = 0,
			FINAL = 0,
			HANJA = 0,
			KANJI = 0,
			ESCAPE = 1,
			CONVERT = 0,
			NONCONVERT = 0,
			ACCEPT = 0,
			MODECHANGE = 0,
			SPACE = 57,
			PRIOR = 73,
			NEXT = 81,
			END = 79,
			HOME = 71,
			LEFT = 75,
			UP = 72,
			RIGHT = 77,
			DOWN = 80,
			SELECT = 0,
			PRINT = 0,
			EXECUTE = 0,
			SNAPSHOT = 84,
			INSERT = 82,
			DELETE = 83,
			HELP = 99,
			KEY_0 = 11,
			KEY_1 = 2,
			KEY_2 = 3,
			KEY_3 = 4,
			KEY_4 = 5,
			KEY_5 = 6,
			KEY_6 = 7,
			KEY_7 = 8,
			KEY_8 = 9,
			KEY_9 = 10,
			KEY_A = 30,
			KEY_B = 48,
			KEY_C = 46,
			KEY_D = 32,
			KEY_E = 18,
			KEY_F = 33,
			KEY_G = 34,
			KEY_H = 35,
			KEY_I = 23,
			KEY_J = 36,
			KEY_K = 37,
			KEY_L = 38,
			KEY_M = 50,
			KEY_N = 49,
			KEY_O = 24,
			KEY_P = 25,
			KEY_Q = 16,
			KEY_R = 19,
			KEY_S = 31,
			KEY_T = 20,
			KEY_U = 22,
			KEY_V = 47,
			KEY_W = 17,
			KEY_X = 45,
			KEY_Y = 21,
			KEY_Z = 44,
			LWIN = 91,
			RWIN = 92,
			APPS = 93,
			SLEEP = 95,
			NUMPAD0 = 82,
			NUMPAD1 = 79,
			NUMPAD2 = 80,
			NUMPAD3 = 81,
			NUMPAD4 = 75,
			NUMPAD5 = 76,
			NUMPAD6 = 77,
			NUMPAD7 = 71,
			NUMPAD8 = 72,
			NUMPAD9 = 73,
			MULTIPLY = 55,
			ADD = 78,
			SEPARATOR = 0,
			SUBTRACT = 74,
			DECIMAL = 83,
			DIVIDE = 53,
			F1 = 59,
			F2 = 60,
			F3 = 61,
			F4 = 62,
			F5 = 63,
			F6 = 64,
			F7 = 65,
			F8 = 66,
			F9 = 67,
			F10 = 68,
			F11 = 87,
			F12 = 88,
			F13 = 100,
			F14 = 101,
			F15 = 102,
			F16 = 103,
			F17 = 104,
			F18 = 105,
			F19 = 106,
			F20 = 107,
			F21 = 108,
			F22 = 109,
			F23 = 110,
			F24 = 118,
			NUMLOCK = 69,
			SCROLL = 70,
			LSHIFT = 42,
			RSHIFT = 54,
			LCONTROL = 29,
			RCONTROL = 29,
			LMENU = 56,
			RMENU = 56,
			BROWSER_BACK = 106,
			BROWSER_FORWARD = 105,
			BROWSER_REFRESH = 103,
			BROWSER_STOP = 104,
			BROWSER_SEARCH = 101,
			BROWSER_FAVORITES = 102,
			BROWSER_HOME = 50,
			VOLUME_MUTE = 32,
			VOLUME_DOWN = 46,
			VOLUME_UP = 48,
			MEDIA_NEXT_TRACK = 25,
			MEDIA_PREV_TRACK = 16,
			MEDIA_STOP = 36,
			MEDIA_PLAY_PAUSE = 34,
			LAUNCH_MAIL = 108,
			LAUNCH_MEDIA_SELECT = 109,
			LAUNCH_APP1 = 107,
			LAUNCH_APP2 = 33,
			OEM_1 = 39,
			OEM_PLUS = 13,
			OEM_COMMA = 51,
			OEM_MINUS = 12,
			OEM_PERIOD = 52,
			OEM_2 = 53,
			OEM_3 = 41,
			OEM_4 = 26,
			OEM_5 = 43,
			OEM_6 = 27,
			OEM_7 = 40,
			OEM_8 = 0,
			OEM_102 = 86,
			PROCESSKEY = 0,
			PACKET = 0,
			ATTN = 0,
			CRSEL = 0,
			EXSEL = 0,
			EREOF = 93,
			PLAY = 0,
			ZOOM = 98,
			NONAME = 0,
			PA1 = 0,
			OEM_CLEAR = 0,
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct HARDWAREINPUT
		{
			internal int uMsg;
			internal short wParamL;
			internal short wParamH;
		}
		#endregion



		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct WINDOWPLACEMENT
		{
			public int Length;

			public int Flags;

			public ShowState ShowCmd;

			public NativePoint MinPosition;

			public NativePoint MaxPosition;

			public RECT NormalPosition;

			public static WINDOWPLACEMENT Default
			{
				get
				{
					WINDOWPLACEMENT result = new WINDOWPLACEMENT();
					result.Length = Marshal.SizeOf(result);
					return result;
				}
			}
		}
		public enum ShowState : int
		{
			SW_HIDE = 0,
			SW_SHOWNORMAL = 1,
			SW_NORMAL = 1,
			SW_SHOWMINIMIZED = 2,
			SW_SHOWMAXIMIZED = 3,
			SW_MAXIMIZE = 3,
			SW_SHOWNOACTIVATE = 4,
			SW_SHOW = 5,
			SW_MINIMIZE = 6,
			SW_SHOWMINNOACTIVE = 7,
			SW_SHOWNA = 8,
			SW_RESTORE = 9,
			SW_SHOWDEFAULT = 10,
			SW_FORCEMINIMIZE = 11,
			SW_MAX = 11
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
