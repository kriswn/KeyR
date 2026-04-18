using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SupTask;

public class HotkeyManager : IDisposable
{
	private nint _hWnd;

	private int _currentId;

	public event Action<int> HotkeyPressed;

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vlc);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(nint hWnd, int id);

	public HotkeyManager(nint hWnd)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		_hWnd = hWnd;
		ComponentDispatcher.ThreadPreprocessMessage += new ThreadMessageEventHandler(ProcessMessage);
	}

	private void ProcessMessage(ref MSG msg, ref bool handled)
	{
		if (msg.message == 786)
		{
			this.HotkeyPressed?.Invoke(((IntPtr)(nint)msg.wParam).ToInt32());
			handled = true;
		}
	}

	public int Register(Key key, ModifierKeys modifiers = (ModifierKeys)0)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected I4, but got Unknown
		_currentId++;
		uint vlc = (uint)KeyInterop.VirtualKeyFromKey(key);
		uint fsModifiers = (uint)(int)modifiers;
		RegisterHotKey(_hWnd, _currentId, fsModifiers, vlc);
		return _currentId;
	}

	public void Unregister(int id)
	{
		UnregisterHotKey(_hWnd, id);
	}

	public void Dispose()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		ComponentDispatcher.ThreadPreprocessMessage -= new ThreadMessageEventHandler(ProcessMessage);
		for (int i = 1; i <= _currentId; i++)
		{
			UnregisterHotKey(_hWnd, i);
		}
	}
}


