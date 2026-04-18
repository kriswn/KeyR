using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SupTask;

public class MainWindow : Window, IComponentConnector
{
	private Settings _settings;

	private MacroService _macroService;

	private bool _prefsExpanded;

	private bool _isLoaded;

	private bool _isListeningForHotkey;

	private string _macroName = "None";

	private double _originalTopBeforeExpand = double.NaN;

	private bool _positionChangedWhileExpanded;

	private bool _isAnimatingTop;

	private bool _isAnimatingHeight;

	private static readonly SolidColorBrush RecordingBrush = CreateFrozenBrush("#e63946");

	private static readonly SolidColorBrush PlayingBrush = CreateFrozenBrush("#2a9d8f");

	private static readonly SolidColorBrush HotkeyListenBrush = CreateFrozenBrush("#3a86ff");

	private static readonly Regex NumberOnlyRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

	private System.Windows.Controls.TextBox _currentHotkeyBox;

	private const int GWL_STYLE = -16;

	private const int WS_MAXIMIZEBOX = 65536;

	internal Popup NotificationPopup;

	internal TextBlock TxtNotification;

	internal Popup HoverTooltip;

	internal TextBlock TxtHoverTooltip;

	internal Border MainBorder;

	internal Grid MainWindowGrid;

	internal System.Windows.Controls.Button BtnMinimize;

	internal System.Windows.Controls.Button BtnCreativeClose;

	internal System.Windows.Controls.Button BtnLoad;

	internal System.Windows.Controls.Button BtnSave;

	internal System.Windows.Controls.Button BtnRec;

	internal Ellipse RecIndicator;

	internal System.Windows.Controls.Button BtnPlay;

	internal Polygon PlayIndicator;

	internal System.Windows.Controls.Button BtnPrefs;

	internal Grid PrefsSection;

	internal ScrollViewer PrefsScrollViewer;

	internal Grid PrefsInnerGrid;

	internal Border GenericSection;

	internal Grid MacroInfoSection;

	internal TextBlock TxtMacroInfo;

	internal System.Windows.Controls.Button BtnClearMacro;

	internal System.Windows.Controls.CheckBox ChkAlwaysOnTop;

	internal System.Windows.Controls.CheckBox ChkContinuous;

	internal System.Windows.Controls.TextBox TxtLoopCount;

	internal System.Windows.Controls.Button BtnResetSpeed;

	internal System.Windows.Controls.TextBox TxtSpeed;

	internal Border HotkeysBorder;

	internal System.Windows.Controls.TextBox TxtRecHotkey;

	internal System.Windows.Controls.TextBox TxtPlayHotkey;

	internal System.Windows.Controls.TextBox TxtPosX;

	internal System.Windows.Controls.TextBox TxtPosY;

	internal Border GlobalOverlay;

	private bool _contentLoaded;

