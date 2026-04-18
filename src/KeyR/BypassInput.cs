using System;
using System.Runtime.InteropServices;

namespace SupTask;

public static class BypassInput
{
	private struct INPUT
	{
		public uint type;

		public INPUTUNION u;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct INPUTUNION
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;
	}

	private struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public int mouseData;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	private struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	private const uint INPUT_MOUSE = 0u;

	private const uint INPUT_KEYBOARD = 1u;

	private const uint MOUSEEVENTF_MOVE = 1u;

	private const uint MOUSEEVENTF_LEFTDOWN = 2u;

	private const uint MOUSEEVENTF_LEFTUP = 4u;

	private const uint MOUSEEVENTF_RIGHTDOWN = 8u;

	private const uint MOUSEEVENTF_RIGHTUP = 16u;

	private const uint MOUSEEVENTF_MIDDLEDOWN = 32u;

	private const uint MOUSEEVENTF_MIDDLEUP = 64u;

	private const uint MOUSEEVENTF_XDOWN = 128u;

	private const uint MOUSEEVENTF_XUP = 256u;

	private const uint MOUSEEVENTF_WHEEL = 2048u;

	private const uint MOUSEEVENTF_HWHEEL = 4096u;

	private const uint MOUSEEVENTF_MOVE_NOCOALESCE = 8192u;

	private const uint MOUSEEVENTF_VIRTUALDESK = 16384u;

	private const uint MOUSEEVENTF_ABSOLUTE = 32768u;

	private const uint KEYEVENTF_EXTENDEDKEY = 1u;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const uint KEYEVENTF_SCANCODE = 8u;

	private const int SM_CXVIRTUALSCREEN = 78;

	private const int SM_CYVIRTUALSCREEN = 79;

	private const int SM_XVIRTUALSCREEN = 76;

	private const int SM_YVIRTUALSCREEN = 77;

	private const int XBUTTON1 = 1;

	private const int XBUTTON2 = 2;

	private static int _cachedVScreenWidth;

	private static int _cachedVScreenHeight;

	private static int _cachedVScreenX;

	private static int _cachedVScreenY;

	private static bool _screenCached;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint uCode, uint uMapType);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	public static void InvalidateScreenCache()
	{
		_screenCached = false;
	}

	private static void EnsureScreenCached()
	{
		if (!_screenCached)
		{
			_cachedVScreenWidth = GetSystemMetrics(78);
			_cachedVScreenHeight = GetSystemMetrics(79);
			_cachedVScreenX = GetSystemMetrics(76);
			_cachedVScreenY = GetSystemMetrics(77);
			if (_cachedVScreenWidth == 0)
			{
				_cachedVScreenWidth = GetSystemMetrics(0);
			}
			if (_cachedVScreenHeight == 0)
			{
				_cachedVScreenHeight = GetSystemMetrics(1);
			}
			_screenCached = true;
		}
	}

	private static bool IsExtendedKey(ushort vk)
	{
		switch (vk)
		{
		case 163:
			return true;
		case 165:
			return true;
		case 91:
		case 92:
			return true;
		case 45:
			return true;
		case 46:
			return true;
		case 36:
			return true;
		case 35:
			return true;
		case 33:
			return true;
		case 34:
			return true;
		case 37:
		case 38:
		case 39:
		case 40:
			return true;
		default:
			return vk switch
			{
				13 => false, 
				93 => true, 
				144 => true, 
				44 => true, 
				111 => true, 
				_ => false, 
			};
		}
	}

	public static void SendKey(ushort keycode, bool isDown, bool isExtended = false)
	{
		bool num = isExtended || IsExtendedKey(keycode);
		ushort wScan = (ushort)MapVirtualKey(keycode, 0u);
		uint num2 = 0u;
		if (!isDown)
		{
			num2 |= 2;
		}
		if (num)
		{
			num2 |= 1;
		}
		INPUT iNPUT = new INPUT
		{
			type = 1u,
			u = new INPUTUNION
			{
				ki = new KEYBDINPUT
				{
					wVk = keycode,
					wScan = wScan,
					dwFlags = num2,
					time = 0u,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
		SendInput(1u, new INPUT[1] { iNPUT }, Marshal.SizeOf(typeof(INPUT)));
	}

	public static void SendMouseClick(string button, bool isDown)
	{
		uint num = 0u;
		int mouseData = 0;
		switch (button)
		{
		default:
			return;
		case "Left":
			num = (isDown ? 2u : 4u);
			break;
		case "Right":
			num = (isDown ? 8u : 16u);
			break;
		case "Middle":
			num = (isDown ? 32u : 64u);
			break;
		case "XButton1":
			num = (isDown ? 128u : 256u);
			mouseData = 1;
			break;
		case "XButton2":
			num = (isDown ? 128u : 256u);
			mouseData = 2;
			break;
		}
		INPUT iNPUT = new INPUT
		{
			type = 0u,
			u = new INPUTUNION
			{
				mi = new MOUSEINPUT
				{
					dx = 0,
					dy = 0,
					mouseData = mouseData,
					dwFlags = num,
					time = 0u,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
		SendInput(1u, new INPUT[1] { iNPUT }, Marshal.SizeOf(typeof(INPUT)));
	}

	public static void SendMouseMove(int x, int y)
	{
		EnsureScreenCached();
		int dx = (int)((double)(x - _cachedVScreenX) * 65535.0 / (double)(_cachedVScreenWidth - 1)) + 1;
		int dy = (int)((double)(y - _cachedVScreenY) * 65535.0 / (double)(_cachedVScreenHeight - 1)) + 1;
		INPUT iNPUT = new INPUT
		{
			type = 0u,
			u = new INPUTUNION
			{
				mi = new MOUSEINPUT
				{
					dx = dx,
					dy = dy,
					mouseData = 0,
					dwFlags = 49153u,
					time = 0u,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
		SendInput(1u, new INPUT[1] { iNPUT }, Marshal.SizeOf(typeof(INPUT)));
	}

	public static void SendMouseWheelAt(int x, int y, int delta, bool horizontal)
	{
		SendMouseMove(x, y);
		uint dwFlags = (horizontal ? 4096u : 2048u);
		INPUT iNPUT = new INPUT
		{
			type = 0u,
			u = new INPUTUNION
			{
				mi = new MOUSEINPUT
				{
					dx = 0,
					dy = 0,
					mouseData = delta,
					dwFlags = dwFlags,
					time = 0u,
					dwExtraInfo = IntPtr.Zero
				}
			}
		};
		SendInput(1u, new INPUT[1] { iNPUT }, Marshal.SizeOf(typeof(INPUT)));
	}

	public static void ReleaseModifiers()
	{
		ushort[] array = new ushort[11]
		{
			16, 17, 18, 160, 161, 162, 163, 164, 165, 91,
			92
		};
		for (int i = 0; i < array.Length; i++)
		{
			SendKey(array[i], isDown: false);
		}
	}
}

