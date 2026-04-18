using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SupTask;

public static class BypassInput
{
	[Flags]
	public enum KeyEventF : uint
	{
		KeyDown = 0u,
		ExtendedKey = 1u,
		KeyUp = 2u,
		Unicode = 4u,
		Scancode = 8u
	}

	[Flags]
	public enum MouseEventF : uint
	{
		Absolute = 0x8000u,
		HWheel = 0x1000u,
		Move = 1u,
		MoveNoCoalesce = 0x2000u,
		LeftDown = 2u,
		LeftUp = 4u,
		RightDown = 8u,
		RightUp = 0x10u,
		MiddleDown = 0x20u,
		MiddleUp = 0x40u,
		VirtualDesk = 0x4000u,
		Wheel = 0x800u,
		XDown = 0x80u,
		XUp = 0x100u
	}

	private static double _cachedScreenWidth;

	private static double _cachedScreenHeight;

	private static bool _screenCached;

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint uCode, uint uMapType);

	public static void InvalidateScreenCache()
	{
		_screenCached = false;
	}

	private static void EnsureScreenCached()
	{
		if (!_screenCached)
		{
			_cachedScreenWidth = SystemParameters.PrimaryScreenWidth;
			_cachedScreenHeight = SystemParameters.PrimaryScreenHeight;
			_screenCached = true;
		}
	}

	public static void SendKey(ushort keycode, bool isDown)
	{
		byte bScan = (byte)MapVirtualKey(keycode, 0u);
		uint num = 0u;
		if (!isDown)
		{
			num |= 2;
		}
		keybd_event((byte)keycode, bScan, num, 0);
	}

	public static void SendMouseClick(string button, bool isDown)
	{
		uint num = 0u;
		if (button.Contains("Left"))
		{
			num = (isDown ? 2u : 4u);
		}
		else if (button.Contains("Right"))
		{
			num = (isDown ? 8u : 16u);
		}
		else if (button.Contains("Middle"))
		{
			num = (isDown ? 32u : 64u);
		}
		if (num != 0)
		{
			mouse_event(num, 0, 0, 0u, 0);
		}
	}

	public static void SendMouseMove(int x, int y)
	{
		EnsureScreenCached();
		int dx = (int)((double)(x * 65536) / _cachedScreenWidth) + 1;
		int dy = (int)((double)(y * 65536) / _cachedScreenHeight) + 1;
		mouse_event(32769u, dx, dy, 0u, 0);
	}

	public static void SendMouseWheelAt(int x, int y, int delta, bool horizontal)
	{
		SendMouseMove(x, y);
		mouse_event(horizontal ? 4096u : 2048u, 0, 0, (uint)delta, 0);
	}

	public static void ReleaseModifiers()
	{
		ushort[] array = new ushort[5] { 17, 16, 18, 91, 92 };
		for (int i = 0; i < array.Length; i++)
		{
			SendKey(array[i], isDown: false);
		}
	}
}

