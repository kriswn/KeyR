using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace SupTask;

public class SettingsWindow : Window, IComponentConnector, IStyleConnector
{
	private struct SearchEntry
	{
		public FrameworkElement Container;

		public Border Section;

		public string SectionTitle;

		public string[] Keywords;
	}

	private Settings _settings;

	private MacroService _macroService;

	private MainWindow _mainWindow;

	private bool _isLoaded;

	private bool _isListeningForHotkey;

	private System.Windows.Controls.TextBox _currentHotkeyBox;

	private bool _isBasicTab = true;

	private ConditionEditorWindow _addWindow;

	private Dictionary<RestartCondition, ConditionEditorWindow> _editWindows = new Dictionary<RestartCondition, ConditionEditorWindow>();

	private static readonly Regex NumberOnlyRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

	private List<SearchEntry> _searchEntries;

	internal Grid RootGrid;

	internal Popup HoverTooltip;

	internal TextBlock TxtHoverTooltip;

	internal System.Windows.Controls.TextBox TxtSearch;

	internal TextBlock TxtSearchPlaceholder;

	internal ToggleButton BtnTabBasic;

	internal ToggleButton BtnTabAdvanced;

	internal StackPanel PanelBasic;

	internal Border SectionAccessibility;

	internal StackPanel ItemAlwaysOnTop;

	internal System.Windows.Controls.CheckBox ChkAlwaysOnTop;

	internal StackPanel ItemShowDeletion;

	internal System.Windows.Controls.CheckBox ChkShowConfirmations;

	internal StackPanel ItemThemeToggle;

	internal System.Windows.Controls.ComboBox CmbTheme;

	internal Border SectionPlayback;

	internal StackPanel ItemContinuous;

	internal System.Windows.Controls.CheckBox ChkContinuous;

	internal System.Windows.Controls.TextBox TxtLoopCount;

	internal Border SectionSpeed;

	internal StackPanel ItemSpeed;

	internal System.Windows.Controls.Button BtnResetSpeed;

	internal System.Windows.Controls.TextBox TxtSpeed;

	internal Border SectionHotkeys;

	internal StackPanel ItemRecHotkey;

	internal System.Windows.Controls.TextBox TxtRecHotkey;

	internal StackPanel ItemPlayHotkey;

	internal System.Windows.Controls.TextBox TxtPlayHotkey;

	internal StackPanel PanelAdvanced;

	internal Border SectionAutoRestart;

	internal System.Windows.Controls.Button BtnConditionHelp;

	internal System.Windows.Controls.Button BtnToggleMatchLogic;

	internal System.Windows.Controls.Button BtnToggleRestartMode;

	internal System.Windows.Controls.Button BtnToggleRestrictedMode;

	internal StackPanel ItemPolling;

	internal System.Windows.Controls.TextBox TxtPollingInterval;

	internal ItemsControl ListConditions;

	internal Border SectionPosition;

	internal Grid ItemQuickMove;

	internal Ellipse DotTL;

	internal Ellipse DotTC;

	internal Ellipse DotTR;

	internal Ellipse DotCL;

	internal Ellipse DotCC;

	internal Ellipse DotCR;

	internal Ellipse DotBL;

	internal Ellipse DotBC;

	internal Ellipse DotBR;

	internal Grid ItemPosXY;

	internal System.Windows.Controls.TextBox TxtPosX;

	internal System.Windows.Controls.TextBox TxtPosY;

	internal Border SectionMacroImport;

	internal StackPanel ItemImportMacros;

	internal Border SectionDataManagement;

	internal StackPanel ItemImportExport;

	internal Border GlobalOverlay;

	private bool _contentLoaded;

	public SettingsWindow(Settings settings, MacroService macroService, MainWindow mainWindow)
	{
		InitializeComponent();
		_settings = settings;
		_macroService = macroService;
		_mainWindow = mainWindow;
		if (_settings.SettingsWindowX != -1.0 && _settings.SettingsWindowY != -1.0)
		{
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = _settings.SettingsWindowX;
			base.Top = _settings.SettingsWindowY;
		}
		else
		{
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		}
		LoadSettingsToUI();
		BuildSearchIndex();
		SetActiveTab(basic: true);
		_isLoaded = true;
		_mainWindow.LocationChanged += delegate
		{
			if (_isLoaded)
			{
				_settings.X = _mainWindow.Left;
				_settings.Y = _mainWindow.Top;
				UpdatePositionUI();
			}
		};
	}

