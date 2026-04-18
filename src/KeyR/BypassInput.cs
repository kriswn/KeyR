using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SupTask;

public static class BypassInput
{
	[Flags]
	public enum InputType
	{
		INPUT_MOUSE = 0,
		INPUT_KEYBOARD = 1,
		INPUT_HARDWARE = 2
	}

	[Flags]
	public enum KeyEventF
	{
		KeyDown = 0,
		ExtendedKey = 1,
		KeyUp = 2,
		Unicode = 4,
		Scancode = 8
	}

	[Flags]
	public enum MouseEventF
	{
		Absolute = 0x8000,
		HWheel = 0x1000,
		Move = 1,
		MoveNoCoalesce = 0x2000,
		LeftDown = 2,
		LeftUp = 4,
		RightDown = 8,
		RightUp = 0x10,
		MiddleDown = 0x20,
		MiddleUp = 0x40,
		VirtualDesk = 0x4000,
		Wheel = 0x800,
		XDown = 0x80,
		XUp = 0x100
	}

	public struct HARDWAREINPUT
	{
		public uint uMsg;

		public ushort wParamL;

		public ushort wParamH;
	}

	public struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	public struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public uint mouseData;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct MOUSEKEYBDHARDWAREINPUT
	{
		[FieldOffset(0)]
		public HARDWAREINPUT hi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;

		[FieldOffset(0)]
		public MOUSEINPUT mi;
	}

	public struct INPUT
	{
		public uint type;

		public MOUSEKEYBDHARDWAREINPUT mkhi;
	}

	[DllImport("user32.dll")]
	private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint uCode, uint uMapType);

	public static void SendKey(ushort keycode, bool isDown)
	{
		ushort wScan = (ushort)MapVirtualKey(keycode, 0u);
		INPUT[] array = new INPUT[1];
		array[0].type = 1u;
		array[0].mkhi.ki.wVk = 0;
		array[0].mkhi.ki.wScan = wScan;
		array[0].mkhi.ki.time = 0u;
		array[0].mkhi.ki.dwExtraInfo = IntPtr.Zero;
		uint num = 8u;
		if (!isDown)
		{
			num |= 2;
		}
		array[0].mkhi.ki.dwFlags = num;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
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
			INPUT[] array = new INPUT[1];
			array[0].type = 0u;
			array[0].mkhi.mi.dx = 0;
			array[0].mkhi.mi.dy = 0;
			array[0].mkhi.mi.mouseData = 0u;
			array[0].mkhi.mi.time = 0u;
			array[0].mkhi.mi.dwFlags = num;
			array[0].mkhi.mi.dwExtraInfo = IntPtr.Zero;
			SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
		}
	}

	public static void SendMouseMove(int x, int y)
	{
		double primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
		double primaryScreenHeight = SystemParameters.PrimaryScreenHeight;
		int dx = (int)((double)(x * 65536) / primaryScreenWidth) + 1;
		int dy = (int)((double)(y * 65536) / primaryScreenHeight) + 1;
		INPUT[] array = new INPUT[1];
		array[0].type = 0u;
		array[0].mkhi.mi.dx = dx;
		array[0].mkhi.mi.dy = dy;
		array[0].mkhi.mi.mouseData = 0u;
		array[0].mkhi.mi.time = 0u;
		array[0].mkhi.mi.dwFlags = 32769u;
		array[0].mkhi.mi.dwExtraInfo = IntPtr.Zero;
		SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
	}
}

