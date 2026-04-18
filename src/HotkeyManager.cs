using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace KeyR
{
    public class HotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr _hWnd;
        private int _currentId = 0;

        public event Action<int> HotkeyPressed;

        public HotkeyManager(IntPtr hWnd)
        {
            _hWnd = hWnd;
            ComponentDispatcher.ThreadPreprocessMessage += ProcessMessage;
        }

        private void ProcessMessage(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg.message == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke(msg.wParam.ToInt32());
                handled = true;
            }
        }

        public int Register(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            _currentId++;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint mod = (uint)modifiers;

            RegisterHotKey(_hWnd, _currentId, mod, vk);
            return _currentId;
        }

        public void Unregister(int id)
        {
            UnregisterHotKey(_hWnd, id);
        }

        public void Dispose()
        {
            ComponentDispatcher.ThreadPreprocessMessage -= ProcessMessage;
            for (int i = 1; i <= _currentId; i++)
            {
                UnregisterHotKey(_hWnd, i);
            }
        }
    }
}