	private static SolidColorBrush CreateFrozenBrush(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	public MainWindow()
	{
		InitializeComponent();
		Title = "KeyR"; base.Loaded += (s, e) => CheckAndReplaceSupTask(this);
		_settings = Settings.Load();
		_macroService = new MacroService();
		_macroService.OnStatusChanged += UpdateStatus;
		base.Loaded += MainWindow_Loaded; base.Loaded += (s, e) => CheckAndReplaceSupTask(this);
		base.Topmost = _settings.AlwaysOnTop;
		if (_settings.X != -1.0 && _settings.Y != -1.0)
		{
			double virtualScreenWidth = SystemParameters.VirtualScreenWidth;
			double virtualScreenHeight = SystemParameters.VirtualScreenHeight;
			if (_settings.X > 0.0 && _settings.X < virtualScreenWidth - base.Width && _settings.Y > 0.0 && _settings.Y < virtualScreenHeight - base.Height)
			{
				base.WindowStartupLocation = WindowStartupLocation.Manual;
				base.Left = _settings.X;
				base.Top = _settings.Y;
			}
		}
		TxtRecHotkey.Text = _settings.RecHotkey;
		TxtPlayHotkey.Text = _settings.PlayHotkey;
		TxtLoopCount.Text = _settings.LoopCount.ToString();
		TxtSpeed.Text = _settings.CustomSpeed.ToString();
		ChkContinuous.IsChecked = _settings.LoopContinuous;
		ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
		base.ResizeMode = ResizeMode.NoResize;
		_isLoaded = true;
		UpdatePrefUIState();
		UpdatePositionUI();
		ApplyResolutionScaling();
		base.Height = 110.0;
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
		if (!_prefsExpanded)
		{
			base.Height = 110.0 * num;
		}
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		_macroService.RegisterHotkeys(_settings);
	}

	private void BtnTogglePrefs_Click(object sender, RoutedEventArgs e)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		_prefsExpanded = !_prefsExpanded;
		double num = 0.0;
		double scaleY = ((ScaleTransform)MainBorder.LayoutTransform).ScaleY;
		if (_prefsExpanded)
		{
			_isAnimatingHeight = true;
			base.ResizeMode = ResizeMode.CanResize;
			PreventMaximize();
			base.MinHeight = 220.0 * scaleY;
			UpdateLayout();
			PrefsInnerGrid.Measure(new Size(280.0, double.PositiveInfinity));
			Size desiredSize = PrefsInnerGrid.DesiredSize;
			num = desiredSize.Height + 20.0;
			if (num > 450.0)
			{
				num = 450.0;
			}
		}
		else
		{
			if (base.ActualHeight > 110.0)
			{
				_settings.ExpandedHeight = base.ActualHeight;
			}
			base.ResizeMode = ResizeMode.NoResize;
			PreventMaximize();
			base.MinHeight = 110.0;
		}
		double targetWindowHeight = (86.0 + num) * scaleY + 24.0;
		if (_prefsExpanded && _settings.ExpandedHeight > 0.0)
		{
			targetWindowHeight = Math.Max(base.MinHeight, _settings.ExpandedHeight);
		}
		double value = (double.IsNaN(PrefsSection.Height) ? PrefsSection.ActualHeight : PrefsSection.Height);
		BtnPrefs.Background = (_prefsExpanded ? ((SolidColorBrush)new BrushConverter().ConvertFrom("#2a9d8f")) : Brushes.Transparent);
		DoubleAnimation animation = new DoubleAnimation(_prefsExpanded ? ((targetWindowHeight - 24.0) / scaleY - 86.0) : 0.0, TimeSpan.FromSeconds(0.3))
		{
			From = value,
			EasingFunction = new CubicEase
			{
				EasingMode = (_prefsExpanded ? EasingMode.EaseOut : EasingMode.EaseIn)
			}
		};
		_isAnimatingHeight = true;
		DoubleAnimation doubleAnimation = new DoubleAnimation(targetWindowHeight, TimeSpan.FromSeconds(0.3))
		{
			From = base.ActualHeight,
			EasingFunction = new CubicEase
			{
				EasingMode = (_prefsExpanded ? EasingMode.EaseOut : EasingMode.EaseIn)
			}
		};
		doubleAnimation.Completed += delegate
		{
			BeginAnimation(FrameworkElement.HeightProperty, null);
			PrefsSection.BeginAnimation(FrameworkElement.HeightProperty, null);
			base.Height = targetWindowHeight;
			if (_prefsExpanded)
			{
				PrefsSection.Height = double.NaN;
			}
			else
			{
				PrefsSection.Height = 0.0;
			}
			_isAnimatingHeight = false;
		};
		if (_prefsExpanded)
		{
			_positionChangedWhileExpanded = false;
			double num2 = base.Top + targetWindowHeight;
			Rect workArea = SystemParameters.WorkArea;
			double height = workArea.Height;
			if (num2 > height)
			{
				_originalTopBeforeExpand = base.Top;
				double targetTop = Math.Max(0.0, height - targetWindowHeight);
				DoubleAnimation doubleAnimation2 = new DoubleAnimation(targetTop, TimeSpan.FromSeconds(0.3))
				{
					From = base.Top,
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseOut
					}
				};
				_isAnimatingTop = true;
				doubleAnimation2.Completed += delegate
				{
					if (base.Top != targetTop)
					{
						base.Top = targetTop;
					}
					BeginAnimation(Window.TopProperty, null);
					_isAnimatingTop = false;
				};
				BeginAnimation(Window.TopProperty, doubleAnimation2);
			}
			else
			{
				_originalTopBeforeExpand = double.NaN;
			}
		}
		else
		{
			if (!double.IsNaN(_originalTopBeforeExpand) && !_positionChangedWhileExpanded)
			{
				double finalTop = _originalTopBeforeExpand;
				DoubleAnimation doubleAnimation3 = new DoubleAnimation(finalTop, TimeSpan.FromSeconds(0.3))
				{
					From = base.Top,
					EasingFunction = new CubicEase
					{
						EasingMode = EasingMode.EaseIn
					}
				};
				_isAnimatingTop = true;
				doubleAnimation3.Completed += delegate
				{
					if (base.Top != finalTop)
					{
						base.Top = finalTop;
					}
					BeginAnimation(Window.TopProperty, null);
					_isAnimatingTop = false;
				};
				BeginAnimation(Window.TopProperty, doubleAnimation3);
			}
			_originalTopBeforeExpand = double.NaN;
		}
		PrefsSection.BeginAnimation(FrameworkElement.HeightProperty, animation);
		BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation);
	}

	private void PrefsChanged(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			UpdatePrefUIState();
			SaveSettingsFromUI();
		}
	}

	private void UpdatePrefUIState()
	{
		if (TxtLoopCount != null)
		{
			if (ChkContinuous.IsChecked == true)
			{
				TxtLoopCount.Visibility = Visibility.Collapsed;
			}
			else
			{
				TxtLoopCount.Visibility = Visibility.Visible;
			}
		}
	}

	private void UpdatePositionUI()
	{
		if (_isLoaded && TxtPosX != null && TxtPosY != null)
		{
			TxtPosX.Text = ((int)_settings.X).ToString();
			TxtPosY.Text = ((int)_settings.Y).ToString();
		}
	}

	private void SaveSettingsFromUI()
	{
		_settings.RecHotkey = TxtRecHotkey.Text.Trim();
		_settings.PlayHotkey = TxtPlayHotkey.Text.Trim();
		if (int.TryParse(TxtLoopCount.Text, out var result) && result > 0)
		{
			_settings.LoopCount = result;
		}
		if (double.TryParse(TxtSpeed.Text, out var result2) && result2 > 0.0)
		{
			_settings.CustomSpeed = result2;
		}
		_settings.UseCustomSpeed = true;
		_settings.LoopContinuous = ChkContinuous.IsChecked == true;
		_settings.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
		if (double.TryParse(TxtPosX.Text, out var result3))
		{
			double num = SystemParameters.VirtualScreenWidth - base.ActualWidth;
			if (result3 < 0.0)
			{
				result3 = 0.0;
			}
			if (result3 > num)
			{
				result3 = Math.Max(0.0, num);
			}
			_settings.X = result3;
			base.Left = result3;
		}
		if (double.TryParse(TxtPosY.Text, out var result4))
		{
			double num2 = SystemParameters.VirtualScreenHeight - base.ActualHeight;
			if (result4 < 0.0)
			{
				result4 = 0.0;
			}
			if (result4 > num2)
			{
				result4 = Math.Max(0.0, num2);
			}
			_settings.Y = result4;
			base.Top = result4;
		}
		base.Topmost = _settings.AlwaysOnTop;
		_macroService.RegisterHotkeys(_settings);
		_settings.Save();
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
				RefreshMacroInfo();
			}
			if (message == "Ready" && _macroService.GetEventCount() == 0)
			{
				_macroName = "None";
				MacroInfoSection.Visibility = Visibility.Collapsed;
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
				LinearGradientBrush borderBrush = (LinearGradientBrush)base.Resources["CardBorder"];
				MainBorder.BorderBrush = borderBrush;
			}
			bool flag = isRecording || isPlaying;
			BtnLoad.IsEnabled = !flag;
			BtnSave.IsEnabled = !flag;
			BtnPrefs.IsEnabled = !flag;
			BtnRec.IsEnabled = !isPlaying;
			BtnPlay.IsEnabled = !isRecording;
			PrefsSection.IsEnabled = !flag;
			PrefsSection.Opacity = (flag ? 0.4 : 1.0);
		});
	}

	private void BtnLoad_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
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
				RefreshMacroInfo();
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
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
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
				RefreshMacroInfo();
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
		if (!_isListeningForHotkey)
		{
			_macroService.ToggleRecord(_settings);
		}
	}

	private void BtnPlay_Click(object sender, RoutedEventArgs e)
	{
		if (!_isListeningForHotkey)
		{
			_macroService.TogglePlay(_settings);
		}
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
		if (ms >= 1000)
		{
			double num = (double)ms / 1000.0;
			if (!(num < 60.0))
			{
				double num2 = num / 60.0;
				if (!(num2 < 60.0))
				{
					double value = num2 / 60.0;
					return $"{value:0.#}h";
				}
				return $"{num2:0.#}m";
			}
			return $"{num:0.#}s";
		}
		return $"{ms}ms";
	}

	private void RefreshMacroInfo()
	{
		long durationMs = _macroService.GetDurationMs();
		if (_macroService.GetEventCount() > 0 || (_macroName != "None" && _macroName != "Unsaved"))
		{
			TxtMacroInfo.Text = _macroName + " (" + FormatDuration(durationMs) + ")";
			MacroInfoSection.Visibility = Visibility.Visible;
		}
		else
		{
			MacroInfoSection.Visibility = Visibility.Collapsed;
		}
	}

	private void BtnClearMacro_Click(object sender, RoutedEventArgs e)
	{
		_macroService.ClearEvents();
		_macroName = "None";
		MacroInfoSection.Visibility = Visibility.Collapsed;
	}

	private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		for (DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null); val != null; val = VisualTreeHelper.GetParent(val))
		{
			if (val is System.Windows.Controls.Primitives.ButtonBase || val is System.Windows.Controls.TextBox || val is Thumb)
			{
				return;
			}
		}
		DragMove();
	}

	protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
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
			Point val = PointToScreen(e.GetPosition(this));
			double num = (double)TxtHoverTooltip.Text.Length * 6.5 + 20.0;
			double num2 = 28.0;
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

	protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		HoverTooltip.IsOpen = false;
	}

	private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
	{
		_isListeningForHotkey = true;
		GlobalOverlay.Visibility = Visibility.Visible;
		MainBorder.BorderBrush = HotkeyListenBrush;
		_currentHotkeyBox = sender as System.Windows.Controls.TextBox;
		_macroService.SuspendHotkeys();
	}

	private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
	{
		EndHotkeyListening();
	}

	private void EndHotkeyListening()
	{
		if (_isListeningForHotkey)
		{
			_isListeningForHotkey = false;
			GlobalOverlay.Visibility = Visibility.Collapsed;
			_currentHotkeyBox = null;
			Focus();
			Task.Run(delegate
			{
				Thread.Sleep(50);
				BypassInput.SendKey(17, isDown: false);
				BypassInput.SendKey(18, isDown: false);
				BypassInput.SendKey(16, isDown: false);
			});
			_macroService.ResumeHotkeys();
			UpdateStatus("", _macroService.IsRecording, _macroService.IsPlaying);
		}
	}

	private void TxtHotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Invalid comparison between Unknown and I4
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Invalid comparison between Unknown and I4
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Invalid comparison between Unknown and I4
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Invalid comparison between Unknown and I4
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Invalid comparison between Unknown and I4
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Invalid comparison between Unknown and I4
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Invalid comparison between Unknown and I4
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		e.Handled = true;
		Key val = (((int)e.Key == 156) ? e.SystemKey : e.Key);
		if ((int)val == 13)
		{
			EndHotkeyListening();
		}
		else if ((int)val != 118 && (int)val != 119 && (int)val != 116 && (int)val != 117 && (int)val != 120 && (int)val != 121 && (int)val != 70 && (int)val != 71 && (int)val != 5 && (int)val != 171 && (int)val != 72)
		{
			Keys keys = (Keys)KeyInterop.VirtualKeyFromKey(val);
			ModifierKeys modifiers = Keyboard.Modifiers;
			string text = "";
			if (((Enum)modifiers).HasFlag((Enum)(object)(ModifierKeys)2))
			{
				text += "Control+";
			}
			if (((Enum)modifiers).HasFlag((Enum)(object)(ModifierKeys)1))
			{
				text += "Alt+";
			}
			if (((Enum)modifiers).HasFlag((Enum)(object)(ModifierKeys)4))
			{
				text += "Shift+";
			}
			text += keys;
			if (sender is System.Windows.Controls.TextBox textBox)
			{
				textBox.Text = text;
				PrefsChanged(null, null);
				EndHotkeyListening();
			}
		}
	}

	private void Window_LocationChanged(object sender, EventArgs e)
	{
		if (base.IsLoaded && !_isAnimatingTop)
		{
			if (_prefsExpanded)
			{
				_positionChangedWhileExpanded = true;
			}
			_settings.X = base.Left;
			_settings.Y = base.Top;
			UpdatePositionUI();
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

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (!_isLoaded)
		{
			return;
		}
		double scaleY = ((ScaleTransform)MainBorder.LayoutTransform).ScaleY;
		if (_prefsExpanded && !_isAnimatingHeight && e.HeightChanged)
		{
			_settings.ExpandedHeight = base.ActualHeight;
			if (base.ActualHeight < 125.0 * scaleY && !_isAnimatingHeight)
			{
				_prefsExpanded = false;
				base.ResizeMode = ResizeMode.NoResize;
				PreventMaximize();
				base.MinHeight = 110.0 * scaleY;
				PrefsSection.Height = 0.0;
				BtnPrefs.Background = Brushes.Transparent;
				_settings.Save();
			}
		}
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		if (_isLoaded)
		{
			SaveSettingsFromUI();
		}
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

	private void BtnMoveLeft_Click(object sender, RoutedEventArgs e)
	{
		base.Left = 0.0;
	}

	private void BtnMoveHCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Left = (workArea.Width - base.ActualWidth) / 2.0;
	}

	private void BtnMoveRight_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Left = workArea.Width - base.ActualWidth;
	}

	private void BtnMoveTop_Click(object sender, RoutedEventArgs e)
	{
		base.Top = 0.0;
	}

	private void BtnMoveVCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Top = (workArea.Height - base.ActualHeight) / 2.0;
	}

	private void BtnMoveBottom_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Top = workArea.Height - base.ActualHeight;
	}

	private void BtnResetSpeed_Click(object sender, RoutedEventArgs e)
	{
		if (TxtSpeed != null)
		{
			TxtSpeed.Text = "1";
			PrefsChanged(null, null);
		}
	}

	private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "JSON Files (*.json)|*.json",
			DefaultExt = ".json"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			SaveSettingsFromUI();
			string directoryName = System.IO.Path.GetDirectoryName(saveFileDialog.FileName);
			if (directoryName != null && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(saveFileDialog.FileName, contents);
			ShowNotification("Settings Exported");
		}
		catch
		{
			ShowNotification("Failed to Export");
		}
	}

	private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			Settings settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(openFileDialog.FileName));
			if (settings != null)
			{
				_settings = settings;
				TxtRecHotkey.Text = _settings.RecHotkey;
				TxtPlayHotkey.Text = _settings.PlayHotkey;
				TxtLoopCount.Text = _settings.LoopCount.ToString();
				TxtSpeed.Text = _settings.CustomSpeed.ToString();
				ChkContinuous.IsChecked = _settings.LoopContinuous;
				ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
				if (_settings.X >= 0.0)
				{
					base.Left = _settings.X;
					TxtPosX.Text = ((int)base.Left).ToString();
				}
				if (_settings.Y >= 0.0)
				{
					base.Top = _settings.Y;
					TxtPosY.Text = ((int)base.Top).ToString();
				}
				base.Topmost = _settings.AlwaysOnTop;
				_macroService.RegisterHotkeys(_settings);
				_settings.Save();
				UpdatePrefUIState();
				ShowNotification("Settings Imported");
			}
		}
		catch
		{
			ShowNotification("Failed to Import");
		}
	}

	private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		e.Handled = !NumberOnlyRegex.IsMatch(e.Text);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/mainwindow.xaml", UriKind.Relative);
			System.Windows.Application.LoadComponent(this, resourceLocator);
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
			((MainWindow)target).PreviewMouseLeftButtonDown += Window_PreviewMouseLeftButtonDown;
			((MainWindow)target).Closing += Window_Closing;
			((MainWindow)target).LocationChanged += Window_LocationChanged;
			((MainWindow)target).StateChanged += Window_StateChanged;
			((MainWindow)target).SizeChanged += Window_SizeChanged;
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
			MainWindowGrid = (Grid)target;
			break;
		case 9:
			BtnMinimize = (System.Windows.Controls.Button)target;
			BtnMinimize.Click += BtnMinimize_Click;
			break;
		case 10:
			BtnCreativeClose = (System.Windows.Controls.Button)target;
			BtnCreativeClose.Click += BtnClose_Click;
			break;
		case 11:
			((ScrollViewer)target).PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
			break;
		case 12:
			BtnLoad = (System.Windows.Controls.Button)target;
			BtnLoad.Click += BtnLoad_Click;
			break;
		case 13:
			BtnSave = (System.Windows.Controls.Button)target;
			BtnSave.Click += BtnSave_Click;
			break;
		case 14:
			BtnRec = (System.Windows.Controls.Button)target;
			BtnRec.Click += BtnRec_Click;
			break;
		case 15:
			RecIndicator = (Ellipse)target;
			break;
		case 16:
			BtnPlay = (System.Windows.Controls.Button)target;
			BtnPlay.Click += BtnPlay_Click;
			break;
		case 17:
			PlayIndicator = (Polygon)target;
			break;
		case 18:
			BtnPrefs = (System.Windows.Controls.Button)target;
			BtnPrefs.Click += BtnTogglePrefs_Click;
			break;
		case 19:
			PrefsSection = (Grid)target;
			break;
		case 20:
			PrefsScrollViewer = (ScrollViewer)target;
			break;
		case 21:
			PrefsInnerGrid = (Grid)target;
			break;
		case 22:
			GenericSection = (Border)target;
			break;
		case 23:
			MacroInfoSection = (Grid)target;
			break;
		case 24:
			TxtMacroInfo = (TextBlock)target;
			break;
		case 25:
			BtnClearMacro = (System.Windows.Controls.Button)target;
			BtnClearMacro.Click += BtnClearMacro_Click;
			break;
		case 26:
			ChkAlwaysOnTop = (System.Windows.Controls.CheckBox)target;
			ChkAlwaysOnTop.Checked += PrefsChanged;
			ChkAlwaysOnTop.Unchecked += PrefsChanged;
			break;
		case 27:
			ChkContinuous = (System.Windows.Controls.CheckBox)target;
			ChkContinuous.Checked += PrefsChanged;
			ChkContinuous.Unchecked += PrefsChanged;
			break;
		case 28:
			TxtLoopCount = (System.Windows.Controls.TextBox)target;
			TxtLoopCount.LostFocus += PrefsChanged;
			TxtLoopCount.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 29:
			BtnResetSpeed = (System.Windows.Controls.Button)target;
			BtnResetSpeed.Click += BtnResetSpeed_Click;
			break;
		case 30:
			TxtSpeed = (System.Windows.Controls.TextBox)target;
			TxtSpeed.LostFocus += PrefsChanged;
			TxtSpeed.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 31:
			HotkeysBorder = (Border)target;
			break;
		case 32:
			TxtRecHotkey = (System.Windows.Controls.TextBox)target;
			TxtRecHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtRecHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtRecHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 33:
			TxtPlayHotkey = (System.Windows.Controls.TextBox)target;
			TxtPlayHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtPlayHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtPlayHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 34:
			TxtPosX = (System.Windows.Controls.TextBox)target;
			TxtPosX.LostFocus += PrefsChanged;
			break;
		case 35:
			TxtPosY = (System.Windows.Controls.TextBox)target;
			TxtPosY.LostFocus += PrefsChanged;
			break;
		case 36:
			((System.Windows.Controls.Button)target).Click += BtnMoveLeft_Click;
			break;
		case 37:
			((System.Windows.Controls.Button)target).Click += BtnMoveHCenter_Click;
			break;
		case 38:
			((System.Windows.Controls.Button)target).Click += BtnMoveRight_Click;
			break;
		case 39:
			((System.Windows.Controls.Button)target).Click += BtnMoveTop_Click;
			break;
		case 40:
			((System.Windows.Controls.Button)target).Click += BtnMoveVCenter_Click;
			break;
		case 41:
			((System.Windows.Controls.Button)target).Click += BtnMoveBottom_Click;
			break;
		case 42:
			((System.Windows.Controls.Button)target).Click += BtnImportSettings_Click;
			break;
		case 43:
			((System.Windows.Controls.Button)target).Click += BtnExportSettings_Click;
			break;
		case 44:
			GlobalOverlay = (Border)target;
			GlobalOverlay.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
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

