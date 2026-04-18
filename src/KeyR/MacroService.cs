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
	private List<MacroEvent> _events = new List<MacroEvent>(2048);

	private IKeyboardMouseEvents _recHook;

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
		if (!_isPlaying)
		{
			_events.Clear();
			_recHook = Hook.GlobalEvents();
			_recHook.MouseDownExt += RecHook_MouseDownExt;
			_recHook.MouseUpExt += RecHook_MouseUpExt;
			_recHook.MouseMove += RecHook_MouseMove;
			_recHook.MouseWheelExt += GlobalHook_MouseWheelExt;
			_recHook.KeyDown += RecHook_KeyDown;
			_recHook.KeyUp += RecHook_KeyUp;
			_isRecording = true;
			_totalRecordedMs = 0L;
			_stopwatch.Restart();
			this.OnStatusChanged?.Invoke("Recording", isRecording: true, isPlaying: false);
		}
	}

	private void RecHook_MouseDownExt(object sender, MouseEventExtArgs e)
	{
		LogMouse(e, true);
	}

	private void RecHook_MouseUpExt(object sender, MouseEventExtArgs e)
	{
		LogMouse(e, false);
	}

	private void RecHook_MouseMove(object sender, MouseEventArgs e)
	{
		LogMouse(e, null);
	}

	private void RecHook_KeyDown(object sender, KeyEventArgs e)
	{
		LogKey(e, isDown: true);
	}

	private void RecHook_KeyUp(object sender, KeyEventArgs e)
	{
		LogKey(e, isDown: false);
	}

	private void StopRecording()
	{
		if (!_isRecording)
		{
			return;
		}
		_isRecording = false;
		if (_recHook != null)
		{
			_recHook.MouseDownExt -= RecHook_MouseDownExt;
			_recHook.MouseUpExt -= RecHook_MouseUpExt;
			_recHook.MouseMove -= RecHook_MouseMove;
			_recHook.MouseWheelExt -= GlobalHook_MouseWheelExt;
			_recHook.KeyDown -= RecHook_KeyDown;
			_recHook.KeyUp -= RecHook_KeyUp;
			_recHook.Dispose();
			_recHook = null;
		}
		_stopwatch.Stop();
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

	private void GlobalHook_MouseWheelExt(object sender, MouseEventExtArgs e)
	{
		double delay = (double)_stopwatch.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
		_stopwatch.Restart();
		_events.Add(new MacroEvent
		{
			Type = EventType.MouseEvent,
			Delay = delay,
			X = e.X,
			Y = e.Y,
			Button = "Wheel",
			ScrollDelta = e.Delta
		});
	}

	private void LogMouse(MouseEventArgs e, bool? isDown)
	{
		double num = (double)_stopwatch.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
		_stopwatch.Restart();
		_totalRecordedMs += (long)num;
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
			return;
		}
		string button = "Move";
		if (isDown.HasValue)
		{
			button = ((e.Button == MouseButtons.Left) ? "Left" : ((e.Button == MouseButtons.Right) ? "Right" : ((e.Button == MouseButtons.Middle) ? "Middle" : "Move")));
		}
		_events.Add(new MacroEvent
		{
			Type = EventType.MouseEvent,
			Delay = num,
			X = e.X,
			Y = e.Y,
			Button = button,
			IsDown = (isDown == true)
		});
	}

	private void LogKey(KeyEventArgs e, bool isDown)
	{
		double num = (double)_stopwatch.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency;
		_stopwatch.Restart();
		_totalRecordedMs += (long)num;
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
		}
		else
		{
			_events.Add(new MacroEvent
			{
				Type = EventType.KeyEvent,
				Delay = num,
				KeyCode = (int)e.KeyCode,
				IsDown = isDown
			});
		}
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
						if (num4 - timestamp2 > Stopwatch.Frequency / 1000 * 15)
						{
							Thread.Sleep(1);
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
						BypassInput.SendKey((ushort)@event.KeyCode, @event.IsDown);
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