	private void BuildSearchIndex()
	{
		_searchEntries = new List<SearchEntry>
		{
			new SearchEntry
			{
				Container = ItemAlwaysOnTop,
				Section = SectionAccessibility,
				SectionTitle = "ACCESSIBILITY",
				Keywords = new string[1] { "always on top" }
			},
			new SearchEntry
			{
				Container = ItemShowDeletion,
				Section = SectionAccessibility,
				SectionTitle = "ACCESSIBILITY",
				Keywords = new string[3] { "show deletion confirmations", "deletion", "confirmations" }
			},
			new SearchEntry
			{
				Container = ItemThemeToggle,
				Section = SectionAccessibility,
				SectionTitle = "ACCESSIBILITY",
				Keywords = new string[5] { "dark theme", "light theme", "theme", "dark", "light" }
			},
			new SearchEntry
			{
				Container = ItemContinuous,
				Section = SectionPlayback,
				SectionTitle = "PLAYBACK SETTINGS",
				Keywords = new string[4] { "continuous playback", "loop count", "loop", "continuous" }
			},
			new SearchEntry
			{
				Container = ItemSpeed,
				Section = SectionSpeed,
				SectionTitle = "SPEED SETTINGS",
				Keywords = new string[3] { "speed multiplier", "speed", "reset" }
			},
			new SearchEntry
			{
				Container = ItemRecHotkey,
				Section = SectionHotkeys,
				SectionTitle = "HOTKEYS",
				Keywords = new string[2] { "record", "record hotkey" }
			},
			new SearchEntry
			{
				Container = ItemPlayHotkey,
				Section = SectionHotkeys,
				SectionTitle = "HOTKEYS",
				Keywords = new string[2] { "play", "play hotkey" }
			},
			new SearchEntry
			{
				Container = ItemQuickMove,
				Section = SectionPosition,
				SectionTitle = "QUICK-MOVE",
				Keywords = new string[11]
				{
					"quick-move", "quick move", "left", "right", "top", "bottom", "center", "position", "x", "y",
					"coordinates"
				}
			},
			new SearchEntry
			{
				Container = ItemImportExport,
				Section = SectionDataManagement,
				SectionTitle = "DATA MANAGEMENT",
				Keywords = new string[4] { "import", "export", "data management", "data" }
			}
		};
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
		CmbTheme.SelectedIndex = ((!_settings.IsDarkTheme) ? 1 : 0);
		BtnToggleMatchLogic.Content = (_settings.MatchAllConditions ? "Match: ALL" : "Match: ANY");
		BtnToggleMatchLogic.Tag = (_settings.MatchAllConditions ? "Macro restarts only when ALL enabled\nconditions are met." : "Macro restarts when ANY enabled condition\nis met.");
		BtnToggleRestartMode.Content = (_settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE");
		BtnToggleRestartMode.Tag = (_settings.UseSmartRestart ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts." : "Triggers instantly and repeatedly as long as\ncondition is met.");
		BtnToggleRestrictedMode.Content = (_settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF");
		BtnToggleRestrictedMode.Tag = (_settings.WaitConditionToRestart ? "Macro pauses at the end of the timeline\nand waits for conditions before looping." : "Macro loops naturally regardless of\nconditions.");
		BtnToggleRestrictedMode.Visibility = ((!_settings.UseSmartRestart) ? Visibility.Collapsed : Visibility.Visible);
		UpdatePrefUIState();
		UpdatePositionUI();
		RefreshConditionsList();
	}

	private void UpdatePrefUIState()
	{
		if (TxtLoopCount != null)
		{
			TxtLoopCount.Visibility = ((ChkContinuous.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (ChkAlwaysOnTop != null)
		{
			ChkAlwaysOnTop.Tag = ((ChkAlwaysOnTop.IsChecked == true) ? "Always On Top is On" : "Always On Top is Off");
		}
		if (ChkShowConfirmations != null)
		{
			ChkShowConfirmations.Tag = ((ChkShowConfirmations.IsChecked == true) ? "Deletion Confirmation when deleting Conditions is On" : "Deletion Confirmation when deleting Conditions is Off");
		}
		if (ChkContinuous != null)
		{
			ChkContinuous.Tag = ((ChkContinuous.IsChecked == true) ? "Macro playback will never stop and play continuously." : "Macro playback will stop after the specified number of loops.");
		}
	}

	private void UpdatePositionUI()
	{
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		if (TxtPosX != null && TxtPosY != null)
		{
			TxtPosX.Text = ((int)_settings.X).ToString();
			TxtPosY.Text = ((int)_settings.Y).ToString();
			Ellipse dotTL = DotTL;
			Ellipse dotTC = DotTC;
			Visibility visibility = (DotTR.Visibility = Visibility.Hidden);
			Visibility visibility3 = (dotTC.Visibility = visibility);
			dotTL.Visibility = visibility3;
			Ellipse dotCL = DotCL;
			Ellipse dotCC = DotCC;
			visibility = (DotCR.Visibility = Visibility.Hidden);
			visibility3 = (dotCC.Visibility = visibility);
			dotCL.Visibility = visibility3;
			Ellipse dotBL = DotBL;
			Ellipse dotBC = DotBC;
			visibility = (DotBR.Visibility = Visibility.Hidden);
			visibility3 = (dotBC.Visibility = visibility);
			dotBL.Visibility = visibility3;
			double num = ((_mainWindow.ActualWidth > 0.0) ? _mainWindow.ActualWidth : _mainWindow.Width);
			double num2 = ((_mainWindow.ActualHeight > 0.0) ? _mainWindow.ActualHeight : _mainWindow.Height);
			if (double.IsNaN(num) || num == 0.0)
			{
				num = 300.0;
			}
			if (double.IsNaN(num2) || num2 == 0.0)
			{
				num2 = 117.0;
			}
			double num3 = 0.0;
			Rect workArea = SystemParameters.WorkArea;
			double num4 = (workArea.Width - num) / 2.0;
			workArea = SystemParameters.WorkArea;
			double num5 = workArea.Width - num;
			double num6 = 0.0;
			workArea = SystemParameters.WorkArea;
			double num7 = (workArea.Height - num2) / 2.0;
			workArea = SystemParameters.WorkArea;
			double num8 = workArea.Height - num2;
			bool flag = Math.Abs(_settings.X - num3) < 1.0;
			bool flag2 = Math.Abs(_settings.X - num4) < 1.0;
			bool flag3 = Math.Abs(_settings.X - num5) < 1.0;
			bool flag4 = Math.Abs(_settings.Y - num6) < 1.0;
			bool flag5 = Math.Abs(_settings.Y - num7) < 1.0;
			bool flag6 = Math.Abs(_settings.Y - num8) < 1.0;
			if (flag && flag4)
			{
				DotTL.Visibility = Visibility.Visible;
			}
			else if (flag2 && flag4)
			{
				DotTC.Visibility = Visibility.Visible;
			}
			else if (flag3 && flag4)
			{
				DotTR.Visibility = Visibility.Visible;
			}
			else if (flag && flag5)
			{
				DotCL.Visibility = Visibility.Visible;
			}
			else if (flag2 && flag5)
			{
				DotCC.Visibility = Visibility.Visible;
			}
			else if (flag3 && flag5)
			{
				DotCR.Visibility = Visibility.Visible;
			}
			else if (flag && flag6)
			{
				DotBL.Visibility = Visibility.Visible;
			}
			else if (flag2 && flag6)
			{
				DotBC.Visibility = Visibility.Visible;
			}
			else if (flag3 && flag6)
			{
				DotBR.Visibility = Visibility.Visible;
			}
		}
	}

	private void BtnTabBasic_Click(object sender, RoutedEventArgs e)
	{
		SetActiveTab(basic: true);
	}

	private void BtnTabAdvanced_Click(object sender, RoutedEventArgs e)
	{
		SetActiveTab(basic: false);
	}

	private void SetActiveTab(bool basic)
	{
		_isBasicTab = basic;
		PanelBasic.Visibility = ((!basic) ? Visibility.Collapsed : Visibility.Visible);
		PanelAdvanced.Visibility = (basic ? Visibility.Collapsed : Visibility.Visible);
		BtnTabBasic.IsChecked = basic;
		BtnTabAdvanced.IsChecked = !basic;
		ApplySearchFilter(TxtSearch.Text);
	}

	private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
	{
		TxtSearchPlaceholder.Visibility = ((!string.IsNullOrEmpty(TxtSearch.Text)) ? Visibility.Collapsed : Visibility.Visible);
		ApplySearchFilter(TxtSearch.Text);
	}

	private void ApplySearchFilter(string query)
	{
		bool num = !string.IsNullOrWhiteSpace(query);
		string value = (num ? query.Trim().ToLowerInvariant() : "");
		if (num)
		{
			PanelBasic.Visibility = Visibility.Visible;
			PanelAdvanced.Visibility = Visibility.Visible;
			HashSet<Border> hashSet = new HashSet<Border>();
			foreach (SearchEntry searchEntry in _searchEntries)
			{
				searchEntry.Container.Visibility = Visibility.Collapsed;
			}
			foreach (SearchEntry searchEntry2 in _searchEntries)
			{
				bool flag = searchEntry2.SectionTitle.ToLowerInvariant().Contains(value);
				bool flag2 = false;
				string[] keywords = searchEntry2.Keywords;
				for (int i = 0; i < keywords.Length; i++)
				{
					if (keywords[i].Contains(value))
					{
						flag2 = true;
						break;
					}
				}
				if (flag || flag2)
				{
					searchEntry2.Container.Visibility = Visibility.Visible;
					hashSet.Add(searchEntry2.Section);
				}
			}
			foreach (SearchEntry searchEntry3 in _searchEntries)
			{
				if (searchEntry3.SectionTitle.ToLowerInvariant().Contains(value))
				{
					searchEntry3.Container.Visibility = Visibility.Visible;
				}
			}
			SectionAccessibility.Visibility = ((!hashSet.Contains(SectionAccessibility)) ? Visibility.Collapsed : Visibility.Visible);
			SectionPlayback.Visibility = ((!hashSet.Contains(SectionPlayback)) ? Visibility.Collapsed : Visibility.Visible);
			SectionSpeed.Visibility = ((!hashSet.Contains(SectionSpeed)) ? Visibility.Collapsed : Visibility.Visible);
			SectionHotkeys.Visibility = ((!hashSet.Contains(SectionHotkeys)) ? Visibility.Collapsed : Visibility.Visible);
			bool flag3 = "auto-restart".Contains(value) || "conditions".Contains(value) || "polling".Contains(value) || "match".Contains(value) || "logic".Contains(value) || "restricted".Contains(value);
			SectionAutoRestart.Visibility = ((!flag3) ? Visibility.Collapsed : Visibility.Visible);
			SectionPosition.Visibility = ((!hashSet.Contains(SectionPosition)) ? Visibility.Collapsed : Visibility.Visible);
			SectionDataManagement.Visibility = ((!hashSet.Contains(SectionDataManagement)) ? Visibility.Collapsed : Visibility.Visible);
			return;
		}
		PanelBasic.Visibility = ((!_isBasicTab) ? Visibility.Collapsed : Visibility.Visible);
		PanelAdvanced.Visibility = (_isBasicTab ? Visibility.Collapsed : Visibility.Visible);
		foreach (SearchEntry searchEntry4 in _searchEntries)
		{
			searchEntry4.Container.Visibility = Visibility.Visible;
		}
		SectionAccessibility.Visibility = Visibility.Visible;
		SectionPlayback.Visibility = Visibility.Visible;
		SectionSpeed.Visibility = Visibility.Visible;
		SectionHotkeys.Visibility = Visibility.Visible;
		SectionAutoRestart.Visibility = Visibility.Visible;
		SectionPosition.Visibility = Visibility.Visible;
		SectionDataManagement.Visibility = Visibility.Visible;
	}

	private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isLoaded)
		{
			bool flag = CmbTheme.SelectedIndex == 0;
			_settings.IsDarkTheme = flag;
			ThemeEngine.Apply(flag);
			_settings.Save();
		}
	}

	private void PrefsChanged(object sender, RoutedEventArgs e)
	{
		if (_isLoaded)
		{
			UpdatePrefUIState();
			SaveSettingsFromUI();
			if (sender is FrameworkElement { Tag: string tag } && HoverTooltip.IsOpen)
			{
				RefreshHoverTooltip(tag);
			}
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
		_settings.WaitConditionToRestart = BtnToggleRestrictedMode.Content.ToString().Contains("ON");
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
			double num = SystemParameters.VirtualScreenWidth - _mainWindow.ActualWidth;
			if (result4 < 0.0)
			{
				result4 = 0.0;
			}
			if (result4 > num)
			{
				result4 = Math.Max(0.0, num);
			}
			_settings.X = result4;
			_mainWindow.Left = result4;
		}
		if (double.TryParse(TxtPosY.Text, out var result5))
		{
			double num2 = SystemParameters.VirtualScreenHeight - _mainWindow.ActualHeight;
			if (result5 < 0.0)
			{
				result5 = 0.0;
			}
			if (result5 > num2)
			{
				result5 = Math.Max(0.0, num2);
			}
			_settings.Y = result5;
			_mainWindow.Top = result5;
		}
		_mainWindow.Topmost = _settings.AlwaysOnTop;
		_macroService.RegisterHotkeys(_settings);
		_settings.Save();
	}

	private void BtnResetSpeed_Click(object sender, RoutedEventArgs e)
	{
		TxtSpeed.Text = "1";
		PrefsChanged(null, null);
	}

	private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
	{
		_isListeningForHotkey = true;
		GlobalOverlay.Visibility = Visibility.Visible;
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
			Keyboard.ClearFocus();
			RootGrid.Focus();
			Task.Run(delegate
			{
				Thread.Sleep(50);
				BypassInput.SendKey(17, isDown: false);
				BypassInput.SendKey(18, isDown: false);
				BypassInput.SendKey(16, isDown: false);
			});
			_macroService.ResumeHotkeys();
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

	private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
	}

	private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Keyboard.ClearFocus();
		RootGrid.Focus();
	}

	private void BtnMoveTL_Click(object sender, RoutedEventArgs e)
	{
		SetPos(0.0, 0.0);
	}

	private void BtnMoveTC_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		SetPos((workArea.Width - _mainWindow.ActualWidth) / 2.0, 0.0);
	}

	private void BtnMoveTR_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		SetPos(workArea.Width - _mainWindow.ActualWidth, 0.0);
	}

	private void BtnMoveCL_Click(object sender, RoutedEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		SetPos(0.0, (workArea.Height - _mainWindow.ActualHeight) / 2.0);
	}

	private void BtnMoveCC_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		double x = (workArea.Width - _mainWindow.ActualWidth) / 2.0;
		workArea = SystemParameters.WorkArea;
		SetPos(x, (workArea.Height - _mainWindow.ActualHeight) / 2.0);
	}

