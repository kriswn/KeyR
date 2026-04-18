using Microsoft.Win32;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;

namespace KeyR
{
    public partial class MainWindow : Window
    {
        private Settings _settings;
        private MacroService _macroService;
        private bool _isLoaded = false;
        private string _macroName = "None";

        private SettingsWindow _settingsWindow = null;

        // Countdown timer for playback duration display
        private DispatcherTimer _countdownTimer;
        // Timer for recording duration display
        private DispatcherTimer _recordingTimer;

        // Cached brushes (frozen for thread-safety and perf)
        private static readonly SolidColorBrush RecordingBrush = CreateFrozenBrush("#e63946");
        private static readonly SolidColorBrush PlayingBrush = CreateFrozenBrush("#2a9d8f");
        private static readonly Regex NumberOnlyRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        public Settings AppSettings => _settings;
        public MacroService AppMacroService => _macroService;

        public MainWindow()
        {
            InitializeComponent();
            _settings = Settings.Load();
            _macroService = new MacroService();
            _macroService.OnStatusChanged += UpdateStatus;
            
            this.Loaded += MainWindow_Loaded;
            
            this.Topmost = _settings.AlwaysOnTop;
            
            _macroService.RegisterHotkeys(_settings);
            
            if (_settings.X != -1 && _settings.Y != -1)
            {
                var screenWidth = SystemParameters.VirtualScreenWidth;
                var screenHeight = SystemParameters.VirtualScreenHeight;

                if (_settings.X >= 0 && _settings.X <= screenWidth - this.Width &&
                    _settings.Y >= 0 && _settings.Y <= screenHeight - this.Height)
                {
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                    this.Left = _settings.X;
                    this.Top = _settings.Y;
                }
            }

            _isLoaded = true;
            ApplyResolutionScaling();

            // Setup countdown timer for playback
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _countdownTimer.Tick += CountdownTimer_Tick;

            // Setup recording timer
            _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _recordingTimer.Tick += RecordingTimer_Tick;
        }

        private void ApplyResolutionScaling()
        {
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double scale = screenHeight / 1080.0;
            if (scale < 0.8) scale = 0.8;
            if (scale > 1.2) scale = 1.2;

            var st = new ScaleTransform(scale, scale);
            MainBorder.LayoutTransform = st;
            
            this.Width = 300 * scale;
            this.Height = 117 * scale;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _macroService.RegisterHotkeys(_settings);
        }

        // --- Settings Window ---
        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Close();
                return;
            }

