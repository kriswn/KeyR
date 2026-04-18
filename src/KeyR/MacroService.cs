using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Point = System.Drawing.Point;
using System.Windows.Forms;
using System.Windows.Threading;
using Gma.System.MouseKeyHook;

namespace SupTask;

public class MacroService : IDisposable
{
	private static class NativeMethods
	{
		public delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

		public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

		public struct MSLLHOOKSTRUCT
		{
			public Point pt;

			public int mouseData;

			public int flags;

			public int time;

			public nint dwExtraInfo;
		}

		public struct KBDLLHOOKSTRUCT
		{
			public uint vkCode;

			public uint scanCode;

			public uint flags;

			public uint time;

			public nint dwExtraInfo;
		}

		public const int WH_MOUSE_LL = 14;

		public const int WH_KEYBOARD_LL = 13;

		public const int WM_MOUSEMOVE = 512;

		public const int WM_LBUTTONDOWN = 513;

		public const int WM_LBUTTONUP = 514;

		public const int WM_RBUTTONDOWN = 516;

		public const int WM_RBUTTONUP = 517;

		public const int WM_MBUTTONDOWN = 519;

		public const int WM_MBUTTONUP = 520;

		public const int WM_MOUSEWHEEL = 522;

		public const int WM_MOUSEHWHEEL = 526;

		public const int WM_XBUTTONDOWN = 523;

		public const int WM_XBUTTONUP = 524;

		public const int WM_KEYDOWN = 256;

		public const int WM_KEYUP = 257;

		public const int WM_SYSKEYDOWN = 260;

		public const int WM_SYSKEYUP = 261;

		[DllImport("user32.dll", SetLastError = true)]
		public static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool UnhookWindowsHookEx(nint hhk);

		[DllImport("user32.dll")]
		public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern nint GetModuleHandle(string lpModuleName);
	}

	private List<MacroEvent> _events = new List<MacroEvent>(2048);

	private nint _mouseHookHandle = IntPtr.Zero;

	private nint _keyboardHookHandle = IntPtr.Zero;

	private NativeMethods.LowLevelMouseProc _mouseHookProc;

	private NativeMethods.LowLevelKeyboardProc _keyboardHookProc;

	private IKeyboardMouseEvents _hotkeyHook;

	private Stopwatch _stopwatch = new Stopwatch();

	private volatile bool _isRecording;

	private volatile bool _isPlaying;

	private CancellationTokenSource _playCts;

	private Thread _playThread;

	private Action _recAction;

	private Action _playAction;

	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions();

	private const long MAX_RECORDING_MS = 356400000L;

	private long _totalRecordedMs;

	private long _recordingStartTicks;

	private volatile int _loopsRemaining;

	private volatile bool _restartRequested;

	private volatile bool _disposed;

	private long _playbackStartTicks;

	private double _playbackSpeed = 1.0;

	private ConditionEngine _engine;

	private string _recHotkey;

	private string _playHotkey;

	private string _recKeyOnly;

	private string _playKeyOnly;

	private Settings _currentSettings;

	public bool IsRecording
	{
		get
		{
			return _isRecording;
		}
		set
		{
			_isRecording = value;
		}
	}

	public bool IsPlaying
	{
		get
		{
			return _isPlaying;
		}
		set
		{
			_isPlaying = value;
		}
	}

	public bool HotkeysSuspended { get; set; }

	public long RecordingElapsedMs
	{
		get
		{
			long num = Interlocked.Read(in _recordingStartTicks);
			if (num == 0L)
			{
				return 0L;
			}
			return (long)((double)(Stopwatch.GetTimestamp() - num) * 1000.0 / (double)Stopwatch.Frequency);
		}
	}

	public long PlaybackElapsedMs
	{
		get
		{
			long num = Interlocked.Read(in _playbackStartTicks);
			if (num == 0L)
			{
				return 0L;
			}
			return (long)((double)(Stopwatch.GetTimestamp() - num) * 1000.0 / (double)Stopwatch.Frequency);
		}
	}

	public double PlaybackSpeed => _playbackSpeed;

	public event StatusChangedHandler OnStatusChanged;

	public MacroService()
	{
		_hotkeyHook = Hook.GlobalEvents();
		_hotkeyHook.KeyDown += HotkeyHook_KeyDown;
	}

