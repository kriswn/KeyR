using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Point = System.Drawing.Point;

namespace KeyR
{
    public enum EventType { MouseEvent, KeyEvent }

    public class MacroEvent
    {
        public EventType Type { get; set; }
        public double Delay { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Button { get; set; } = "Move"; // Left, Right, Middle, XButton1, XButton2, Wheel, HWheel, Move
        public bool IsDown { get; set; }
        public int KeyCode { get; set; }
        public int ScrollDelta { get; set; } // For wheel events (WHEEL_DELTA units)
        public bool IsExtendedKey { get; set; } // For extended keyboard keys (arrows, nav, numpad enter, etc.)
    }

    public delegate void StatusChangedHandler(string message, bool isRecording, bool isPlaying);

    public class MacroService : IDisposable
    {
        private List<MacroEvent> _events = new List<MacroEvent>(2048); 

        // ── Raw low-level hook handles for recording ──
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private NativeMethods.LowLevelMouseProc _mouseHookProc;
        private NativeMethods.LowLevelKeyboardProc _keyboardHookProc;

        // ── Hotkey hook (uses globalmousekeyhook for convenience) ──
        private Gma.System.MouseKeyHook.IKeyboardMouseEvents _hotkeyHook;

        private Stopwatch _stopwatch = new Stopwatch();

        private volatile bool _isRecording;
        public bool IsRecording { get => _isRecording; set => _isRecording = value; }

        private volatile bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; set => _isPlaying = value; }

        public event StatusChangedHandler OnStatusChanged;

        private CancellationTokenSource _playCts;
        private Thread _playThread;

        public bool HotkeysSuspended { get; set; } = false;

        private Action _recAction;
        private Action _playAction;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions();
        private const long MAX_RECORDING_MS = 99 * 3600 * 1000L; // 99 hours
        private long _totalRecordedMs;

        // Recording elapsed ticks for the live recording timer
        private long _recordingStartTicks;

        // Tracks the remaining loops for restart logic
        private volatile int _loopsRemaining;
        private volatile bool _restartRequested;
        private volatile bool _disposed;
        private long _playbackStartTicks;
        private double _playbackSpeed = 1.0;

        // The single long-lived engine that persists across restarts
        private ConditionEngine _engine;

        // ── Win32 Interop for Raw Input Hooks ──
        private static class NativeMethods
        {
            public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
            public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll")]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            public const int WH_MOUSE_LL = 14;
            public const int WH_KEYBOARD_LL = 13;

            // Mouse messages
            public const int WM_MOUSEMOVE = 0x0200;
            public const int WM_LBUTTONDOWN = 0x0201;
            public const int WM_LBUTTONUP = 0x0202;
            public const int WM_RBUTTONDOWN = 0x0204;
            public const int WM_RBUTTONUP = 0x0205;
            public const int WM_MBUTTONDOWN = 0x0207;
            public const int WM_MBUTTONUP = 0x0208;
            public const int WM_MOUSEWHEEL = 0x020A;
            public const int WM_MOUSEHWHEEL = 0x020E;
            public const int WM_XBUTTONDOWN = 0x020B;
            public const int WM_XBUTTONUP = 0x020C;

            // Keyboard messages
            public const int WM_KEYDOWN = 0x0100;
            public const int WM_KEYUP = 0x0101;
            public const int WM_SYSKEYDOWN = 0x0104;
            public const int WM_SYSKEYUP = 0x0105;

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public Point pt;
                public int mouseData;
                public int flags;
                public int time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KBDLLHOOKSTRUCT
            {
                public uint vkCode;
                public uint scanCode;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }
        }

        public MacroService()
        {
            _hotkeyHook = Gma.System.MouseKeyHook.Hook.GlobalEvents();
            _hotkeyHook.KeyDown += HotkeyHook_KeyDown;
        }

        private string _recHotkey;
        private string _playHotkey;
        private string _recKeyOnly;
        private string _playKeyOnly;

        private Settings _currentSettings;

