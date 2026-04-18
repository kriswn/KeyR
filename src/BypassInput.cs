using System;
using System.Runtime.InteropServices;

namespace KeyR
{
    public static class BypassInput
    {
        // ───────── SendInput Structures ─────────
        [StructLayout(LayoutKind.Sequential)]
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

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // ───────── Constants ─────────
        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;

        // Mouse flags
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x1000;
        private const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // Keyboard flags
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        // System metrics
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;

        // XBUTTON identifiers
        private const int XBUTTON1 = 0x0001;
        private const int XBUTTON2 = 0x0002;

        private static int _cachedVScreenWidth;
        private static int _cachedVScreenHeight;
        private static int _cachedVScreenX;
        private static int _cachedVScreenY;
        private static bool _screenCached;

        public static void InvalidateScreenCache() => _screenCached = false;

        private static void EnsureScreenCached()
        {
            if (!_screenCached)
            {
                _cachedVScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                _cachedVScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                _cachedVScreenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
                _cachedVScreenY = GetSystemMetrics(SM_YVIRTUALSCREEN);
                if (_cachedVScreenWidth == 0) _cachedVScreenWidth = GetSystemMetrics(0); // SM_CXSCREEN fallback
                if (_cachedVScreenHeight == 0) _cachedVScreenHeight = GetSystemMetrics(1); // SM_CYSCREEN fallback
                _screenCached = true;
            }
        }

        /// <summary>
        /// Known extended virtual keys that MUST have KEYEVENTF_EXTENDEDKEY set.
        /// </summary>
        private static bool IsExtendedKey(ushort vk)
        {
            // Right-hand modifiers
            if (vk == 0xA3) return true; // VK_RCONTROL
            if (vk == 0xA5) return true; // VK_RMENU (Right Alt)
            // Win keys
            if (vk == 0x5B || vk == 0x5C) return true; // VK_LWIN / VK_RWIN
            // Navigation cluster
            if (vk == 0x2D) return true; // VK_INSERT
            if (vk == 0x2E) return true; // VK_DELETE
            if (vk == 0x24) return true; // VK_HOME
            if (vk == 0x23) return true; // VK_END
            if (vk == 0x21) return true; // VK_PRIOR (Page Up)
            if (vk == 0x22) return true; // VK_NEXT (Page Down)
            // Arrows
            if (vk >= 0x25 && vk <= 0x28) return true; // VK_LEFT..VK_DOWN
            // Numpad enter (when raw hook reports it with extended flag)
            if (vk == 0x0D) return false; // Regular Enter — extended only if flagged at recording time
            // Apps/Menu key
            if (vk == 0x5D) return true; // VK_APPS
            // Numlock, Scroll Lock, Break
            if (vk == 0x90) return true; // VK_NUMLOCK
            if (vk == 0x2C) return true; // VK_SNAPSHOT (PrintScreen)
            // Numpad divide and multiply via extended
            if (vk == 0x6F) return true; // VK_DIVIDE
            return false;
        }

        // ───────── Public API ─────────

        /// <summary>
        /// Send a key event via SendInput. Respects the extended-key flag from recording.
        /// </summary>
        public static void SendKey(ushort keycode, bool isDown, bool isExtended = false)
        {
            // Determine extended flag: use recording flag, OR known-extended table
            bool extended = isExtended || IsExtendedKey(keycode);

            ushort scanCode = (ushort)MapVirtualKey(keycode, 0);

            uint flags = 0;
            if (!isDown) flags |= KEYEVENTF_KEYUP;
            if (extended) flags |= KEYEVENTF_EXTENDEDKEY;

            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keycode,
                        wScan = scanCode,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Send a mouse button click via SendInput.
        /// </summary>
        public static void SendMouseClick(string button, bool isDown)
        {
            uint flags = 0;
            int data = 0;

            switch (button)
            {
                case "Left":
                    flags = isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
                    break;
                case "Right":
                    flags = isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
                    break;
                case "Middle":
                    flags = isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
                    break;
                case "XButton1":
                    flags = isDown ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                    data = XBUTTON1;
                    break;
                case "XButton2":
                    flags = isDown ? MOUSEEVENTF_XDOWN : MOUSEEVENTF_XUP;
                    data = XBUTTON2;
                    break;
                default:
                    return;
            }

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = data,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Move the mouse to absolute pixel coordinates using SendInput.
        /// Supports multi-monitor virtual desktop.
        /// </summary>
        public static void SendMouseMove(int x, int y)
        {
            EnsureScreenCached();

            // Convert pixel coordinates to normalized 0-65535 range across the VIRTUAL DESKTOP
            int absoluteX = (int)(((double)(x - _cachedVScreenX) * 65535) / (_cachedVScreenWidth - 1)) + 1;
            int absoluteY = (int)(((double)(y - _cachedVScreenY) * 65535) / (_cachedVScreenHeight - 1)) + 1;

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = absoluteX,
                        dy = absoluteY,
                        mouseData = 0,
                        dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Send a mouse wheel scroll (vertical or horizontal) at the given position.
        /// </summary>
        public static void SendMouseWheelAt(int x, int y, int delta, bool horizontal)
        {
            SendMouseMove(x, y);

            uint flags = horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL;

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = delta,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Release common modifier keys to prevent stuck state after playback.
        /// </summary>
        public static void ReleaseModifiers()
        {
            // VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, VK_RWIN
            // Plus left/right specific variants
            ushort[] keys = { 
                0x10, 0x11, 0x12,       // VK_SHIFT, VK_CONTROL, VK_MENU
                0xA0, 0xA1,             // VK_LSHIFT, VK_RSHIFT
                0xA2, 0xA3,             // VK_LCONTROL, VK_RCONTROL
                0xA4, 0xA5,             // VK_LMENU, VK_RMENU
                0x5B, 0x5C              // VK_LWIN, VK_RWIN
            };
            foreach (var k in keys) SendKey(k, false);
        }
    }
}
