using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
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

public class MainWindow : Window, IComponentConnector, IStyleConnector
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

	private ConditionEditorWindow _addWindow;

	private Dictionary<RestartCondition, ConditionEditorWindow> _editWindows = new Dictionary<RestartCondition, ConditionEditorWindow>();

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

	internal System.Windows.Controls.CheckBox ChkShowConfirmations;

	internal System.Windows.Controls.CheckBox ChkContinuous;

	internal System.Windows.Controls.TextBox TxtLoopCount;

	internal System.Windows.Controls.Button BtnResetSpeed;

	internal System.Windows.Controls.TextBox TxtSpeed;

	internal Border HotkeysBorder;

	internal System.Windows.Controls.TextBox TxtRecHotkey;

	internal System.Windows.Controls.TextBox TxtPlayHotkey;

	internal System.Windows.Controls.Button BtnConditionHelp;

	internal System.Windows.Controls.Button BtnToggleMatchLogic;

	internal System.Windows.Controls.Button BtnToggleRestartMode;

	internal System.Windows.Controls.Button BtnToggleRestrictedMode;

	internal System.Windows.Controls.TextBox TxtPollingInterval;

	internal ItemsControl ListConditions;

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
		LoadSettingsToUI();
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
		base.ResizeMode = ResizeMode.NoResize;
		_isLoaded = true;
		UpdatePrefUIState();
		UpdatePositionUI();
		ApplyResolutionScaling();
		base.Height = 117.0;
	}

	private void LoadSettingsToUI()
	{
		TxtRecHotkey.Text = _settings.RecHotkey;
		TxtPlayHotkey.Text = _settings.PlayHotkey;
		TxtLoopCount.Text = _settings.LoopCount.ToString();
		TxtSpeed.Text = _settings.CustomSpeed.ToString();
		ChkContinuous.IsChecked = _settings.LoopContinuous;
		TxtPollingInterval.Text = _settings.ConditionsPollingInterval.ToString();
		ChkShowConfirmations.IsChecked = !_settings.HideDeleteConfirmation;
		ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
		if (BtnConditionHelp != null)
		{
			BtnConditionHelp.Tag = "The macro monitors your screen while playing.\nIf the condition(s) are met, it will immediately\nrestart from the beginning.";
		}
		if (BtnToggleMatchLogic != null)
		{
			BtnToggleMatchLogic.Content = (_settings.MatchAllConditions ? "Match: ALL" : "Match: ANY");
			BtnToggleMatchLogic.Tag = (_settings.MatchAllConditions ? "Macro restarts only when ALL enabled\nconditions are met." : "Macro restarts when ANY enabled condition is met.");
		}
		if (BtnToggleRestartMode != null)
		{
			BtnToggleRestartMode.Content = (_settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE");
			BtnToggleRestartMode.Tag = (_settings.UseSmartRestart ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts." : "Triggers instantly and repeatedly as long as condition is met.");
		}
		if (BtnToggleRestrictedMode != null)
		{
			BtnToggleRestrictedMode.Content = (_settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF");
			BtnToggleRestrictedMode.Tag = (_settings.WaitConditionToRestart ? "Macro pauses at the end of the timeline\nand waits for conditions before looping." : "Macro loops naturally regardless of conditions.");
		}
		UpdatePrefUIState();
		UpdatePositionUI();
		RefreshConditionsList();
		base.Topmost = _settings.AlwaysOnTop;
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
			base.Height = 117.0 * num;
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
		//IL_02f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
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
			if (base.ActualHeight > 117.0)
			{
				_settings.ExpandedHeight = base.ActualHeight;
			}
			base.ResizeMode = ResizeMode.NoResize;
			base.MinHeight = 117.0;
		}
		double targetWindowHeight = (93.0 + num) * scaleY + 24.0;
		if (_prefsExpanded && _settings.ExpandedHeight > 0.0)
		{
			targetWindowHeight = Math.Max(base.MinHeight, _settings.ExpandedHeight);
		}
		double value = (double.IsNaN(PrefsSection.Height) ? PrefsSection.ActualHeight : PrefsSection.Height);
		SolidColorBrush solidColorBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#2a9d8f");
		BtnPrefs.Background = (_prefsExpanded ? solidColorBrush : Brushes.Transparent);
		BtnPrefs.Resources["BtnHover"] = (_prefsExpanded ? ((SolidColorBrush)new BrushConverter().ConvertFrom("#42c2b1")) : ((SolidColorBrush)new BrushConverter().ConvertFrom("#3a3e5c")));
		DoubleAnimation animation = new DoubleAnimation(_prefsExpanded ? ((targetWindowHeight - 24.0) / scaleY - 93.0) : 0.0, TimeSpan.FromSeconds(0.3))
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
		base.Topmost = _settings.AlwaysOnTop;
		_settings.WaitConditionToRestart = BtnToggleRestrictedMode != null && BtnToggleRestrictedMode.Content.ToString().Contains("ON");
		_settings.HideDeleteConfirmation = ChkShowConfirmations.IsChecked == false;
		if (int.TryParse(TxtPollingInterval.Text.Trim(), out var result3))
		{
			if (result3 < 100)
			{
				result3 = 100;
			}
			_settings.ConditionsPollingInterval = result3;
			TxtPollingInterval.Text = result3.ToString();
		}
		if (double.TryParse(TxtPosX.Text, out var result4))
		{
			double num = SystemParameters.VirtualScreenWidth - base.ActualWidth;
			if (result4 < 0.0)
			{
				result4 = 0.0;
			}
			if (result4 > num)
			{
				result4 = Math.Max(0.0, num);
			}
			_settings.X = result4;
			base.Left = result4;
		}
		if (double.TryParse(TxtPosY.Text, out var result5))
		{
			double num2 = SystemParameters.VirtualScreenHeight - base.ActualHeight;
			if (result5 < 0.0)
			{
				result5 = 0.0;
			}
			if (result5 > num2)
			{
				result5 = Math.Max(0.0, num2);
			}
			_settings.Y = result5;
			base.Top = result5;
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
		if (_isLoaded)
		{
			SaveSettingsFromUI();
		}
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
		if (!_settings.HideDeleteConfirmation)
		{
			if (!ThemedConfirmWindow.Show(this, "Are you sure you want to clear the entire macro timeline?", out var dontAskAgain, "Clear"))
			{
				return;
			}
			if (dontAskAgain)
			{
				_settings.HideDeleteConfirmation = true;
				ChkShowConfirmations.IsChecked = false;
				_settings.Save();
			}
		}
		_macroService.ClearEvents();
		_macroName = "None";
		MacroInfoSection.Visibility = Visibility.Collapsed;
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			DragMove();
		}
	}

	protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
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

	private void RefreshHoverTooltip(string newTip)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (HoverTooltip.IsOpen)
		{
			TxtHoverTooltip.Text = newTip;
			TxtHoverTooltip.UpdateLayout();
			double num = TxtHoverTooltip.ActualWidth + 20.0;
			double num2 = TxtHoverTooltip.ActualHeight + 12.0;
			Point val = PointToScreen(Mouse.GetPosition(this));
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
			if (base.ActualHeight < 116.0 * scaleY && !_isAnimatingHeight)
			{
				_prefsExpanded = false;
				base.ResizeMode = ResizeMode.NoResize;
				PreventMaximize();
				base.MinHeight = 117.0 * scaleY;
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
		SaveQuickPos();
	}

	private void BtnMoveHCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Left = (workArea.Width - base.ActualWidth) / 2.0;
		SaveQuickPos();
	}

	private void BtnMoveRight_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Left = workArea.Width - base.ActualWidth;
		SaveQuickPos();
	}

	private void BtnMoveTop_Click(object sender, RoutedEventArgs e)
	{
		base.Top = 0.0;
		SaveQuickPos();
	}

	private void BtnMoveVCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Top = (workArea.Height - base.ActualHeight) / 2.0;
		SaveQuickPos();
	}

	private void BtnMoveBottom_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		base.Top = workArea.Height - base.ActualHeight;
		SaveQuickPos();
	}

	private async void SaveQuickPos()
	{
		_originalTopBeforeExpand = double.NaN;
		_positionChangedWhileExpanded = true;
		await Task.Delay(50);
		_settings.X = base.Left;
		_settings.Y = base.Top;
		UpdatePositionUI();
		_settings.Save();
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
				LoadSettingsToUI();
				_macroService.RegisterHotkeys(_settings);
				_settings.Save();
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

	private void BtnToggleMatchLogic_Click(object sender, RoutedEventArgs e)
	{
		_settings.MatchAllConditions = !_settings.MatchAllConditions;
		string text = (_settings.MatchAllConditions ? "Macro restarts only when ALL enabled\nconditions are met." : "Macro restarts when ANY enabled condition is met.");
		if (BtnToggleMatchLogic != null)
		{
			BtnToggleMatchLogic.Content = (_settings.MatchAllConditions ? "Match: ALL" : "Match: ANY");
			BtnToggleMatchLogic.Tag = text;
			RefreshHoverTooltip(text);
		}
		SaveSettingsFromUI();
	}

	private void BtnToggleRestartMode_Click(object sender, RoutedEventArgs e)
	{
		_settings.UseSmartRestart = !_settings.UseSmartRestart;
		string text = (_settings.UseSmartRestart ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts." : "Triggers instantly and repeatedly as long as condition is met.");
		if (BtnToggleRestartMode != null)
		{
			BtnToggleRestartMode.Content = (_settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE");
			BtnToggleRestartMode.Tag = text;
			RefreshHoverTooltip(text);
		}
		SaveSettingsFromUI();
	}

	private void BtnToggleRestrictedMode_Click(object sender, RoutedEventArgs e)
	{
		_settings.WaitConditionToRestart = !_settings.WaitConditionToRestart;
		string text = (_settings.WaitConditionToRestart ? "Macro pauses at the end of the timeline\nand waits for conditions before looping." : "Macro loops naturally regardless of conditions.");
		if (BtnToggleRestrictedMode != null)
		{
			BtnToggleRestrictedMode.Content = (_settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF");
			BtnToggleRestrictedMode.Tag = text;
			RefreshHoverTooltip(text);
		}
		SaveSettingsFromUI();
	}

	private void RefreshConditionsList()
	{
		if (ListConditions != null)
		{
			ListConditions.ItemsSource = null;
			ListConditions.ItemsSource = _settings.RestartConditions;
		}
	}

	private void ConditionCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: RestartCondition dataContext })
		{
			dataContext.IsEnabled = !dataContext.IsEnabled;
			SaveSettingsFromUI();
			RefreshConditionsList();
			e.Handled = true;
		}
	}

	private void BtnAddCondition_Click(object sender, RoutedEventArgs e)
	{
		if (_addWindow != null)
		{
			if (_addWindow.WindowState == WindowState.Minimized)
			{
				_addWindow.WindowState = WindowState.Normal;
			}
			_addWindow.Activate();
			return;
		}
		_addWindow = new ConditionEditorWindow(new RestartCondition(), "Add Condition", (string name) => IsNameDuplicate(name, null));
		_addWindow.Owner = this;
		_addWindow.Closed += delegate(object? s, EventArgs args)
		{
			ConditionEditorWindow conditionEditorWindow = (ConditionEditorWindow)s;
			if (conditionEditorWindow.IsSaved)
			{
				if (_settings.RestartConditions == null)
				{
					_settings.RestartConditions = new List<RestartCondition>();
				}
				_settings.RestartConditions.Add(conditionEditorWindow.Condition);
				SaveSettingsFromUI();
				RefreshConditionsList();
			}
			_addWindow = null;
		};
		_addWindow.Show();
	}

	private void BtnEditCondition_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: var dataContext }))
		{
			return;
		}
		RestartCondition cond = dataContext as RestartCondition;
		if (cond == null)
		{
			return;
		}
		if (_editWindows.TryGetValue(cond, out ConditionEditorWindow value))
		{
			if (value.WindowState == WindowState.Minimized)
			{
				value.WindowState = WindowState.Normal;
			}
			value.Activate();
			return;
		}
		ConditionEditorWindow conditionEditorWindow = new ConditionEditorWindow(cond, "Edit Condition", (string name) => IsNameDuplicate(name, cond));
		conditionEditorWindow.Owner = this;
		_editWindows[cond] = conditionEditorWindow;
		conditionEditorWindow.Closed += delegate(object? s, EventArgs args)
		{
			if (((ConditionEditorWindow)s).IsSaved)
			{
				SaveSettingsFromUI();
				RefreshConditionsList();
			}
			_editWindows.Remove(cond);
		};
		conditionEditorWindow.Show();
		e.Handled = true;
	}

	private bool IsNameDuplicate(string name, RestartCondition current)
	{
		if (_settings.RestartConditions == null)
		{
			return false;
		}
		foreach (RestartCondition restartCondition in _settings.RestartConditions)
		{
			if (restartCondition != current && restartCondition.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	private void BtnDeleteCondition_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: RestartCondition dataContext }))
		{
			return;
		}
		bool flag = _settings.HideDeleteConfirmation;
		if (!flag)
		{
			flag = ThemedConfirmWindow.Show(this, "Are you sure you want to delete the condition \"" + dataContext.Name + "\"?", out var dontAskAgain);
			if (dontAskAgain && flag)
			{
				_settings.HideDeleteConfirmation = true;
				ChkShowConfirmations.IsChecked = false;
				_settings.Save();
			}
		}
		if (flag)
		{
			_settings.RestartConditions.Remove(dataContext);
			SaveSettingsFromUI();
			RefreshConditionsList();
		}
		e.Handled = true;
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
			((Grid)target).MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
			break;
		case 10:
			((System.Windows.Controls.Button)target).Click += BtnMinimize_Click;
			break;
		case 11:
			((System.Windows.Controls.Button)target).Click += BtnClose_Click;
			break;
		case 12:
			((ScrollViewer)target).PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
			break;
		case 13:
			BtnLoad = (System.Windows.Controls.Button)target;
			BtnLoad.Click += BtnLoad_Click;
			break;
		case 14:
			BtnSave = (System.Windows.Controls.Button)target;
			BtnSave.Click += BtnSave_Click;
			break;
		case 15:
			BtnRec = (System.Windows.Controls.Button)target;
			BtnRec.Click += BtnRec_Click;
			break;
		case 16:
			RecIndicator = (Ellipse)target;
			break;
		case 17:
			BtnPlay = (System.Windows.Controls.Button)target;
			BtnPlay.Click += BtnPlay_Click;
			break;
		case 18:
			PlayIndicator = (Polygon)target;
			break;
		case 19:
			BtnPrefs = (System.Windows.Controls.Button)target;
			BtnPrefs.Click += BtnTogglePrefs_Click;
			break;
		case 20:
			PrefsSection = (Grid)target;
			break;
		case 21:
			PrefsScrollViewer = (ScrollViewer)target;
			break;
		case 22:
			PrefsInnerGrid = (Grid)target;
			break;
		case 23:
			GenericSection = (Border)target;
			break;
		case 24:
			MacroInfoSection = (Grid)target;
			break;
		case 25:
			TxtMacroInfo = (TextBlock)target;
			break;
		case 26:
			BtnClearMacro = (System.Windows.Controls.Button)target;
			BtnClearMacro.Click += BtnClearMacro_Click;
			break;
		case 27:
			ChkAlwaysOnTop = (System.Windows.Controls.CheckBox)target;
			ChkAlwaysOnTop.Checked += PrefsChanged;
			ChkAlwaysOnTop.Unchecked += PrefsChanged;
			break;
		case 28:
			ChkShowConfirmations = (System.Windows.Controls.CheckBox)target;
			ChkShowConfirmations.Checked += PrefsChanged;
			ChkShowConfirmations.Unchecked += PrefsChanged;
			break;
		case 29:
			ChkContinuous = (System.Windows.Controls.CheckBox)target;
			ChkContinuous.Checked += PrefsChanged;
			ChkContinuous.Unchecked += PrefsChanged;
			break;
		case 30:
			TxtLoopCount = (System.Windows.Controls.TextBox)target;
			TxtLoopCount.LostFocus += PrefsChanged;
			TxtLoopCount.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 31:
			BtnResetSpeed = (System.Windows.Controls.Button)target;
			BtnResetSpeed.Click += BtnResetSpeed_Click;
			break;
		case 32:
			TxtSpeed = (System.Windows.Controls.TextBox)target;
			TxtSpeed.LostFocus += PrefsChanged;
			TxtSpeed.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 33:
			HotkeysBorder = (Border)target;
			break;
		case 34:
			TxtRecHotkey = (System.Windows.Controls.TextBox)target;
			TxtRecHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtRecHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtRecHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 35:
			TxtPlayHotkey = (System.Windows.Controls.TextBox)target;
			TxtPlayHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtPlayHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtPlayHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 36:
			BtnConditionHelp = (System.Windows.Controls.Button)target;
			break;
		case 37:
			BtnToggleMatchLogic = (System.Windows.Controls.Button)target;
			BtnToggleMatchLogic.Click += BtnToggleMatchLogic_Click;
			break;
		case 38:
			BtnToggleRestartMode = (System.Windows.Controls.Button)target;
			BtnToggleRestartMode.Click += BtnToggleRestartMode_Click;
			break;
		case 39:
			BtnToggleRestrictedMode = (System.Windows.Controls.Button)target;
			BtnToggleRestrictedMode.Click += BtnToggleRestrictedMode_Click;
			break;
		case 40:
			TxtPollingInterval = (System.Windows.Controls.TextBox)target;
			TxtPollingInterval.LostFocus += PrefsChanged;
			TxtPollingInterval.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 41:
			ListConditions = (ItemsControl)target;
			break;
		case 46:
			((System.Windows.Controls.Button)target).Click += BtnAddCondition_Click;
			break;
		case 47:
			TxtPosX = (System.Windows.Controls.TextBox)target;
			TxtPosX.LostFocus += PrefsChanged;
			break;
		case 48:
			TxtPosY = (System.Windows.Controls.TextBox)target;
			TxtPosY.LostFocus += PrefsChanged;
			break;
		case 49:
			((System.Windows.Controls.Button)target).Click += BtnMoveLeft_Click;
			break;
		case 50:
			((System.Windows.Controls.Button)target).Click += BtnMoveHCenter_Click;
			break;
		case 51:
			((System.Windows.Controls.Button)target).Click += BtnMoveRight_Click;
			break;
		case 52:
			((System.Windows.Controls.Button)target).Click += BtnMoveTop_Click;
			break;
		case 53:
			((System.Windows.Controls.Button)target).Click += BtnMoveVCenter_Click;
			break;
		case 54:
			((System.Windows.Controls.Button)target).Click += BtnMoveBottom_Click;
			break;
		case 55:
			((System.Windows.Controls.Button)target).Click += BtnImportSettings_Click;
			break;
		case 56:
			((System.Windows.Controls.Button)target).Click += BtnExportSettings_Click;
			break;
		case 57:
			GlobalOverlay = (Border)target;
			GlobalOverlay.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 42:
			((Border)target).MouseLeftButtonDown += ConditionCard_MouseLeftButtonDown;
			break;
		case 43:
			((System.Windows.Controls.CheckBox)target).Checked += PrefsChanged;
			((System.Windows.Controls.CheckBox)target).Unchecked += PrefsChanged;
			break;
		case 44:
			((System.Windows.Controls.Button)target).Click += BtnEditCondition_Click;
			break;
		case 45:
			((System.Windows.Controls.Button)target).Click += BtnDeleteCondition_Click;
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