        public void RegisterHotkeys(Settings settings)
        {
            _currentSettings = settings;
            _recHotkey = settings.RecHotkey;
            _playHotkey = settings.PlayHotkey;
            
            _recKeyOnly = _recHotkey.Split('+').Last();
            _playKeyOnly = _playHotkey.Split('+').Last();

            _recAction = () => { System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => ToggleRecord(settings))); };
            _playAction = () => {
                // If already playing, stop IMMEDIATELY on this thread
                if (_isPlaying) { StopPlaying(); return; }
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => TogglePlay(settings)));
            };
        }

        public void SuspendHotkeys() => HotkeysSuspended = true;
        public void ResumeHotkeys() => HotkeysSuspended = false;

        private void HotkeyHook_KeyDown(object sender, KeyEventArgs e)
        {
            if (HotkeysSuspended) return;
            if (string.IsNullOrEmpty(_recHotkey) && string.IsNullOrEmpty(_playHotkey)) return;

            string pressed = "";
            if (e.Control) pressed += "Control+";
            if (e.Alt) pressed += "Alt+";
            if (e.Shift) pressed += "Shift+";
            pressed += e.KeyCode.ToString();

            // Match full hotkey
            if (pressed == _recHotkey)
            {
                e.Handled = true;
                _recAction?.Invoke();
            }
            else if (pressed == _playHotkey)
            {
                e.Handled = true;
                _playAction?.Invoke();
            }
            // During playback, be more lenient to stop (match base key)
            // This fixes the "multiple tries to stop" issue if modifiers are stuck
            else if (_isPlaying && e.KeyCode.ToString() == _playKeyOnly)
            {
                e.Handled = true;
                _playAction?.Invoke();
            }
        }

        public void ToggleRecord(Settings settings)
        {
            if (_isRecording) StopRecording();
            else StartRecording();
        }

        public void TogglePlay(Settings settings)
        {
            if (_isPlaying) StopPlaying();
            else StartPlaying(settings);
        }

        // ── Recording via Raw Low-Level Hooks ──
        private void StartRecording()
        {
            if (_isPlaying) return;
            _events.Clear();

            // Keep delegate references alive (prevent GC)
            _mouseHookProc = MouseHookCallback;
            _keyboardHookProc = KeyboardHookCallback;

            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                IntPtr hMod = NativeMethods.GetModuleHandle(curModule.ModuleName);
                _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseHookProc, hMod, 0);
                _keyboardHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardHookProc, hMod, 0);
            }

            _isRecording = true;
            _totalRecordedMs = 0;
            System.Threading.Interlocked.Exchange(ref _recordingStartTicks, Stopwatch.GetTimestamp());
            _stopwatch.Restart();
            OnStatusChanged?.Invoke("Recording", true, false);
        }

        /// <summary>
        /// Low-level mouse hook callback. Captures ALL mouse messages including XButtons and horizontal wheel.
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;

                double delay = _stopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                _stopwatch.Restart();
                _totalRecordedMs += (long)delay;

                if (_totalRecordedMs >= MAX_RECORDING_MS)
                {
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => StopRecording()));
                    return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                }

                string btn = "Move";
                bool isDown = false;
                int scrollDelta = 0;
                bool shouldRecord = true;

                switch (msg)
                {
                    case NativeMethods.WM_MOUSEMOVE:
                        btn = "Move"; break;
                    case NativeMethods.WM_LBUTTONDOWN:
                        btn = "Left"; isDown = true; break;
                    case NativeMethods.WM_LBUTTONUP:
                        btn = "Left"; isDown = false; break;
                    case NativeMethods.WM_RBUTTONDOWN:
                        btn = "Right"; isDown = true; break;
                    case NativeMethods.WM_RBUTTONUP:
                        btn = "Right"; isDown = false; break;
                    case NativeMethods.WM_MBUTTONDOWN:
                        btn = "Middle"; isDown = true; break;
                    case NativeMethods.WM_MBUTTONUP:
                        btn = "Middle"; isDown = false; break;
                    case NativeMethods.WM_MOUSEWHEEL:
                        btn = "Wheel";
                        scrollDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                        break;
                    case NativeMethods.WM_MOUSEHWHEEL:
                        btn = "HWheel";
                        scrollDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                        break;
                    case NativeMethods.WM_XBUTTONDOWN:
                        {
                            int xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                            btn = xButton == 1 ? "XButton1" : "XButton2";
                            isDown = true;
                        }
                        break;
                    case NativeMethods.WM_XBUTTONUP:
                        {
                            int xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                            btn = xButton == 1 ? "XButton1" : "XButton2";
                            isDown = false;
                        }
                        break;
                    default:
                        shouldRecord = false; break;
                }

                if (shouldRecord)
                {
                    _events.Add(new MacroEvent
                    {
                        Type = EventType.MouseEvent,
                        Delay = delay,
                        X = hookStruct.pt.X,
                        Y = hookStruct.pt.Y,
                        Button = btn,
                        IsDown = isDown,
                        ScrollDelta = scrollDelta
                    });
                }
            }
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        /// <summary>
        /// Low-level keyboard hook callback. Captures ALL key events including extended keys (arrows, nav, numpad).
        /// </summary>
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isRecording)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;

                bool isKeyDown = (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN);
                bool isKeyUp = (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP);

                if (isKeyDown || isKeyUp)
                {
                    double delay = _stopwatch.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
                    _stopwatch.Restart();
                    _totalRecordedMs += (long)delay;

                    if (_totalRecordedMs >= MAX_RECORDING_MS)
                    {
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => StopRecording()));
                        return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    // Detect extended key flag from the raw hook data
                    bool isExtended = (hookStruct.flags & 0x01) != 0; // LLKHF_EXTENDED

                    _events.Add(new MacroEvent
                    {
                        Type = EventType.KeyEvent,
                        Delay = delay,
                        KeyCode = (int)hookStruct.vkCode,
                        IsDown = isKeyDown,
                        IsExtendedKey = isExtended
                    });
                }
            }
            return NativeMethods.CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private void StopRecording()
        {
            if (!_isRecording) return;
            _isRecording = false;

            // Unhook raw hooks
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
            System.Threading.Interlocked.Exchange(ref _recordingStartTicks, 0);
            BypassInput.ReleaseModifiers(); // Clean up modifiers on finish
            
            // Filter hotkey release events at the tail (from stop-hotkey press)
            if (_events.Count > 0 && _events.Last().Type == EventType.KeyEvent)
            {
                _events.RemoveAt(_events.Count - 1); 
                if (_events.Count > 0) _events.RemoveAt(_events.Count - 1);
            }

            OnStatusChanged?.Invoke($"Stored {_events.Count} acts", false, false);
        }

        /// <summary>
        /// Elapsed recording time in milliseconds (for live timer display).
        /// </summary>
        public long RecordingElapsedMs
        {
            get
            {
                long start = System.Threading.Interlocked.Read(ref _recordingStartTicks);
                if (start == 0) return 0;
                long elapsedTicks = Stopwatch.GetTimestamp() - start;
                return (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
            }
        }

        private void StartPlaying(Settings settings)
        {
            StartPlayingInternal(settings, false);
        }

        /// <summary>
        /// Core method to start playback. 
        /// isRestart: true if this is a condition-triggered restart (engine stays alive).
        /// </summary>
        private void StartPlayingInternal(Settings settings, bool isRestart)
        {
            if (_isRecording || _events.Count == 0) return;
            
            _isPlaying = true;
            _restartRequested = false;
            _playCts = new CancellationTokenSource();
            BypassInput.InvalidateScreenCache();
            OnStatusChanged?.Invoke(isRestart ? "Restarting..." : "Playing", false, true);

            if (!isRestart)
            {
                // Fresh start: initialize loop count
                _loopsRemaining = settings.LoopContinuous ? -1 : settings.LoopCount;

                // Create a NEW engine only on fresh start
                _engine?.Stop();
                _engine = new ConditionEngine(settings, () => {
                    _restartRequested = true;
                    _playCts?.Cancel();
                }, () => _isPlaying);
                _engine.Start();
            }
            else
            {
                // Restart: acknowledge the engine so it resumes monitoring
                _engine?.AcknowledgeRestart();
            }

            _playThread = new Thread(() => PlaybackRoutine(settings, _playCts.Token));
            _playThread.IsBackground = true;
            _playThread.SetApartmentState(ApartmentState.STA);
            _playThread.Start();
        }

        public void StopPlaying()
        {
            _restartRequested = false; // Important: Clear this FIRST to prevent pending loops
            if (!_isPlaying) return;
            
            _isPlaying = false;
            try { _playCts?.Cancel(); } catch { }
            try { _engine?.Stop(); } catch { }
            _engine = null;
            
            BypassInput.ReleaseModifiers(); // CRITICAL: Stop stuck keys
            OnStatusChanged?.Invoke("Ready", false, false);
        }

        public long PlaybackElapsedMs
        {
            get
            {
                long start = System.Threading.Interlocked.Read(ref _playbackStartTicks);
                if (start == 0) return 0;
                long elapsedTicks = Stopwatch.GetTimestamp() - start;
                return (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
            }
        }
        public double PlaybackSpeed => _playbackSpeed;

        private void PlaybackRoutine(Settings settings, CancellationToken token)
        {
            double speed = settings.UseCustomSpeed ? settings.CustomSpeed : 1.0;
            if (speed <= 0.05) speed = 1.0;
            _playbackSpeed = speed;

            bool isContinuous = _loopsRemaining < 0; // -1 means continuous

            try
            {
                while (!token.IsCancellationRequested && (isContinuous || _loopsRemaining > 0))
                {
                    System.Threading.Interlocked.Exchange(ref _playbackStartTicks, Stopwatch.GetTimestamp());
                    long startTimestamp = Stopwatch.GetTimestamp();
                    long totalExpectedTicks = 0;

                    foreach (var ev in _events)
                    {
                        if (token.IsCancellationRequested) return;

                        long waitDelayTicks = (long)(ev.Delay * Stopwatch.Frequency / 1000.0 / speed);
                        totalExpectedTicks += waitDelayTicks;
                        
                        long targetTimestamp = startTimestamp + totalExpectedTicks;

                        while (true)
                        {
                            if (token.IsCancellationRequested) return; // Immediate Breakout Guarantee
                            long current = Stopwatch.GetTimestamp();
                            if (current >= targetTimestamp) break;

                            long remaining = targetTimestamp - current;
                            if (remaining > (Stopwatch.Frequency / 1000) * 15)
                            {
                                // Yield safely if >15ms away to save CPU
                                Thread.Sleep(1);
                            }
                            else if (remaining > (Stopwatch.Frequency / 1000) * 2)
                            {
                                // Short yield for 2-15ms range
                                Thread.Sleep(0);
                            }
                            else
                            {
                                // High priority spin for ultimate <2ms precision accuracy
                                Thread.SpinWait(10);
                            }
                        }
                        if (token.IsCancellationRequested) return;

                        if (ev.Type == EventType.MouseEvent)
                        {
                            if (ev.Button == "Wheel")
                            {
                                BypassInput.SendMouseWheelAt(ev.X, ev.Y, ev.ScrollDelta, false);
                            }
                            else if (ev.Button == "HWheel")
                            {
                                BypassInput.SendMouseWheelAt(ev.X, ev.Y, ev.ScrollDelta, true);
                            }
                            else
                            {
                                BypassInput.SendMouseMove(ev.X, ev.Y);
                                if (ev.Button != "Move") BypassInput.SendMouseClick(ev.Button, ev.IsDown);
                            }
                        }
                        else
                        {
                            BypassInput.SendKey((ushort)ev.KeyCode, ev.IsDown, ev.IsExtendedKey);
                        }
                    }

                    // If WaitConditionToRestart is ON, pause here until condition triggers restart
                    if (settings.WaitConditionToRestart && !token.IsCancellationRequested && !_restartRequested)
                    {
                        while (!_restartRequested && !token.IsCancellationRequested && _isPlaying)
                        {
                            token.WaitHandle.WaitOne(50);
                        }
                    }

                    if (!isContinuous)
                    {
                        _loopsRemaining--;
                    }
                }
            }
            catch { }
            finally
            {
                BypassInput.ReleaseModifiers();

                // Determine if we should restart
                bool doRestart = _restartRequested && !_disposed && _isPlaying;
                
                if (doRestart)
                {
                    _restartRequested = false;
                    int loopsForRestart = isContinuous ? -1 : (_loopsRemaining > 0 ? _loopsRemaining : 1);
                    // Update the loops remaining for restart, keeping our loop state
                    if (!isContinuous) _loopsRemaining = loopsForRestart;

                    Thread.Sleep(300);

                    // Re-check after sleep
                    if (!_disposed && _isPlaying)
                    {
                        var app = System.Windows.Application.Current;
                        if (app != null)
                        {
                            app.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_disposed || !_isPlaying) return;
                                StartPlayingInternal(settings, true);
                            }));
                        }
                        else
                        {
                            // App shutting down — clean up
                            _isPlaying = false;
                            try { _engine?.Stop(); } catch { }
                            _engine = null;
                        }
                    }
                    else
                    {
                        // Stopped during sleep — clean up
                        _isPlaying = false;
                        try { _engine?.Stop(); } catch { }
                        _engine = null;
                    }
                }
                else
                {
                    _isPlaying = false;
                    _restartRequested = false;
                    try { _engine?.Stop(); } catch { }
                    _engine = null;
                    
                    var currentApp = System.Windows.Application.Current;
                    if (currentApp != null && !_disposed)
                    {
                        currentApp.Dispatcher.BeginInvoke(new Action(() => 
                        {
                            OnStatusChanged?.Invoke("Ready", false, false);
                        }));
                    }
                }
            }
        }

        public string Serialize() => JsonSerializer.Serialize(_events, _jsonOptions);

        public void Deserialize(string json)
        {
            try {
                var evs = JsonSerializer.Deserialize<List<MacroEvent>>(json, _jsonOptions);
                if (evs != null) _events = evs;
            } catch { }
        }

        public void ImportInformaalTask(string[] lines)
        {
            var newEvents = new List<MacroEvent>();
            double targetW = NativeMethods.GetSystemMetrics(78); if (targetW == 0) targetW = NativeMethods.GetSystemMetrics(0);
            double targetH = NativeMethods.GetSystemMetrics(79); if (targetH == 0) targetH = NativeMethods.GetSystemMetrics(1);

            // Pass 1: Analysis - Discover coordinate system and range
            double maxX = 0, maxY = 0;
            bool foundCoords = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                string type = parts[0].Trim().ToUpperInvariant();
                string[] coordParts = null;

                if (type == "MOVE" || type == "MOUSE_MOVE")
                {
                    if (parts.Length >= 4) // TYPE|DELAY|X|Y or TYPE|DELAY|B|X|Y
                        coordParts = (parts.Length >= 5) ? new[] { parts[3], parts[4] } : new[] { parts[2], parts[3] };
                    else // TYPE|DELAY|X,Y
                    {
                        var dp = parts[2].Split(',');
                        if (dp.Length >= 2) coordParts = new[] { dp[0], dp[1] };
                    }
                }
                else if (type.Contains("MOUSE"))
                {
                    if (parts.Length >= 5) // TYPE|DELAY|B|X|Y
                        coordParts = new[] { parts[3], parts[4] };
                    else // TYPE|DELAY|B,X,Y
                    {
                        var dp = parts[2].Split(',');
                        if (dp.Length >= 3) coordParts = new[] { dp[1], dp[2] };
                        else if (dp.Length == 2) coordParts = dp;
                    }
                }

                if (coordParts != null && coordParts.Length >= 2 && 
                    double.TryParse(coordParts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x) && double.TryParse(coordParts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y))
                {
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                    foundCoords = true;
                }
            }

            // Determine scaling factor
            double scaleX = 1.0, scaleY = 1.0;
            string scaleInfo = "Raw Pixels";
            if (foundCoords)
            {
                if (maxX <= 1.05) { scaleX = targetW; scaleY = targetH; scaleInfo = "0..1 Scale"; }
                else if (maxX <= 1005) { scaleX = targetW / 1000.0; scaleY = targetH / 1000.0; scaleInfo = "0..1000 Scale"; }
                else if (maxX <= 65536) 
                {
                    if (maxX > targetW + 100) { scaleX = targetW / 65535.0; scaleY = targetH / 65535.0; scaleInfo = "Normalized (65k)"; }
                    else if (Math.Abs(maxX - 1920) < 10 || Math.Abs(maxY - 1080) < 10) { scaleX = targetW / 1920.0; scaleY = targetH / 1080.0; scaleInfo = "Scale 1080p -> Current"; }
                }
            }

            // Pass 2: Import with determined scaling
            int lastX = 0, lastY = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                string type = parts[0].Trim().ToUpperInvariant();
                if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double delay)) delay = 0;

                string[] coordParts = null;
                string btn = "Left";
                bool isMouse = type.Contains("MOUSE") || type == "MOVE";

                if (isMouse)
                {
                    if (parts.Length >= 4 && (type == "MOVE" || type == "MOUSE_MOVE"))
                    {
                        if (parts.Length >= 5) { btn = parts[2].Trim(); coordParts = new[] { parts[3], parts[4] }; }
                        else coordParts = new[] { parts[2], parts[3] };
                    }
                    else
                    {
                        var dataParts = parts[2].Split(',');
                        if (type == "MOVE" || type == "MOUSE_MOVE")
                        {
                            btn = "Move";
                            if (dataParts.Length >= 2) coordParts = new[] { dataParts[0], dataParts[1] };
                        }
                        else
                        {
                            btn = dataParts[0].Trim();
                            if (dataParts.Length >= 3) coordParts = new[] { dataParts[1], dataParts[2] };
                            else if (dataParts.Length == 2) coordParts = dataParts;
                        }
                    }

                    if (coordParts != null && coordParts.Length >= 2 && 
                        double.TryParse(coordParts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x) && double.TryParse(coordParts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y))
                    {
                        lastX = (int)(x * scaleX);
                        lastY = (int)(y * scaleY);
                    }

                    if (btn == "2") btn = "Right";
                    else if (btn == "3") btn = "Middle";
                    else if (btn == "4") btn = "Wheel";
                    else if (btn == "5") btn = "HWheel";
                    else if (btn != "Left" && btn != "Right" && btn != "Middle" && btn != "Move") btn = "Left";

                    newEvents.Add(new MacroEvent { 
                        Type = EventType.MouseEvent, 
                        Delay = delay, 
                        X = lastX, 
                        Y = lastY, 
                        Button = (type == "MOVE" || type == "MOUSE_MOVE") ? "Move" : btn, 
                        IsDown = (type == "MOUSE_DOWN") 
                    });
                }
                else if (type == "KEY_DOWN" || type == "KEY_UP")
                {
                    string kData = parts[2].Trim();
                    int keyCode = 0;
                    if (int.TryParse(kData, out int code)) keyCode = code;
                    else
                    {
                        string dUpper = kData.ToUpperInvariant().Replace("VK_", "");
                        if (dUpper == "ENTER" || dUpper == "RETURN") keyCode = 13;
                        else if (dUpper == "SPACE") keyCode = 32;
                        else if (dUpper == "TAB") keyCode = 9;
                        else if (dUpper == "ESCAPE" || dUpper == "ESC") keyCode = 27;
                        else if (dUpper == "SHIFT" || dUpper == "LSHIFT" || dUpper == "RSHIFT") keyCode = 16;
                        else if (dUpper == "CTRL" || dUpper == "LCONTROL" || dUpper == "RCONTROL" || dUpper == "CONTROL") keyCode = 17;
                        else if (dUpper == "ALT" || dUpper == "LMENU" || dUpper == "RMENU") keyCode = 18;
                        else if (dUpper == "BACKSPACE" || dUpper == "BACK") keyCode = 8;
                        else if (dUpper == "UP") keyCode = 38;
                        else if (dUpper == "DOWN") keyCode = 40;
                        else if (dUpper == "LEFT") keyCode = 37;
                        else if (dUpper == "RIGHT") keyCode = 39;
                        else if (dUpper.Length >= 1) keyCode = (int)dUpper[0];
                    }
                    
                    if (keyCode > 0)
                        newEvents.Add(new MacroEvent { Type = EventType.KeyEvent, Delay = delay, KeyCode = keyCode, IsDown = (type == "KEY_DOWN") });
                }
            }

            if (newEvents.Count > 0)
            {
                _events = newEvents;
                OnStatusChanged?.Invoke($"Imported {newEvents.Count} acts ({scaleInfo})", false, false);
            }
        }

        public void ImportTinyTask(byte[] rawData)
        {
            var newEvents = new List<MacroEvent>();
            double targetW = NativeMethods.GetSystemMetrics(78); if (targetW == 0) targetW = NativeMethods.GetSystemMetrics(0);
            double targetH = NativeMethods.GetSystemMetrics(79); if (targetH == 0) targetH = NativeMethods.GetSystemMetrics(1);
            
            int offset = 0;
            // TinyTask headers are typically "tiny" (4-12 bytes) or empty
            if (rawData.Length > 8 && rawData[0] == 'T') offset = 12;

            int recordSize = 16;
            if ((rawData.Length - offset) % 20 == 0) recordSize = 20;

            int lastX = 0, lastY = 0, lastTimestamp = -1;
            while (offset + recordSize <= rawData.Length)
            {
                int pType = BitConverter.ToInt32(rawData, offset);
                int pX = BitConverter.ToInt32(rawData, offset + 4);
                int pY = BitConverter.ToInt32(rawData, offset + 8);
                int pTime = BitConverter.ToInt32(rawData, offset + 12);
                
                int delay = (lastTimestamp != -1 && pTime >= lastTimestamp) ? pTime - lastTimestamp : 0;
                lastTimestamp = pTime;
                
                EventType evType = EventType.MouseEvent; string btn = "Move"; bool isDown = false; int kCode = 0; bool ignore = false;

                // TinyTask primarily uses raw Windows Hooks message IDs
                // Coordinates (pX, pY) in .rec files are almost always normalized (0-65535)
                switch (pType)
                {
                    case 0x0200: btn = "Move"; break; // WM_MOUSEMOVE (512)
                    case 0x0201: btn = "Left"; isDown = true; break; // WM_LBUTTONDOWN (513)
                    case 0x0202: btn = "Left"; isDown = false; break; // WM_LBUTTONUP (514)
                    case 0x0204: btn = "Right"; isDown = true; break; // WM_RBUTTONDOWN (516)
                    case 0x0205: btn = "Right"; isDown = false; break; // WM_RBUTTONUP (517)
                    case 0x0207: btn = "Middle"; isDown = true; break; // WM_MBUTTONDOWN (519)
                    case 0x0208: btn = "Middle"; isDown = false; break; // WM_MBUTTONUP (520)
                    case 0x0100: // WM_KEYDOWN (256)
                    case 0x0104: // WM_SYSKEYDOWN (260)
                        evType = EventType.KeyEvent; kCode = pX; isDown = true; break;
                    case 0x0101: // WM_KEYUP (257)
                    case 0x0105: // WM_SYSKEYUP (261)
                        evType = EventType.KeyEvent; kCode = pX; isDown = false; break;
                    default:
                        // Fallback mapping for 0-6 based indices
                        if (pType == 0) { btn = "Move"; }
                        else if (pType == 1) { btn = "Left"; isDown = true; }
                        else if (pType == 2) { btn = "Left"; isDown = false; }
                        else if (pType == 3) { btn = "Right"; isDown = true; }
                        else if (pType == 4) { btn = "Right"; isDown = false; }
                        else if (pType == 5) { evType = EventType.KeyEvent; kCode = pX; isDown = true; }
                        else if (pType == 6) { evType = EventType.KeyEvent; kCode = pX; isDown = false; }
                        else { ignore = true; }
                        break;
                }

                if (!ignore)
                {
                    if (evType == EventType.MouseEvent)
                    {
                        // Scaling normalized coordinates to screen pixels
                        lastX = (int)(pX * targetW / 65535.0);
                        lastY = (int)(pY * targetH / 65535.0);

                        newEvents.Add(new MacroEvent { Type = evType, Delay = Math.Max(0, delay), X = lastX, Y = lastY, Button = btn, IsDown = isDown });
                    }
                    else
                    {
                        newEvents.Add(new MacroEvent { Type = evType, Delay = Math.Max(0, delay), KeyCode = kCode, IsDown = isDown });
                    }
                }

                offset += recordSize;
            }

            if (newEvents.Count > 0)
            {
                _events = newEvents;
                OnStatusChanged?.Invoke($"Imported {newEvents.Count} acts", false, false);
            }
        }

        public int GetEventCount() => _events.Count;

        public void ClearEvents()
        {
            _events.Clear();
            OnStatusChanged?.Invoke("Ready", false, false);
        }

        public long GetDurationMs()
        {
            if (_events.Count == 0) return 0;
            return (long)_events.Sum(x => x.Delay);
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
}