	private void BtnMoveCR_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		double x = workArea.Width - _mainWindow.ActualWidth;
		workArea = SystemParameters.WorkArea;
		SetPos(x, (workArea.Height - _mainWindow.ActualHeight) / 2.0);
	}

	private void BtnMoveBL_Click(object sender, RoutedEventArgs e)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		SetPos(0.0, workArea.Height - _mainWindow.ActualHeight);
	}

	private void BtnMoveBC_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		double x = (workArea.Width - _mainWindow.ActualWidth) / 2.0;
		workArea = SystemParameters.WorkArea;
		SetPos(x, workArea.Height - _mainWindow.ActualHeight);
	}

	private void BtnMoveBR_Click(object sender, RoutedEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		double x = workArea.Width - _mainWindow.ActualWidth;
		workArea = SystemParameters.WorkArea;
		SetPos(x, workArea.Height - _mainWindow.ActualHeight);
	}

	private void SetPos(double x, double y)
	{
		_mainWindow.Left = x;
		_mainWindow.Top = y;
		SaveQuickPos();
	}

	private async void SaveQuickPos()
	{
		await Task.Delay(50);
		_settings.X = _mainWindow.Left;
		_settings.Y = _mainWindow.Top;
		UpdatePositionUI();
		_settings.Save();
	}

	private void BtnToggleMatchLogic_Click(object sender, RoutedEventArgs e)
	{
		_settings.MatchAllConditions = !_settings.MatchAllConditions;
		string text = (_settings.MatchAllConditions ? "Macro restarts only when ALL enabled\nconditions are met." : "Macro restarts when ANY enabled condition\nis met.");
		BtnToggleMatchLogic.Content = (_settings.MatchAllConditions ? "Match: ALL" : "Match: ANY");
		BtnToggleMatchLogic.Tag = text;
		RefreshHoverTooltip(text);
		SaveSettingsFromUI();
	}

	private void BtnToggleRestartMode_Click(object sender, RoutedEventArgs e)
	{
		_settings.UseSmartRestart = !_settings.UseSmartRestart;
		string text = (_settings.UseSmartRestart ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts." : "Triggers instantly and repeatedly as long as\ncondition is met.");
		BtnToggleRestartMode.Content = (_settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE");
		BtnToggleRestartMode.Tag = text;
		BtnToggleRestrictedMode.Visibility = ((!_settings.UseSmartRestart) ? Visibility.Collapsed : Visibility.Visible);
		RefreshHoverTooltip(text);
		SaveSettingsFromUI();
	}

	private void BtnToggleRestrictedMode_Click(object sender, RoutedEventArgs e)
	{
		_settings.WaitConditionToRestart = !_settings.WaitConditionToRestart;
		string text = (_settings.WaitConditionToRestart ? "Macro pauses at the end of the timeline\nand waits for conditions before looping." : "Macro loops naturally regardless of\nconditions.");
		BtnToggleRestrictedMode.Content = (_settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF");
		BtnToggleRestrictedMode.Tag = text;
		RefreshHoverTooltip(text);
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
		_addWindow = new ConditionEditorWindow(new RestartCondition(), "Add Condition", (string name) => IsNameDuplicate(name, null), _settings);
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
		ConditionEditorWindow conditionEditorWindow = new ConditionEditorWindow(cond, "Edit Condition", (string name) => IsNameDuplicate(name, cond), _settings);
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
		}
		catch
		{
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
				_settings.RecHotkey = settings.RecHotkey;
				_settings.PlayHotkey = settings.PlayHotkey;
				_settings.CustomSpeed = settings.CustomSpeed;
				_settings.LoopCount = settings.LoopCount;
				_settings.AlwaysOnTop = settings.AlwaysOnTop;
				_settings.LoopContinuous = settings.LoopContinuous;
				_settings.UseCustomSpeed = settings.UseCustomSpeed;
				_settings.HideDeleteConfirmation = settings.HideDeleteConfirmation;
				_settings.ConditionsPollingInterval = settings.ConditionsPollingInterval;
				_settings.UseSmartRestart = settings.UseSmartRestart;
				_settings.WaitConditionToRestart = settings.WaitConditionToRestart;
				_settings.MatchAllConditions = settings.MatchAllConditions;
				_settings.RestartConditions = settings.RestartConditions;
				_settings.IsDarkTheme = settings.IsDarkTheme;
				_settings.UseSmartRestart = settings.UseSmartRestart;
				ThemeEngine.Apply(_settings.IsDarkTheme);
				LoadSettingsToUI();
				SetActiveTab(_isBasicTab);
				_macroService.RegisterHotkeys(_settings);
				_mainWindow.Topmost = _settings.AlwaysOnTop;
				_settings.Save();
			}
		}
		catch
		{
		}
	}

	private void BtnImportInformaalTask_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "InformaalTask Scripts (*.txt)|*.txt|All Files (*.*)|*.*"
		};
		bool dontAskAgain;
		if (openFileDialog.ShowDialog() == true)
		{
			try
			{
				string[] lines = File.ReadAllLines(openFileDialog.FileName);
				_macroService.ImportInformaalTask(lines);
				_mainWindow.SetMacroName(System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName));
				ThemedConfirmWindow.Show(this, "Successfully converted InformaalTask macro:\n" + System.IO.Path.GetFileName(openFileDialog.FileName), out dontAskAgain, "OK", showDontAsk: false, isPositive: true);
			}
			catch (Exception ex)
			{
				ThemedConfirmWindow.Show(this, "Failed to parse InformaalTask file: " + ex.Message, out dontAskAgain, "OK", showDontAsk: false);
			}
		}
	}

	private void BtnImportTinyTask_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "TinyTask Recordings (*.rec)|*.rec|All Files (*.*)|*.*"
		};
		bool dontAskAgain;
		if (openFileDialog.ShowDialog() == true)
		{
			try
			{
				byte[] rawData = File.ReadAllBytes(openFileDialog.FileName);
				_macroService.ImportTinyTask(rawData);
				_mainWindow.SetMacroName(System.IO.Path.GetFileNameWithoutExtension(openFileDialog.FileName));
				ThemedConfirmWindow.Show(this, "Successfully extracted generic TinyTask records from:\n" + System.IO.Path.GetFileName(openFileDialog.FileName), out dontAskAgain, "OK", showDontAsk: false, isPositive: true);
			}
			catch (Exception ex)
			{
				ThemedConfirmWindow.Show(this, "Failed to parse TinyTask binary: " + ex.Message, out dontAskAgain, "OK", showDontAsk: false);
			}
		}
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			DragMove();
		}
	}

	private void Window_LocationChanged(object sender, EventArgs e)
	{
		if (base.IsLoaded)
		{
			_settings.SettingsWindowX = base.Left;
			_settings.SettingsWindowY = base.Top;
			_settings.Save();
		}
	}

	private void BtnMinimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
	{
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		base.OnMouseMove(e);
		FrameworkElement frameworkElement = Mouse.DirectlyOver as FrameworkElement;
		string text = null;
		while (frameworkElement != null)
		{
			if (frameworkElement is ComboBoxItem)
			{
				frameworkElement = VisualTreeHelper.GetParent((DependencyObject)(object)frameworkElement) as FrameworkElement;
				continue;
			}
			if (frameworkElement.Tag is string text2 && !string.IsNullOrEmpty(text2))
			{
				text = text2;
				break;
			}
			frameworkElement = VisualTreeHelper.GetParent((DependencyObject)(object)frameworkElement) as FrameworkElement;
		}
		if (!string.IsNullOrEmpty(text))
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

	protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		HoverTooltip.IsOpen = false;
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
			Uri resourceLocator = new Uri("/SupTask;component/settingswindow.xaml", UriKind.Relative);
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
			((SettingsWindow)target).MouseLeftButtonDown += Window_MouseLeftButtonDown;
			((SettingsWindow)target).LocationChanged += Window_LocationChanged;
			break;
		case 2:
			RootGrid = (Grid)target;
			RootGrid.MouseLeftButtonDown += RootGrid_MouseLeftButtonDown;
			break;
		case 3:
			HoverTooltip = (Popup)target;
			break;
		case 4:
			TxtHoverTooltip = (TextBlock)target;
			break;
		case 5:
			((System.Windows.Controls.Button)target).Click += BtnMinimize_Click;
			break;
		case 6:
			((System.Windows.Controls.Button)target).Click += BtnClose_Click;
			break;
		case 7:
			TxtSearch = (System.Windows.Controls.TextBox)target;
			TxtSearch.TextChanged += TxtSearch_TextChanged;
			break;
		case 8:
			TxtSearchPlaceholder = (TextBlock)target;
			break;
		case 9:
			BtnTabBasic = (ToggleButton)target;
			BtnTabBasic.Click += BtnTabBasic_Click;
			break;
		case 10:
			BtnTabAdvanced = (ToggleButton)target;
			BtnTabAdvanced.Click += BtnTabAdvanced_Click;
			break;
		case 11:
			PanelBasic = (StackPanel)target;
			break;
		case 12:
			SectionAccessibility = (Border)target;
			break;
		case 13:
			ItemAlwaysOnTop = (StackPanel)target;
			break;
		case 14:
			ChkAlwaysOnTop = (System.Windows.Controls.CheckBox)target;
			ChkAlwaysOnTop.Checked += PrefsChanged;
			ChkAlwaysOnTop.Unchecked += PrefsChanged;
			break;
		case 15:
			ItemShowDeletion = (StackPanel)target;
			break;
		case 16:
			ChkShowConfirmations = (System.Windows.Controls.CheckBox)target;
			ChkShowConfirmations.Checked += PrefsChanged;
			ChkShowConfirmations.Unchecked += PrefsChanged;
			break;
		case 17:
			ItemThemeToggle = (StackPanel)target;
			break;
		case 18:
			CmbTheme = (System.Windows.Controls.ComboBox)target;
			CmbTheme.SelectionChanged += CmbTheme_SelectionChanged;
			break;
		case 19:
			SectionPlayback = (Border)target;
			break;
		case 20:
			ItemContinuous = (StackPanel)target;
			break;
		case 21:
			ChkContinuous = (System.Windows.Controls.CheckBox)target;
			ChkContinuous.Checked += PrefsChanged;
			ChkContinuous.Unchecked += PrefsChanged;
			break;
		case 22:
			TxtLoopCount = (System.Windows.Controls.TextBox)target;
			TxtLoopCount.LostFocus += PrefsChanged;
			TxtLoopCount.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 23:
			SectionSpeed = (Border)target;
			break;
		case 24:
			ItemSpeed = (StackPanel)target;
			break;
		case 25:
			BtnResetSpeed = (System.Windows.Controls.Button)target;
			BtnResetSpeed.Click += BtnResetSpeed_Click;
			break;
		case 26:
			TxtSpeed = (System.Windows.Controls.TextBox)target;
			TxtSpeed.LostFocus += PrefsChanged;
			TxtSpeed.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 27:
			SectionHotkeys = (Border)target;
			break;
		case 28:
			ItemRecHotkey = (StackPanel)target;
			break;
		case 29:
			TxtRecHotkey = (System.Windows.Controls.TextBox)target;
			TxtRecHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtRecHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtRecHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 30:
			ItemPlayHotkey = (StackPanel)target;
			break;
		case 31:
			TxtPlayHotkey = (System.Windows.Controls.TextBox)target;
			TxtPlayHotkey.GotFocus += TxtHotkey_GotFocus;
			TxtPlayHotkey.PreviewKeyDown += TxtHotkey_PreviewKeyDown;
			TxtPlayHotkey.LostFocus += TxtHotkey_LostFocus;
			break;
		case 32:
			PanelAdvanced = (StackPanel)target;
			break;
		case 33:
			SectionAutoRestart = (Border)target;
			break;
		case 34:
			BtnConditionHelp = (System.Windows.Controls.Button)target;
			break;
		case 35:
			BtnToggleMatchLogic = (System.Windows.Controls.Button)target;
			BtnToggleMatchLogic.Click += BtnToggleMatchLogic_Click;
			break;
		case 36:
			BtnToggleRestartMode = (System.Windows.Controls.Button)target;
			BtnToggleRestartMode.Click += BtnToggleRestartMode_Click;
			break;
		case 37:
			BtnToggleRestrictedMode = (System.Windows.Controls.Button)target;
			BtnToggleRestrictedMode.Click += BtnToggleRestrictedMode_Click;
			break;
		case 38:
			ItemPolling = (StackPanel)target;
			break;
		case 39:
			TxtPollingInterval = (System.Windows.Controls.TextBox)target;
			TxtPollingInterval.LostFocus += PrefsChanged;
			TxtPollingInterval.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 40:
			ListConditions = (ItemsControl)target;
			break;
		case 45:
			((System.Windows.Controls.Button)target).Click += BtnAddCondition_Click;
			break;
		case 46:
			SectionPosition = (Border)target;
			break;
		case 47:
			ItemQuickMove = (Grid)target;
			break;
		case 48:
			((System.Windows.Controls.Button)target).Click += BtnMoveTL_Click;
			break;
		case 49:
			DotTL = (Ellipse)target;
			break;
		case 50:
			((System.Windows.Controls.Button)target).Click += BtnMoveTC_Click;
			break;
		case 51:
			DotTC = (Ellipse)target;
			break;
		case 52:
			((System.Windows.Controls.Button)target).Click += BtnMoveTR_Click;
			break;
		case 53:
			DotTR = (Ellipse)target;
			break;
		case 54:
			((System.Windows.Controls.Button)target).Click += BtnMoveCL_Click;
			break;
		case 55:
			DotCL = (Ellipse)target;
			break;
		case 56:
			((System.Windows.Controls.Button)target).Click += BtnMoveCC_Click;
			break;
		case 57:
			DotCC = (Ellipse)target;
			break;
		case 58:
			((System.Windows.Controls.Button)target).Click += BtnMoveCR_Click;
			break;
		case 59:
			DotCR = (Ellipse)target;
			break;
		case 60:
			((System.Windows.Controls.Button)target).Click += BtnMoveBL_Click;
			break;
		case 61:
			DotBL = (Ellipse)target;
			break;
		case 62:
			((System.Windows.Controls.Button)target).Click += BtnMoveBC_Click;
			break;
		case 63:
			DotBC = (Ellipse)target;
			break;
		case 64:
			((System.Windows.Controls.Button)target).Click += BtnMoveBR_Click;
			break;
		case 65:
			DotBR = (Ellipse)target;
			break;
		case 66:
			ItemPosXY = (Grid)target;
			break;
		case 67:
			TxtPosX = (System.Windows.Controls.TextBox)target;
			TxtPosX.LostFocus += PrefsChanged;
			break;
		case 68:
			TxtPosY = (System.Windows.Controls.TextBox)target;
			TxtPosY.LostFocus += PrefsChanged;
			break;
		case 69:
			SectionMacroImport = (Border)target;
			break;
		case 70:
			ItemImportMacros = (StackPanel)target;
			break;
		case 71:
			((System.Windows.Controls.Button)target).Click += BtnImportInformaalTask_Click;
			break;
		case 72:
			((System.Windows.Controls.Button)target).Click += BtnImportTinyTask_Click;
			break;
		case 73:
			SectionDataManagement = (Border)target;
			break;
		case 74:
			ItemImportExport = (StackPanel)target;
			break;
		case 75:
			((System.Windows.Controls.Button)target).Click += BtnImportSettings_Click;
			break;
		case 76:
			((System.Windows.Controls.Button)target).Click += BtnExportSettings_Click;
			break;
		case 77:
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
		case 41:
			((Border)target).MouseLeftButtonDown += ConditionCard_MouseLeftButtonDown;
			break;
		case 42:
			((System.Windows.Controls.CheckBox)target).Checked += PrefsChanged;
			((System.Windows.Controls.CheckBox)target).Unchecked += PrefsChanged;
			break;
		case 43:
			((System.Windows.Controls.Button)target).Click += BtnEditCondition_Click;
			break;
		case 44:
			((System.Windows.Controls.Button)target).Click += BtnDeleteCondition_Click;
			break;
		}
	}
}



