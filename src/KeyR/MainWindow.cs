using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SupTask;

public class MainWindow : Window, IComponentConnector
{
	private Settings _settings;

	private MacroService _macroService;

	private bool _isLoaded;

	private string _macroName = "None";

	private SettingsWindow _settingsWindow;

	private DispatcherTimer _countdownTimer;

	private DispatcherTimer _recordingTimer;

	private static readonly SolidColorBrush RecordingBrush = CreateFrozenBrush("#e63946");

	private static readonly SolidColorBrush PlayingBrush = CreateFrozenBrush("#2a9d8f");

	private static readonly Regex NumberOnlyRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

	private const int GWL_STYLE = -16;

	private const int WS_MAXIMIZEBOX = 65536;

	internal Popup NotificationPopup;

	internal TextBlock TxtNotification;

	internal Popup HoverTooltip;

	internal TextBlock TxtHoverTooltip;

	internal Border MainBorder;

	internal TextBlock TxtTitleName;

	internal TextBlock TxtTitleDuration;

	internal Button BtnLoad;

	internal Button BtnSave;

	internal Button BtnRec;

	internal Ellipse RecIndicator;

	internal Button BtnPlay;

	internal Polygon PlayIndicator;

	internal Button BtnPrefs;

	private bool _contentLoaded;

	public Settings AppSettings => _settings;

	public MacroService AppMacroService => _macroService;