	public void RegisterHotkeys(Settings settings)
	{
		_currentSettings = settings;
		_recHotkey = settings.RecHotkey;
		_playHotkey = settings.PlayHotkey;
		_recKeyOnly = _recHotkey.Split('+').Last();
		_playKeyOnly = _playHotkey.Split('+').Last();
		_recAction = delegate
		{
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				ToggleRecord(settings);
			}, Array.Empty<object>());
		};
		_playAction = delegate
		{
			if (_isPlaying)
			{
				StopPlaying();
			}
			else
			{
				((DispatcherObject)System.Windows.Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					TogglePlay(settings);
				}, Array.Empty<object>());
			}
		};
	}

	public void SuspendHotkeys()
	{
		HotkeysSuspended = true;
	}

	public void ResumeHotkeys()
	{
		HotkeysSuspended = false;
	}

	private void HotkeyHook_KeyDown(object sender, KeyEventArgs e)
	{
		if (!HotkeysSuspended && (!string.IsNullOrEmpty(_recHotkey) || !string.IsNullOrEmpty(_playHotkey)))
		{
			string text = "";
			if (e.Control)
			{
				text += "Control+";
			}
			if (e.Alt)
			{
				text += "Alt+";
			}
			if (e.Shift)
			{
				text += "Shift+";
			}
			text += e.KeyCode;
			if (text == _recHotkey)
			{
				e.Handled = true;
				_recAction?.Invoke();
			}
			else if (text == _playHotkey)
			{
				e.Handled = true;
				_playAction?.Invoke();
			}
			else if (_isPlaying && e.KeyCode.ToString() == _playKeyOnly)
			{
				e.Handled = true;
				_playAction?.Invoke();
			}
		}
	}

	public void ToggleRecord(Settings settings)
	{
		if (_isRecording)
		{
			StopRecording();
		}
		else
		{
			StartRecording();
		}
	}

	public void TogglePlay(Settings settings)
	{
		if (_isPlaying)
		{
			StopPlaying();
		}
		else
		{
			StartPlaying(settings);
		}
	}

	private void StartRecording()
	{
		if (_isPlaying)
		{
			return;
		}
		_events.Clear();
		_mouseHookProc = MouseHookCallback;
		_keyboardHookProc = KeyboardHookCallback;
		using (Process process = Process.GetCurrentProcess())
		{
			using ProcessModule processModule = process.MainModule;
			nint moduleHandle = NativeMethods.GetModuleHandle(processModule.ModuleName);
			_mouseHookHandle = NativeMethods.SetWindowsHookEx(14, _mouseHookProc, moduleHandle, 0u);
			_keyboardHookHandle = NativeMethods.SetWindowsHookEx(13, _keyboardHookProc, moduleHandle, 0u);
		}
		_isRecording = true;
		_totalRecordedMs = 0L;
		Interlocked.Exchange(ref _recordingStartTicks, Stopwatch.GetTimestamp());
		_stopwatch.Restart();
		this.OnStatusChanged?.Invoke("Recording", isRecording: true, isPlaying: false);
	}

	private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
	{
		if (nCode >= 0 && _isRecording)
		{
			NativeMethods.MSLLHOOKSTRUCT mSLLHOOKSTRUCT = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
			int num = (int)wParam;
			double num2 = (double)_stopwatch.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
			_stopwatch.Restart();
			_totalRecordedMs += (long)num2;
			if (_totalRecordedMs >= 356400000)
			{
				System.Windows.Application current = System.Windows.Application.Current;
				if (current != null)
				{
					((DispatcherObject)current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
					{
						StopRecording();
					}, Array.Empty<object>());
				}
				return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
			}
			string button = "Move";
			bool isDown = false;
			int scrollDelta = 0;
			bool flag = true;
			switch (num)
			{
			case 512:
				button = "Move";
				break;
			case 513:
				button = "Left";
				isDown = true;
				break;
			case 514:
				button = "Left";
				isDown = false;
				break;
			case 516:
				button = "Right";
				isDown = true;
				break;
			case 517:
				button = "Right";
				isDown = false;
				break;
			case 519:
				button = "Middle";
				isDown = true;
				break;
			case 520:
				button = "Middle";
				isDown = false;
				break;
			case 522:
				button = "Wheel";
				scrollDelta = (short)((mSLLHOOKSTRUCT.mouseData >> 16) & 0xFFFF);
				break;
			case 526:
				button = "HWheel";
				scrollDelta = (short)((mSLLHOOKSTRUCT.mouseData >> 16) & 0xFFFF);
				break;
			case 523:
				button = ((((mSLLHOOKSTRUCT.mouseData >> 16) & 0xFFFF) == 1) ? "XButton1" : "XButton2");
				isDown = true;
				break;
			case 524:
				button = ((((mSLLHOOKSTRUCT.mouseData >> 16) & 0xFFFF) == 1) ? "XButton1" : "XButton2");
				isDown = false;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				_events.Add(new MacroEvent
				{
					Type = EventType.MouseEvent,
					Delay = num2,
					X = mSLLHOOKSTRUCT.pt.X,
					Y = mSLLHOOKSTRUCT.pt.Y,
					Button = button,
					IsDown = isDown,
					ScrollDelta = scrollDelta
				});
			}
		}
		return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
	}

	private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
	{
		if (nCode >= 0 && _isRecording)
		{
			NativeMethods.KBDLLHOOKSTRUCT kBDLLHOOKSTRUCT = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
			int num = (int)wParam;
			bool flag = num == 256 || num == 260;
			bool flag2 = num == 257 || num == 261;
			if (flag || flag2)
			{
				double num2 = (double)_stopwatch.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
				_stopwatch.Restart();
				_totalRecordedMs += (long)num2;
				if (_totalRecordedMs >= 356400000)
				{
					System.Windows.Application current = System.Windows.Application.Current;
					if (current != null)
					{
						((DispatcherObject)current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
						{
							StopRecording();
						}, Array.Empty<object>());
					}
					return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
				}
				bool isExtendedKey = (kBDLLHOOKSTRUCT.flags & 1) != 0;
				_events.Add(new MacroEvent
				{
					Type = EventType.KeyEvent,
					Delay = num2,
					KeyCode = (int)kBDLLHOOKSTRUCT.vkCode,
					IsDown = flag,
					IsExtendedKey = isExtendedKey
				});
			}
		}
		return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
	}

	private void StopRecording()
	{
		if (!_isRecording)
		{
			return;
		}
		_isRecording = false;
		if (_mouseHookHandle != IntPtr.Zero)
		{
			NativeMethods.UnhookWindowsHookEx(_mouseHookHandle);
			_mouseHookHandle = IntPtr.Zero;
		}
		if (_keyboardHookHandle != IntPtr.Zero)
		{
			NativeMethods.UnhookWindowsHookEx(_keyboardHookHandle);
			_keyboardHookHandle = IntPtr.Zero;
		}
		_stopwatch.Stop();
		Interlocked.Exchange(ref _recordingStartTicks, 0L);
		BypassInput.ReleaseModifiers();
		if (_events.Count > 0 && _events.Last().Type == EventType.KeyEvent)
		{
			_events.RemoveAt(_events.Count - 1);
			if (_events.Count > 0)
			{
				_events.RemoveAt(_events.Count - 1);
			}
		}
		this.OnStatusChanged?.Invoke($"Stored {_events.Count} acts", isRecording: false, isPlaying: false);
	}

	private void StartPlaying(Settings settings)
	{
		StartPlayingInternal(settings, isRestart: false);
	}

	private void StartPlayingInternal(Settings settings, bool isRestart)
	{
		if (_isRecording || _events.Count == 0)
		{
			return;
		}
		_isPlaying = true;
		_restartRequested = false;
		_playCts = new CancellationTokenSource();
		BypassInput.InvalidateScreenCache();
		this.OnStatusChanged?.Invoke(isRestart ? "Restarting..." : "Playing", isRecording: false, isPlaying: true);
		if (!isRestart)
		{
			_loopsRemaining = (settings.LoopContinuous ? (-1) : settings.LoopCount);
			_engine?.Stop();
			_engine = new ConditionEngine(settings, delegate
			{
				_restartRequested = true;
				_playCts?.Cancel();
			}, () => _isPlaying);
			_engine.Start();
		}
		else
		{
			_engine?.AcknowledgeRestart();
		}
		_playThread = new Thread((ThreadStart)delegate
		{
			PlaybackRoutine(settings, _playCts.Token);
		});
		_playThread.IsBackground = true;
		_playThread.SetApartmentState(ApartmentState.STA);
		_playThread.Start();
	}

	public void StopPlaying()
	{
		_restartRequested = false;
		if (_isPlaying)
		{
			_isPlaying = false;
			try
			{
				_playCts?.Cancel();
			}
			catch
			{
			}
			try
			{
				_engine?.Stop();
			}
			catch
			{
			}
			_engine = null;
			BypassInput.ReleaseModifiers();
			this.OnStatusChanged?.Invoke("Ready", isRecording: false, isPlaying: false);
		}
	}

	private void PlaybackRoutine(Settings settings, CancellationToken token)
	{
		double num = (settings.UseCustomSpeed ? settings.CustomSpeed : 1.0);
		if (num <= 0.05)
		{
			num = 1.0;
		}
		_playbackSpeed = num;
		bool flag = _loopsRemaining < 0;
		try
		{
			while (!token.IsCancellationRequested && (flag || _loopsRemaining > 0))
			{
				Interlocked.Exchange(ref _playbackStartTicks, Stopwatch.GetTimestamp());
				long timestamp = Stopwatch.GetTimestamp();
				long num2 = 0L;
				foreach (MacroEvent @event in _events)
				{
					if (token.IsCancellationRequested)
					{
						return;
					}
					long num3 = (long)(@event.Delay * (double)Stopwatch.Frequency / 1000.0 / num);
					num2 += num3;
					long num4 = timestamp + num2;
					while (true)
					{
						if (token.IsCancellationRequested)
						{
							return;
						}
						long timestamp2 = Stopwatch.GetTimestamp();
						if (timestamp2 >= num4)
						{
							break;
						}
						long num5 = num4 - timestamp2;
						if (num5 > Stopwatch.Frequency / 1000 * 15)
						{
							Thread.Sleep(1);
						}
						else if (num5 > Stopwatch.Frequency / 1000 * 2)
						{
							Thread.Sleep(0);
						}
						else
						{
							Thread.SpinWait(10);
						}
					}
					if (token.IsCancellationRequested)
					{
						return;
					}
					if (@event.Type == EventType.MouseEvent)
					{
						if (@event.Button == "Wheel")
						{
							BypassInput.SendMouseWheelAt(@event.X, @event.Y, @event.ScrollDelta, horizontal: false);
							continue;
						}
						if (@event.Button == "HWheel")
						{
							BypassInput.SendMouseWheelAt(@event.X, @event.Y, @event.ScrollDelta, horizontal: true);
							continue;
						}
						BypassInput.SendMouseMove(@event.X, @event.Y);
						if (@event.Button != "Move")
						{
							BypassInput.SendMouseClick(@event.Button, @event.IsDown);
						}
					}
					else
					{
						BypassInput.SendKey((ushort)@event.KeyCode, @event.IsDown, @event.IsExtendedKey);
					}
				}
				if (settings.WaitConditionToRestart && !token.IsCancellationRequested && !_restartRequested)
				{
					while (!_restartRequested && !token.IsCancellationRequested && _isPlaying)
					{
						token.WaitHandle.WaitOne(50);
					}
				}
				if (!flag)
				{
					_loopsRemaining--;
				}
			}
		}
		catch
		{
		}
		finally
		{
			BypassInput.ReleaseModifiers();
			if (_restartRequested && !_disposed && _isPlaying)
			{
				_restartRequested = false;
				int loopsRemaining = (flag ? (-1) : ((_loopsRemaining <= 0) ? 1 : _loopsRemaining));
				if (!flag)
				{
					_loopsRemaining = loopsRemaining;
				}
				Thread.Sleep(300);
				if (!_disposed && _isPlaying)
				{
					System.Windows.Application current2 = System.Windows.Application.Current;
					if (current2 != null)
					{
						((DispatcherObject)current2).Dispatcher.BeginInvoke((Delegate)(Action)delegate
						{
							if (!_disposed && _isPlaying)
							{
								StartPlayingInternal(settings, isRestart: true);
							}
						}, Array.Empty<object>());
					}
					else
					{
						_isPlaying = false;
						try
						{
							_engine?.Stop();
						}
						catch
						{
						}
						_engine = null;
					}
				}
				else
				{
					_isPlaying = false;
					try
					{
						_engine?.Stop();
					}
					catch
					{
					}
					_engine = null;
				}
			}
			else
			{
				_isPlaying = false;
				_restartRequested = false;
				try
				{
					_engine?.Stop();
				}
				catch
				{
				}
				_engine = null;
				System.Windows.Application current3 = System.Windows.Application.Current;
				if (current3 != null && !_disposed)
				{
					((DispatcherObject)current3).Dispatcher.BeginInvoke((Delegate)(Action)delegate
					{
						this.OnStatusChanged?.Invoke("Ready", isRecording: false, isPlaying: false);
					}, Array.Empty<object>());
				}
			}
		}
	}

	public string Serialize()
	{
		return JsonSerializer.Serialize(_events, _jsonOptions);
	}

	public void Deserialize(string json)
	{
		try
		{
			List<MacroEvent> list = JsonSerializer.Deserialize<List<MacroEvent>>(json, _jsonOptions);
			if (list != null)
			{
				_events = list;
			}
		}
		catch
		{
		}
	}

	public void ImportInformaalTask(string[] lines)
	{
		List<MacroEvent> list = new List<MacroEvent>();
		double primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
		double primaryScreenHeight = SystemParameters.PrimaryScreenHeight;
		double num = 0.0;
		double num2 = 0.0;
		bool flag = false;
		string[] array = lines;
		foreach (string text in array)
		{
			if (string.IsNullOrWhiteSpace(text) || text.StartsWith("#"))
			{
				continue;
			}
			string[] array2 = text.Split('|');
			if (array2.Length < 3)
			{
				continue;
			}
			string text2 = array2[0].Trim().ToUpperInvariant();
			string[] array3 = null;
			if (text2 == "MOVE" || text2 == "MOUSE_MOVE")
			{
				if (array2.Length >= 4)
				{
					array3 = ((array2.Length < 5) ? new string[2]
					{
						array2[2],
						array2[3]
					} : new string[2]
					{
						array2[3],
						array2[4]
					});
				}
				else
				{
					string[] array4 = array2[2].Split(',');
					if (array4.Length >= 2)
					{
						array3 = new string[2]
						{
							array4[0],
							array4[1]
						};
					}
				}
			}
			else if (text2.Contains("MOUSE"))
			{
				if (array2.Length >= 5)
				{
					array3 = new string[2]
					{
						array2[3],
						array2[4]
					};
				}
				else
				{
					string[] array5 = array2[2].Split(',');
					if (array5.Length >= 3)
					{
						array3 = new string[2]
						{
							array5[1],
							array5[2]
						};
					}
					else if (array5.Length == 2)
					{
						array3 = array5;
					}
				}
			}
			if (array3 != null && array3.Length >= 2 && double.TryParse(array3[0], out var result) && double.TryParse(array3[1], out var result2))
			{
				if (result > num)
				{
					num = result;
				}
				if (result2 > num2)
				{
					num2 = result2;
				}
				flag = true;
			}
		}
		double num3 = 1.0;
		double num4 = 1.0;
		string value = "Raw Pixels";
		if (flag)
		{
			if (num <= 1.05)
			{
				num3 = primaryScreenWidth;
				num4 = primaryScreenHeight;
				value = "0..1 Scale";
			}
			else if (num <= 1005.0)
			{
				num3 = primaryScreenWidth / 1000.0;
				num4 = primaryScreenHeight / 1000.0;
				value = "0..1000 Scale";
			}
			else if (num <= 65536.0)
			{
				if (num > primaryScreenWidth + 100.0)
				{
					num3 = primaryScreenWidth / 65535.0;
					num4 = primaryScreenHeight / 65535.0;
					value = "Normalized (65k)";
				}
				else if (Math.Abs(num - 1920.0) < 10.0 || Math.Abs(num2 - 1080.0) < 10.0)
				{
					num3 = primaryScreenWidth / 1920.0;
					num4 = primaryScreenHeight / 1080.0;
					value = "Scale 1080p -> Current";
				}
			}
		}
		int x = 0;
		int y = 0;
		array = lines;
		foreach (string text3 in array)
		{
			if (string.IsNullOrWhiteSpace(text3) || text3.StartsWith("#"))
			{
				continue;
			}
			string[] array6 = text3.Split('|');
			if (array6.Length < 3)
			{
				continue;
			}
			string text4 = array6[0].Trim().ToUpperInvariant();
			if (!double.TryParse(array6[1], out var result3))
			{
				result3 = 0.0;
			}
			string[] array7 = null;
			string text5 = "Left";
			if (text4.Contains("MOUSE") || text4 == "MOVE")
			{
				if (array6.Length >= 4 && (text4 == "MOVE" || text4 == "MOUSE_MOVE"))
				{
					if (array6.Length >= 5)
					{
						text5 = array6[2].Trim();
						array7 = new string[2]
						{
							array6[3],
							array6[4]
						};
					}
					else
					{
						array7 = new string[2]
						{
							array6[2],
							array6[3]
						};
					}
				}
				else
				{
					string[] array8 = array6[2].Split(',');
					if (text4 == "MOVE" || text4 == "MOUSE_MOVE")
					{
						text5 = "Move";
						if (array8.Length >= 2)
						{
							array7 = new string[2]
							{
								array8[0],
								array8[1]
							};
						}
					}
					else
					{
						text5 = array8[0].Trim();
						if (array8.Length >= 3)
						{
							array7 = new string[2]
							{
								array8[1],
								array8[2]
							};
						}
						else if (array8.Length == 2)
						{
							array7 = array8;
						}
					}
				}
				if (array7 != null && array7.Length >= 2 && double.TryParse(array7[0], out var result4) && double.TryParse(array7[1], out var result5))
				{
					x = (int)(result4 * num3);
					y = (int)(result5 * num4);
				}
				switch (text5)
				{
				case "2":
					text5 = "Right";
					break;
				case "3":
					text5 = "Middle";
					break;
				case "4":
					text5 = "Wheel";
					break;
				case "5":
					text5 = "HWheel";
					break;
				default:
					if (text5 != "Left" && text5 != "Right" && text5 != "Middle" && text5 != "Move")
					{
						text5 = "Left";
					}
					break;
				}
				list.Add(new MacroEvent
				{
					Type = EventType.MouseEvent,
					Delay = result3,
					X = x,
					Y = y,
					Button = ((text4 == "MOVE" || text4 == "MOUSE_MOVE") ? "Move" : text5),
					IsDown = (text4 == "MOUSE_DOWN")
				});
			}
			else
			{
				if (!(text4 == "KEY_DOWN") && !(text4 == "KEY_UP"))
				{
					continue;
				}
				string text6 = array6[2].Trim();
				int num5 = 0;
				if (int.TryParse(text6, out var result6))
				{
					num5 = result6;
				}
				else
				{
					string text7 = text6.ToUpperInvariant().Replace("VK_", "");
					switch (text7)
					{
					case "ENTER":
					case "RETURN":
						num5 = 13;
						break;
					case "SPACE":
						num5 = 32;
						break;
					case "TAB":
						num5 = 9;
						break;
					case "ESCAPE":
					case "ESC":
						num5 = 27;
						break;
					case "SHIFT":
					case "LSHIFT":
					case "RSHIFT":
						num5 = 16;
						break;
					case "CTRL":
					case "LCONTROL":
					case "RCONTROL":
					case "CONTROL":
						num5 = 17;
						break;
					case "ALT":
					case "LMENU":
					case "RMENU":
						num5 = 18;
						break;
					case "BACKSPACE":
					case "BACK":
						num5 = 8;
						break;
					case "UP":
						num5 = 38;
						break;
					case "DOWN":
						num5 = 40;
						break;
					case "LEFT":
						num5 = 37;
						break;
					case "RIGHT":
						num5 = 39;
						break;
					default:
						if (text7.Length >= 1)
						{
							num5 = text7[0];
						}
						break;
					}
				}
				if (num5 > 0)
				{
					list.Add(new MacroEvent
					{
						Type = EventType.KeyEvent,
						Delay = result3,
						KeyCode = num5,
						IsDown = (text4 == "KEY_DOWN")
					});
				}
			}
		}
		if (list.Count > 0)
		{
			_events = list;
			this.OnStatusChanged?.Invoke($"Imported {list.Count} acts ({value})", isRecording: false, isPlaying: false);
		}
	}

	public void ImportTinyTask(byte[] rawData)
	{
		List<MacroEvent> list = new List<MacroEvent>();
		double primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
		double primaryScreenHeight = SystemParameters.PrimaryScreenHeight;
		int i = 0;
		if (rawData.Length > 8 && rawData[0] == 84)
		{
			i = 12;
		}
		int num = 16;
		if ((rawData.Length - i) % 20 == 0)
		{
			num = 20;
		}
		int num2 = 0;
		int num3 = 0;
		int num4 = -1;
		for (; i + num <= rawData.Length; i += num)
		{
			int num5 = BitConverter.ToInt32(rawData, i);
			int num6 = BitConverter.ToInt32(rawData, i + 4);
			int num7 = BitConverter.ToInt32(rawData, i + 8);
			int num8 = BitConverter.ToInt32(rawData, i + 12);
			int val = ((num4 != -1 && num8 >= num4) ? (num8 - num4) : 0);
			num4 = num8;
			EventType eventType = EventType.MouseEvent;
			string button = "Move";
			bool isDown = false;
			int keyCode = 0;
			bool flag = false;
			switch (num5)
			{
			case 512:
				button = "Move";
				break;
			case 513:
				button = "Left";
				isDown = true;
				break;
			case 514:
				button = "Left";
				isDown = false;
				break;
			case 516:
				button = "Right";
				isDown = true;
				break;
			case 517:
				button = "Right";
				isDown = false;
				break;
			case 519:
				button = "Middle";
				isDown = true;
				break;
			case 520:
				button = "Middle";
				isDown = false;
				break;
			case 256:
			case 260:
				eventType = EventType.KeyEvent;
				keyCode = num6;
				isDown = true;
				break;
			case 257:
			case 261:
				eventType = EventType.KeyEvent;
				keyCode = num6;
				isDown = false;
				break;
			default:
				switch (num5)
				{
				case 0:
					button = "Move";
					break;
				case 1:
					button = "Left";
					isDown = true;
					break;
				case 2:
					button = "Left";
					isDown = false;
					break;
				case 3:
					button = "Right";
					isDown = true;
					break;
				case 4:
					button = "Right";
					isDown = false;
					break;
				case 5:
					eventType = EventType.KeyEvent;
					keyCode = num6;
					isDown = true;
					break;
				case 6:
					eventType = EventType.KeyEvent;
					keyCode = num6;
					isDown = false;
					break;
				default:
					flag = true;
					break;
				}
				break;
			}
			if (!flag)
			{
				if (eventType == EventType.MouseEvent)
				{
					num2 = (int)((double)num6 * primaryScreenWidth / 65535.0);
					num3 = (int)((double)num7 * primaryScreenHeight / 65535.0);
					list.Add(new MacroEvent
					{
						Type = eventType,
						Delay = Math.Max(0, val),
						X = num2,
						Y = num3,
						Button = button,
						IsDown = isDown
					});
				}
				else
				{
					list.Add(new MacroEvent
					{
						Type = eventType,
						Delay = Math.Max(0, val),
						KeyCode = keyCode,
						IsDown = isDown
					});
				}
			}
		}
		if (list.Count > 0)
		{
			_events = list;
			this.OnStatusChanged?.Invoke($"Imported {list.Count} acts", isRecording: false, isPlaying: false);
		}
	}

	public int GetEventCount()
	{
		return _events.Count;
	}

	public void ClearEvents()
	{
		_events.Clear();
		this.OnStatusChanged?.Invoke("Ready", isRecording: false, isPlaying: false);
	}

	public long GetDurationMs()
	{
		if (_events.Count == 0)
		{
			return 0L;
		}
		return (long)_events.Sum((MacroEvent x) => x.Delay);
	}

	public void Dispose()
	{
		_disposed = true;
		StopPlaying();
		StopRecording();
		if (_hotkeyHook != null)
		{
			_hotkeyHook.KeyDown -= HotkeyHook_KeyDown;
			_hotkeyHook.Dispose();
		}
	}
}