            _settingsWindow = new SettingsWindow(_settings, _macroService, this);
            _settingsWindow.Owner = this;
            _settingsWindow.Closed += (s, args) => { _settingsWindow = null; };
            _settingsWindow.Show();
        }

        // --- Notifications ---
        private async void ShowNotification(string text)
        {
            TxtNotification.Text = text;
            NotificationPopup.IsOpen = true;
            await Task.Delay(2000);
            NotificationPopup.IsOpen = false;
        }

        private void NotificationPopup_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            NotificationPopup.IsOpen = false;
        }

        // --- Status ---
        private void UpdateStatus(string message, bool isRecording, bool isPlaying)
        {
            Dispatcher.Invoke(() =>
            {
                if (message.Contains("Stored") || message == "Ready")
                {
                    if (message.Contains("Stored")) _macroName = "Unsaved";
                    RefreshTitleBar();
                }
                if (message == "Ready" && _macroService.GetEventCount() == 0)
                {
                    _macroName = "None";
                    RefreshTitleBar();
                }

                if (isRecording) {
                    MainBorder.BorderBrush = RecordingBrush;
                }
                else if (isPlaying) {
                    MainBorder.BorderBrush = PlayingBrush;
                }
                else {
                    MainBorder.BorderBrush = (Brush)Application.Current.Resources["ThemeCardBorder"];
                }

                bool busy = isRecording || isPlaying;
                BtnLoad.IsEnabled = !busy;
                BtnSave.IsEnabled = !busy;
                BtnPrefs.IsEnabled = !busy;
                
                BtnRec.IsEnabled = !isPlaying; 
                BtnPlay.IsEnabled = !isRecording;

                // Start/stop recording timer
                if (isRecording)
                    _recordingTimer.Start();
                else
                    _recordingTimer.Stop();

                // Start/stop countdown timer
                if (isPlaying)
                    _countdownTimer.Start();
                else
                {
                    _countdownTimer.Stop();
                    RefreshTitleBar();
                }
            });
        }

        // --- File Operations ---
        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "KeyR Files (*.tt2)|*.tt2|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string content = TT2FileManager.Load(dlg.FileName);
                    _macroService.Deserialize(content);
                    _macroName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    RefreshTitleBar();
                    ShowNotification("Macro Loaded!");
                }
                catch
                {
                    ShowNotification("Failed to Load Data");
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "KeyR Files (*.tt2)|*.tt2", DefaultExt = ".tt2" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string content = _macroService.Serialize();
                    TT2FileManager.Save(dlg.FileName, content);
                    _macroName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                    RefreshTitleBar();
                    ShowNotification("Saved Successfully");
                }
                catch
                {
                    ShowNotification("Failed to Save");
                }
            }
        }

        // --- Record / Play ---
        private void BtnRec_Click(object sender, RoutedEventArgs e) { _macroService.ToggleRecord(_settings); }
        private void BtnPlay_Click(object sender, RoutedEventArgs e) { _macroService.TogglePlay(_settings); }

        // --- Window Controls ---
        private void BtnClose_Click(object sender, RoutedEventArgs e) 
        { 
            this.Close(); 
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }

        // --- Title Bar ---
        private string FormatDuration(long ms)
        {
            long totalSeconds = ms / 1000;
            long hours = Math.Min(totalSeconds / 3600, 99);
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        public void RefreshTitleBar()
        {
            var count = _macroService.GetEventCount();
            var ms = _macroService.GetDurationMs();

            if (_macroName != "None" && _macroName != "Unsaved" && count > 0)
                TxtTitleName.Text = _macroName;
            else
                TxtTitleName.Text = "KeyR";

            if (count > 0)
                TxtTitleDuration.Text = FormatDuration(ms);
            else
                TxtTitleDuration.Text = "";
        }

        private void RecordingTimer_Tick(object sender, EventArgs e)
        {
            if (!_macroService.IsRecording) { _recordingTimer.Stop(); RefreshTitleBar(); return; }
            long elapsedMs = _macroService.RecordingElapsedMs;
            TxtTitleDuration.Text = FormatDuration(elapsedMs);
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            if (!_macroService.IsPlaying) { _countdownTimer.Stop(); RefreshTitleBar(); return; }

            long totalMs = _macroService.GetDurationMs();
            double speed = _macroService.PlaybackSpeed;
            long adjustedTotal = (long)(totalMs / speed);
            long elapsedMs = _macroService.PlaybackElapsedMs;
            long remaining = Math.Max(0, adjustedTotal - elapsedMs);
            TxtTitleDuration.Text = FormatDuration(remaining);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // --- Tooltip ---
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            var element = Mouse.DirectlyOver as FrameworkElement;
            string tip = null;
            while (element != null)
            {
                if (element.Tag is string tagStr && !string.IsNullOrEmpty(tagStr))
                {
                    tip = tagStr;
                    break;
                }
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            if (tip != null)
            {
                TxtHoverTooltip.Text = tip;
                HoverTooltip.IsOpen = true;
                TxtHoverTooltip.UpdateLayout();
                
                var pos = PointToScreen(e.GetPosition(this));
                double w = TxtHoverTooltip.ActualWidth + 20;
                double h = TxtHoverTooltip.ActualHeight + 12;
                double targetX = pos.X + 15;
                double targetY = pos.Y + 15;
                if (targetX + w > SystemParameters.VirtualScreenWidth) targetX = pos.X - w - 5;
                if (targetY + h > SystemParameters.VirtualScreenHeight) targetY = pos.Y - h - 5;
                
                HoverTooltip.HorizontalOffset = targetX;
                HoverTooltip.VerticalOffset = targetY;
            }
            else
            {
                HoverTooltip.IsOpen = false;
            }
        }
        
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            HoverTooltip.IsOpen = false;
        }

        // --- Window Events ---
        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.IsLoaded)
            {
                _settings.X = this.Left;
                _settings.Y = this.Top;
                _settings.Save();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            PreventMaximize();
        }

        private void PreventMaximize()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int style = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);
            }
        }

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settings.X = this.Left;
            _settings.Y = this.Top;
            _settings.Save();

            _macroService.StopPlaying();
            _macroService.Dispose();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                if (e.Delta > 0) { scrollViewer.LineLeft(); scrollViewer.LineLeft(); }
                else { scrollViewer.LineRight(); scrollViewer.LineRight(); }
                e.Handled = true;
            }
        }
    }
}