	private static SolidColorBrush CreateFrozenBrush(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	public MainWindow()
	{
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Expected O, but got Unknown
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Expected O, but got Unknown
		InitializeComponent();
		Title = "KeyR"; base.Loaded += (s, e) => CheckAndReplaceSupTask(this);
		_settings = Settings.Load();
		_macroService = new MacroService();
		_macroService.OnStatusChanged += UpdateStatus;
		base.Loaded += MainWindow_Loaded; base.Loaded += (s, e) => CheckAndReplaceSupTask(this);
		base.Topmost = _settings.AlwaysOnTop;
		_macroService.RegisterHotkeys(_settings);
		if (_settings.X != -1.0 && _settings.Y != -1.0)
		{
			double virtualScreenWidth = SystemParameters.VirtualScreenWidth;
			double virtualScreenHeight = SystemParameters.VirtualScreenHeight;
			if (_settings.X >= 0.0 && _settings.X <= virtualScreenWidth - base.Width && _settings.Y >= 0.0 && _settings.Y <= virtualScreenHeight - base.Height)
			{
				base.WindowStartupLocation = WindowStartupLocation.Manual;
				base.Left = _settings.X;
				base.Top = _settings.Y;
			}
		}
		_isLoaded = true;
		ApplyResolutionScaling();
		_countdownTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100L, 0L)
		};
		_countdownTimer.Tick += CountdownTimer_Tick;
		_recordingTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100L, 0L)
		};
		_recordingTimer.Tick += RecordingTimer_Tick;
	}

	private void ApplyResolutionScaling()
	{
		double num = SystemParameters.PrimaryScreenHeight / 1080.0;
		if (num < 0.8)
		{
			num = 0.8;
		}
		if (num > 1.2)
		{
			num = 1.2;
		}
		ScaleTransform layoutTransform = new ScaleTransform(num, num);
		MainBorder.LayoutTransform = layoutTransform;
		base.Width = 300.0 * num;
		base.Height = 117.0 * num;
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		_macroService.RegisterHotkeys(_settings);
	}

	private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
	{
		if (_settingsWindow != null)
		{
			_settingsWindow.Close();
			return;
		}
		_settingsWindow = new SettingsWindow(_settings, _macroService, this);
		_settingsWindow.Owner = this;
		_settingsWindow.Closed += delegate
		{
			_settingsWindow = null;
		};
		_settingsWindow.Show();
	}

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

	private void UpdateStatus(string message, bool isRecording, bool isPlaying)
	{
		((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
		{
			if (message.Contains("Stored") || message == "Ready")
			{
				if (message.Contains("Stored"))
				{
					_macroName = "Unsaved";
				}
				RefreshTitleBar();
			}
			if (message == "Ready" && _macroService.GetEventCount() == 0)
			{
				_macroName = "None";
				RefreshTitleBar();
			}
			if (isRecording)
			{
				MainBorder.BorderBrush = RecordingBrush;
			}
			else if (isPlaying)
			{
				MainBorder.BorderBrush = PlayingBrush;
			}
			else
			{
				MainBorder.BorderBrush = (Brush)Application.Current.Resources["ThemeCardBorder"];
			}
			bool flag = isRecording || isPlaying;
			BtnLoad.IsEnabled = !flag;
			BtnSave.IsEnabled = !flag;
			BtnPrefs.IsEnabled = !flag;
			BtnRec.IsEnabled = !isPlaying;
			BtnPlay.IsEnabled = !isRecording;
			if (isRecording)
			{
				_recordingTimer.Start();
			}
			else
			{
				_recordingTimer.Stop();
			}
			if (isPlaying)
			{
				_countdownTimer.Start();
			}
			else
			{
				_countdownTimer.Stop();
				RefreshTitleBar();
			}
		});
	}

	private void BtnLoad_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "KeyR Files (*.tt2)|*.tt2|All Files (*.*)|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			try
			{
				string json = TT2FileManager.Load(openFileDialog.FileName);
				_macroService.Deserialize(json);
				_macroName = System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName);
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
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "KeyR Files (*.tt2)|*.tt2",
			DefaultExt = ".tt2"
		};
		if (saveFileDialog.ShowDialog() == true)
		{
			try
			{
				string jsonContent = _macroService.Serialize();
				TT2FileManager.Save(saveFileDialog.FileName, jsonContent);
				_macroName = System.IO.Path.GetFileNameWithoutExtension(saveFileDialog.FileName);
				RefreshTitleBar();
				ShowNotification("Saved Successfully");
			}
			catch
			{
				ShowNotification("Failed to Save");
			}
		}
	}

	private void BtnRec_Click(object sender, RoutedEventArgs e)
	{
		_macroService.ToggleRecord(_settings);
	}

	private void BtnPlay_Click(object sender, RoutedEventArgs e)
	{
		_macroService.TogglePlay(_settings);
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void BtnMinimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private string FormatDuration(long ms)
	{
		long num = ms / 1000;
		long value = Math.Min(num / 3600, 99L);
		long value2 = num % 3600 / 60;
		long value3 = num % 60;
		return $"{value:D2}:{value2:D2}:{value3:D2}";
	}

	public void RefreshTitleBar()
	{
		int eventCount = _macroService.GetEventCount();
		long durationMs = _macroService.GetDurationMs();
		if (_macroName != "None" && _macroName != "Unsaved" && eventCount > 0)
		{
			TxtTitleName.Text = _macroName;
		}
		else
		{
			TxtTitleName.Text = "KeyR";
		}
		if (eventCount > 0)
		{
			TxtTitleDuration.Text = FormatDuration(durationMs);
		}
		else
		{
			TxtTitleDuration.Text = "";
		}
	}

	private void RecordingTimer_Tick(object sender, EventArgs e)
	{
		if (!_macroService.IsRecording)
		{
			_recordingTimer.Stop();
			RefreshTitleBar();
		}
		else
		{
			long recordingElapsedMs = _macroService.RecordingElapsedMs;
			TxtTitleDuration.Text = FormatDuration(recordingElapsedMs);
		}
	}

	private void CountdownTimer_Tick(object sender, EventArgs e)
	{
		if (!_macroService.IsPlaying)
		{
			_countdownTimer.Stop();
			RefreshTitleBar();
			return;
		}
		long durationMs = _macroService.GetDurationMs();
		double playbackSpeed = _macroService.PlaybackSpeed;
		long num = (long)((double)durationMs / playbackSpeed);
		long playbackElapsedMs = _macroService.PlaybackElapsedMs;
		long ms = Math.Max(0L, num - playbackElapsedMs);
		TxtTitleDuration.Text = FormatDuration(ms);
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			DragMove();
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		base.OnMouseMove(e);
		FrameworkElement frameworkElement = Mouse.DirectlyOver as FrameworkElement;
		string text = null;
		while (frameworkElement != null)
		{
			if (frameworkElement.Tag is string text2 && !string.IsNullOrEmpty(text2))
			{
				text = text2;
				break;
			}
			frameworkElement = VisualTreeHelper.GetParent((DependencyObject)(object)frameworkElement) as FrameworkElement;
		}
		if (text != null)
		{
			TxtHoverTooltip.Text = text;
			HoverTooltip.IsOpen = true;
			TxtHoverTooltip.UpdateLayout();
			Point val = PointToScreen(e.GetPosition(this));
			double num = TxtHoverTooltip.ActualWidth + 20.0;
			double num2 = TxtHoverTooltip.ActualHeight + 12.0;
			double num3 = val.X + 15.0;
			double num4 = val.Y + 15.0;
			if (num3 + num > SystemParameters.VirtualScreenWidth)
			{
				num3 = val.X - num - 5.0;
			}
			if (num4 + num2 > SystemParameters.VirtualScreenHeight)
			{
				num4 = val.Y - num2 - 5.0;
			}
			HoverTooltip.HorizontalOffset = num3;
			HoverTooltip.VerticalOffset = num4;
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

	private void Window_LocationChanged(object sender, EventArgs e)
	{
		if (base.IsLoaded)
		{
			_settings.X = base.Left;
			_settings.Y = base.Top;
			_settings.Save();
		}
	}

	private void Window_StateChanged(object sender, EventArgs e)
	{
		if (base.WindowState == WindowState.Maximized)
		{
			base.WindowState = WindowState.Normal;
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		PreventMaximize();
	}

	private void PreventMaximize()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle != IntPtr.Zero)
		{
			int windowLong = GetWindowLong(handle, -16);
			SetWindowLong(handle, -16, windowLong & -65537);
		}
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int GetWindowLong(nint hWnd, int nIndex);

	[DllImport("user32.dll")]
	private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		_settings.X = base.Left;
		_settings.Y = base.Top;
		_settings.Save();
		_macroService.StopPlaying();
		_macroService.Dispose();
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			if (e.Delta > 0)
			{
				scrollViewer.LineLeft();
				scrollViewer.LineLeft();
			}
			else
			{
				scrollViewer.LineRight();
				scrollViewer.LineRight();
			}
			e.Handled = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((MainWindow)target).Closing += Window_Closing;
			((MainWindow)target).LocationChanged += Window_LocationChanged;
			((MainWindow)target).StateChanged += Window_StateChanged;
			break;
		case 2:
			NotificationPopup = (Popup)target;
			break;
		case 3:
			((Border)target).MouseLeftButtonDown += NotificationPopup_MouseLeftButtonDown;
			break;
		case 4:
			TxtNotification = (TextBlock)target;
			break;
		case 5:
			HoverTooltip = (Popup)target;
			break;
		case 6:
			TxtHoverTooltip = (TextBlock)target;
			break;
		case 7:
			MainBorder = (Border)target;
			break;
		case 8:
			((Grid)target).MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
			break;
		case 9:
			TxtTitleName = (TextBlock)target;
			break;
		case 10:
			TxtTitleDuration = (TextBlock)target;
			break;
		case 11:
			((Button)target).Click += BtnMinimize_Click;
			break;
		case 12:
			((Button)target).Click += BtnClose_Click;
			break;
		case 13:
			((ScrollViewer)target).PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
			break;
		case 14:
			BtnLoad = (Button)target;
			BtnLoad.Click += BtnLoad_Click;
			break;
		case 15:
			BtnSave = (Button)target;
			BtnSave.Click += BtnSave_Click;
			break;
		case 16:
			BtnRec = (Button)target;
			BtnRec.Click += BtnRec_Click;
			break;
		case 17:
			RecIndicator = (Ellipse)target;
			break;
		case 18:
			BtnPlay = (Button)target;
			BtnPlay.Click += BtnPlay_Click;
			break;
		case 19:
			PlayIndicator = (Polygon)target;
			break;
		case 20:
			BtnPrefs = (Button)target;
			BtnPrefs.Click += BtnOpenSettings_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

    private void CheckAndReplaceSupTask(System.Windows.DependencyObject parent) {
        if (parent == null) return;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++) {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.TextBlock tb && tb.Text != null && tb.Text.Contains("SupTask")) {
                tb.Text = tb.Text.Replace("SupTask", "KeyR");
            }
            CheckAndReplaceSupTask(child);
        }
    }
}

