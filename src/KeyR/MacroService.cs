using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Gma.System.MouseKeyHook;

namespace SupTask;

public class MacroService : IDisposable
{
	private List<MacroEvent> _events = new List<MacroEvent>();

	private IKeyboardMouseEvents _recHook;

	private IKeyboardMouseEvents _hotkeyHook;

	private Stopwatch _stopwatch = new Stopwatch();

	private CancellationTokenSource _playCts;

	private Thread _playThread;

	private Action _recAction;

	private Action _playAction;

	private string _recHotkey;

	private string _playHotkey;

	private Settings _currentSettings;

	public bool IsRecording { get; private set; }

	public bool IsPlaying { get; private set; }

	public bool HotkeysSuspended { get; set; }

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
		_recAction = delegate
		{
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)delegate
			{
				ToggleRecord(settings);
			});
		};
		_playAction = delegate
		{
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)delegate
			{
				TogglePlay(settings);
			});
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
		}
	}

	public void ToggleRecord(Settings settings)
	{
		if (IsRecording)
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
		if (IsPlaying)
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
		if (!IsPlaying)
		{
			_events.Clear();
			_recHook = Hook.GlobalEvents();
			_recHook.MouseDownExt += GlobalHook_MouseDownExt;
			_recHook.MouseUpExt += GlobalHook_MouseUpExt;
			_recHook.MouseMove += GlobalHook_MouseMove;
			_recHook.KeyDown += GlobalHook_KeyDown;
			_recHook.KeyUp += GlobalHook_KeyUp;
			IsRecording = true;
			_stopwatch.Restart();
			this.OnStatusChanged?.Invoke("Recording", isRecording: true, isPlaying: false);
		}
	}

	private void StopRecording()
	{
		if (!IsRecording)
		{
			return;
		}
		IsRecording = false;
		if (_recHook != null)
		{
			_recHook.MouseDownExt -= GlobalHook_MouseDownExt;
			_recHook.MouseUpExt -= GlobalHook_MouseUpExt;
			_recHook.MouseMove -= GlobalHook_MouseMove;
			_recHook.KeyDown -= GlobalHook_KeyDown;
			_recHook.KeyUp -= GlobalHook_KeyUp;
			_recHook.Dispose();
			_recHook = null;
		}
		_stopwatch.Stop();
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

	private void GlobalHook_MouseDownExt(object sender, MouseEventExtArgs e)
	{
		LogMouse(e, true);
	}

	private void GlobalHook_MouseUpExt(object sender, MouseEventExtArgs e)
	{
		LogMouse(e, false);
	}

	private void GlobalHook_MouseMove(object sender, MouseEventArgs e)
	{
		LogMouse(e, null);
	}

	private void LogMouse(MouseEventArgs e, bool? isDown)
	{
		long elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
		_stopwatch.Restart();
		string button = "Move";
		if (isDown.HasValue)
		{
			button = ((e.Button == MouseButtons.Left) ? "Left" : ((e.Button == MouseButtons.Right) ? "Right" : ((e.Button == MouseButtons.Middle) ? "Middle" : "Move")));
		}
		_events.Add(new MacroEvent
		{
			Type = EventType.MouseEvent,
			Delay = elapsedMilliseconds,
			X = e.X,
			Y = e.Y,
			Button = button,
			IsDown = (isDown == true)
		});
	}

	private void GlobalHook_KeyDown(object sender, KeyEventArgs e)
	{
		LogKey(e, isDown: true);
	}

	private void GlobalHook_KeyUp(object sender, KeyEventArgs e)
	{
		LogKey(e, isDown: false);
	}

	private void LogKey(KeyEventArgs e, bool isDown)
	{
		long elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
		_stopwatch.Restart();
		_events.Add(new MacroEvent
		{
			Type = EventType.KeyEvent,
			Delay = elapsedMilliseconds,
			KeyCode = (int)e.KeyCode,
			IsDown = isDown
		});
	}

	private void StartPlaying(Settings settings)
	{
		if (!IsRecording && _events.Count != 0)
		{
			IsPlaying = true;
			_playCts = new CancellationTokenSource();
			this.OnStatusChanged?.Invoke("Playing", isRecording: false, isPlaying: true);
			_playThread = new Thread((ThreadStart)delegate
			{
				PlaybackRoutine(settings, _playCts.Token);
			});
			_playThread.SetApartmentState(ApartmentState.STA);
			_playThread.Start();
		}
	}

	public void StopPlaying()
	{
		if (IsPlaying)
		{
			IsPlaying = false;
			_playCts?.Cancel();
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
		int num2 = 0;
		try
		{
			while (!token.IsCancellationRequested && (settings.LoopContinuous || num2 < settings.LoopCount))
			{
				long num3 = 0L;
				Stopwatch stopwatch = Stopwatch.StartNew();
				foreach (MacroEvent @event in _events)
				{
					if (token.IsCancellationRequested)
					{
						break;
					}
					long num4 = (long)((double)@event.Delay / num);
					num3 += num4;
					long num5 = num3 - stopwatch.ElapsedMilliseconds;
					if (num5 > 15)
					{
						Thread.Sleep((int)(num5 - 10));
					}
					while (stopwatch.ElapsedMilliseconds < num3)
					{
						Thread.SpinWait(100);
					}
					if (@event.Type == EventType.MouseEvent)
					{
						BypassInput.SendMouseMove(@event.X, @event.Y);
						if (@event.Button != "Move")
						{
							BypassInput.SendMouseClick(@event.Button, @event.IsDown);
						}
					}
					else
					{
						BypassInput.SendKey((ushort)@event.KeyCode, @event.IsDown);
					}
				}
				num2++;
			}
		}
		catch (Exception)
		{
		}
		finally
		{
			IsPlaying = false;
			((DispatcherObject)System.Windows.Application.Current).Dispatcher.Invoke((Action)delegate
			{
				this.OnStatusChanged?.Invoke("Ready", isRecording: false, isPlaying: false);
			});
		}
	}

	public string Serialize()
	{
		return JsonSerializer.Serialize(_events);
	}

	public void Deserialize(string json)
	{
		try
		{
			List<MacroEvent> list = JsonSerializer.Deserialize<List<MacroEvent>>(json);
			if (list != null)
			{
				_events = list;
			}
		}
		catch
		{
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
		return _events.Sum((MacroEvent e) => e.Delay);
	}

	public void Dispose()
	{
		StopPlaying();
		StopRecording();
		if (_hotkeyHook != null)
		{
			_hotkeyHook.KeyDown -= HotkeyHook_KeyDown;
			_hotkeyHook.Dispose();
		}
	}
}

