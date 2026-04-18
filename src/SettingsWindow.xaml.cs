using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeyR
{
    public partial class SettingsWindow : Window
    {
        private Settings _settings;
        private MacroService _macroService;
        private MainWindow _mainWindow;
        private bool _isLoaded = false;
        private bool _isListeningForHotkey = false;
        private TextBox _currentHotkeyBox = null;
        private bool _isBasicTab = true;

        private ConditionEditorWindow _addWindow = null;
        private Dictionary<RestartCondition, ConditionEditorWindow> _editWindows = new Dictionary<RestartCondition, ConditionEditorWindow>();

        private static readonly Regex NumberOnlyRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

        // Searchable items mapped: (container, keywords[])
        private List<SearchEntry> _searchEntries;

        private struct SearchEntry
        {
            public FrameworkElement Container; // the named StackPanel wrapping the item
            public Border Section;            // the parent section Border
            public string SectionTitle;
            public string[] Keywords;
        }

        public SettingsWindow(Settings settings, MacroService macroService, MainWindow mainWindow)
        {
            InitializeComponent();
            _settings = settings;
            _macroService = macroService;
            _mainWindow = mainWindow;

            // Restore position
            if (_settings.SettingsWindowX != -1 && _settings.SettingsWindowY != -1)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _settings.SettingsWindowX;
                this.Top = _settings.SettingsWindowY;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            LoadSettingsToUI();
            BuildSearchIndex();
            SetActiveTab(true);
            _isLoaded = true;

            // Track main window movements to update indicators dynamically
            _mainWindow.LocationChanged += (s, ev) =>
            {
                if (!_isLoaded) return;
                _settings.X = _mainWindow.Left;
                _settings.Y = _mainWindow.Top;
                UpdatePositionUI();
            };
        }

        private void BuildSearchIndex()
        {
            _searchEntries = new List<SearchEntry>
            {
                // Basic - Accessibility
                new SearchEntry { Container = ItemAlwaysOnTop, Section = SectionAccessibility, SectionTitle = "ACCESSIBILITY", Keywords = new[] { "always on top" } },
                new SearchEntry { Container = ItemShowDeletion, Section = SectionAccessibility, SectionTitle = "ACCESSIBILITY", Keywords = new[] { "show deletion confirmations", "deletion", "confirmations" } },
                new SearchEntry { Container = ItemThemeToggle, Section = SectionAccessibility, SectionTitle = "ACCESSIBILITY", Keywords = new[] { "dark theme", "light theme", "theme", "dark", "light" } },
                // Basic - Playback
                new SearchEntry { Container = ItemContinuous, Section = SectionPlayback, SectionTitle = "PLAYBACK SETTINGS", Keywords = new[] { "continuous playback", "loop count", "loop", "continuous" } },
                // Basic - Speed
                new SearchEntry { Container = ItemSpeed, Section = SectionSpeed, SectionTitle = "SPEED SETTINGS", Keywords = new[] { "speed multiplier", "speed", "reset" } },
                // Basic - Hotkeys
                new SearchEntry { Container = ItemRecHotkey, Section = SectionHotkeys, SectionTitle = "HOTKEYS", Keywords = new[] { "record", "record hotkey" } },
                new SearchEntry { Container = ItemPlayHotkey, Section = SectionHotkeys, SectionTitle = "HOTKEYS", Keywords = new[] { "play", "play hotkey" } },
                // Advanced - Auto-Restart (treat as monolithic — no individual items to filter)
                // Advanced - Quick Move
                new SearchEntry { Container = ItemQuickMove, Section = SectionPosition, SectionTitle = "QUICK-MOVE", Keywords = new[] { "quick-move", "quick move", "left", "right", "top", "bottom", "center", "position", "x", "y", "coordinates" } },
                // Advanced - Data Management
                new SearchEntry { Container = ItemImportExport, Section = SectionDataManagement, SectionTitle = "DATA MANAGEMENT", Keywords = new[] { "import", "export", "data management", "data" } },
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
            CmbTheme.SelectedIndex = _settings.IsDarkTheme ? 0 : 1;
            TxtPauseHotkey.Text = _settings.PauseHotkey;

            BtnToggleMatchLogic.Content = _settings.MatchAllConditions ? "Match: ALL" : "Match: ANY";
            BtnToggleMatchLogic.Tag = _settings.MatchAllConditions
                ? "Macro restarts only when ALL enabled\nconditions are met."
                : "Macro restarts when ANY enabled condition\nis met.";

            BtnToggleRestartMode.Content = _settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE";
            BtnToggleRestartMode.Tag = _settings.UseSmartRestart
                ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts."
                : "Triggers instantly and repeatedly as long as\ncondition is met.";

            BtnToggleRestrictedMode.Content = _settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF";
            BtnToggleRestrictedMode.Tag = _settings.WaitConditionToRestart
                ? "Macro pauses at the end of the timeline\nand waits for conditions before looping."
                : "Macro loops naturally regardless of\nconditions.";
            BtnToggleRestrictedMode.Visibility = _settings.UseSmartRestart ? Visibility.Visible : Visibility.Collapsed;

            UpdatePrefUIState();
            UpdatePositionUI();
            RefreshConditionsList();
        }

        private void UpdatePrefUIState()
        {
            if (TxtLoopCount != null)
                TxtLoopCount.Visibility = ChkContinuous.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;

            if (ChkAlwaysOnTop != null)
                ChkAlwaysOnTop.Tag = ChkAlwaysOnTop.IsChecked == true ? "Always On Top is On" : "Always On Top is Off";

            if (ChkShowConfirmations != null)
                ChkShowConfirmations.Tag = ChkShowConfirmations.IsChecked == true ? "Deletion Confirmation when deleting Conditions is On" : "Deletion Confirmation when deleting Conditions is Off";

            if (ChkContinuous != null)
                ChkContinuous.Tag = ChkContinuous.IsChecked == true 
                    ? "Macro playback will never stop and play continuously." 
                    : "Macro playback will stop after the specified number of loops.";
        }

        private void UpdatePositionUI()
        {
            if (TxtPosX == null || TxtPosY == null) return;
            TxtPosX.Text = ((int)_settings.X).ToString();
            TxtPosY.Text = ((int)_settings.Y).ToString();
            
            // Hide all dots
            DotTL.Visibility = DotTC.Visibility = DotTR.Visibility = Visibility.Hidden;
            DotCL.Visibility = DotCC.Visibility = DotCR.Visibility = Visibility.Hidden;
            DotBL.Visibility = DotBC.Visibility = DotBR.Visibility = Visibility.Hidden;

            // Calculate expected positions using Width/Height fallbacks if window is not rendered yet
            double w = _mainWindow.ActualWidth > 0 ? _mainWindow.ActualWidth : _mainWindow.Width;
            double h = _mainWindow.ActualHeight > 0 ? _mainWindow.ActualHeight : _mainWindow.Height;
            if (double.IsNaN(w) || w == 0) w = 300;
            if (double.IsNaN(h) || h == 0) h = 117;

            double minX = 0;
            double centerX = (SystemParameters.WorkArea.Width - w) / 2;
            double maxX = SystemParameters.WorkArea.Width - w;

            double minY = 0;
            double centerY = (SystemParameters.WorkArea.Height - h) / 2;
            double maxY = SystemParameters.WorkArea.Height - h;

            // Tolerance for floating point precision: wait user asked for EXACT spot. So use < 1.0
            bool isMinX = Math.Abs(_settings.X - minX) < 1.0;
            bool isCenterX = Math.Abs(_settings.X - centerX) < 1.0;
            bool isMaxX = Math.Abs(_settings.X - maxX) < 1.0;

            bool isMinY = Math.Abs(_settings.Y - minY) < 1.0;
            bool isCenterY = Math.Abs(_settings.Y - centerY) < 1.0;
            bool isMaxY = Math.Abs(_settings.Y - maxY) < 1.0;

            if (isMinX && isMinY) DotTL.Visibility = Visibility.Visible;
            else if (isCenterX && isMinY) DotTC.Visibility = Visibility.Visible;
            else if (isMaxX && isMinY) DotTR.Visibility = Visibility.Visible;
            
            else if (isMinX && isCenterY) DotCL.Visibility = Visibility.Visible;
            else if (isCenterX && isCenterY) DotCC.Visibility = Visibility.Visible;
            else if (isMaxX && isCenterY) DotCR.Visibility = Visibility.Visible;
            
            else if (isMinX && isMaxY) DotBL.Visibility = Visibility.Visible;
            else if (isCenterX && isMaxY) DotBC.Visibility = Visibility.Visible;
            else if (isMaxX && isMaxY) DotBR.Visibility = Visibility.Visible;
        }

        // --- Tab Switching ---
        private void BtnTabBasic_Click(object sender, RoutedEventArgs e) => SetActiveTab(true);
        private void BtnTabAdvanced_Click(object sender, RoutedEventArgs e) => SetActiveTab(false);

        private void SetActiveTab(bool basic)
        {
            _isBasicTab = basic;
            PanelBasic.Visibility = basic ? Visibility.Visible : Visibility.Collapsed;
            PanelAdvanced.Visibility = basic ? Visibility.Collapsed : Visibility.Visible;

            BtnTabBasic.IsChecked = basic;
            BtnTabAdvanced.IsChecked = !basic;

            // Re-apply search filter when switching tabs
            ApplySearchFilter(TxtSearch.Text);
        }

        // --- Search ---
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplySearchFilter(TxtSearch.Text);
        }

        private void ApplySearchFilter(string query)
        {
            bool hasQuery = !string.IsNullOrWhiteSpace(query);
            string q = hasQuery ? query.Trim().ToLowerInvariant() : "";

            if (hasQuery)
            {
                // When searching, show both panels
                PanelBasic.Visibility = Visibility.Visible;
                PanelAdvanced.Visibility = Visibility.Visible;

                // Track which sections have visible items
                var sectionsWithVisibleItems = new HashSet<Border>();

                // First: hide all indexed item containers
                foreach (var entry in _searchEntries)
                    entry.Container.Visibility = Visibility.Collapsed;

                // Show items that match
                foreach (var entry in _searchEntries)
                {
                    bool sectionMatch = entry.SectionTitle.ToLowerInvariant().Contains(q);
                    bool itemMatch = false;

                    foreach (var kw in entry.Keywords)
                    {
                        if (kw.Contains(q)) { itemMatch = true; break; }
                    }

                    if (sectionMatch || itemMatch)
                    {
                        entry.Container.Visibility = Visibility.Visible;
                        sectionsWithVisibleItems.Add(entry.Section);
                    }
                }

                // If a section title matches, show ALL items in that section
                foreach (var entry in _searchEntries)
                {
                    if (entry.SectionTitle.ToLowerInvariant().Contains(q))
                    {
                        entry.Container.Visibility = Visibility.Visible;
                    }
                }

                // Show/hide sections
                SectionAccessibility.Visibility = sectionsWithVisibleItems.Contains(SectionAccessibility) ? Visibility.Visible : Visibility.Collapsed;
                SectionPlayback.Visibility = sectionsWithVisibleItems.Contains(SectionPlayback) ? Visibility.Visible : Visibility.Collapsed;
                SectionSpeed.Visibility = sectionsWithVisibleItems.Contains(SectionSpeed) ? Visibility.Visible : Visibility.Collapsed;
                SectionHotkeys.Visibility = sectionsWithVisibleItems.Contains(SectionHotkeys) ? Visibility.Visible : Visibility.Collapsed;

                // Auto-restart: search section title + keywords
                bool autoRestartMatch = "auto-restart".Contains(q) || "conditions".Contains(q) || "polling".Contains(q) || "match".Contains(q) || "logic".Contains(q) || "restricted".Contains(q);
                SectionAutoRestart.Visibility = autoRestartMatch ? Visibility.Visible : Visibility.Collapsed;

                SectionPosition.Visibility = sectionsWithVisibleItems.Contains(SectionPosition) ? Visibility.Visible : Visibility.Collapsed;
                SectionDataManagement.Visibility = sectionsWithVisibleItems.Contains(SectionDataManagement) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // No search — revert to tab selection
                PanelBasic.Visibility = _isBasicTab ? Visibility.Visible : Visibility.Collapsed;
                PanelAdvanced.Visibility = _isBasicTab ? Visibility.Collapsed : Visibility.Visible;

                // Show everything
                foreach (var entry in _searchEntries)
                    entry.Container.Visibility = Visibility.Visible;

                SectionAccessibility.Visibility = Visibility.Visible;
                SectionPlayback.Visibility = Visibility.Visible;
                SectionSpeed.Visibility = Visibility.Visible;
                SectionHotkeys.Visibility = Visibility.Visible;
                SectionAutoRestart.Visibility = Visibility.Visible;
                SectionPosition.Visibility = Visibility.Visible;
                SectionDataManagement.Visibility = Visibility.Visible;
            }
        }

        // --- Theme Toggle ---
        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            bool dark = CmbTheme.SelectedIndex == 0;
            _settings.IsDarkTheme = dark;
            ThemeEngine.Apply(dark);

            _settings.Save();
        }

        private void PrefsChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            UpdatePrefUIState();
            SaveSettingsFromUI();

            if (sender is FrameworkElement fe && fe.Tag is string tagStr && HoverTooltip.IsOpen)
            {
                RefreshHoverTooltip(tagStr);
            }
        }

        private void SaveSettingsFromUI()
        {
            _settings.RecHotkey = TxtRecHotkey.Text.Trim();
            _settings.PlayHotkey = TxtPlayHotkey.Text.Trim();
            _settings.PauseHotkey = TxtPauseHotkey.Text.Trim();

            if (int.TryParse(TxtLoopCount.Text, out int count) && count > 0)
                _settings.LoopCount = count;

            if (double.TryParse(TxtSpeed.Text, out double speed) && speed > 0)
                _settings.CustomSpeed = speed;

            _settings.UseCustomSpeed = true;
            _settings.LoopContinuous = ChkContinuous.IsChecked == true;
            _settings.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
            _settings.WaitConditionToRestart = BtnToggleRestrictedMode.Content.ToString().Contains("ON");
            _settings.HideDeleteConfirmation = ChkShowConfirmations.IsChecked == false;

            if (int.TryParse(TxtPollingInterval.Text.Trim(), out int poll))
            {
                if (poll < 100) poll = 100;
                _settings.ConditionsPollingInterval = poll;
                TxtPollingInterval.Text = poll.ToString();
            }

            if (double.TryParse(TxtPosX.Text, out double x))
            {
                double maxX = SystemParameters.VirtualScreenWidth - _mainWindow.ActualWidth;
                if (x < 0) x = 0;
                if (x > maxX) x = Math.Max(0, maxX);
                _settings.X = x;
                _mainWindow.Left = x;
            }
            if (double.TryParse(TxtPosY.Text, out double y))
            {
                double maxY = SystemParameters.VirtualScreenHeight - _mainWindow.ActualHeight;
                if (y < 0) y = 0;
                if (y > maxY) y = Math.Max(0, maxY);
                _settings.Y = y;
                _mainWindow.Top = y;
            }

            _mainWindow.Topmost = _settings.AlwaysOnTop;
            _macroService.RegisterHotkeys(_settings);
            _settings.Save();
        }

        // --- Speed Reset ---
        private void BtnResetSpeed_Click(object sender, RoutedEventArgs e)
        {
            TxtSpeed.Text = "1";
            PrefsChanged(null, null);
        }

        private void BtnResetRecHotkey_Click(object sender, RoutedEventArgs e)
        {
            TxtRecHotkey.Text = "F8";
            PrefsChanged(null, null);
        }

        private void BtnResetPlayHotkey_Click(object sender, RoutedEventArgs e)
        {
            TxtPlayHotkey.Text = "F9";
            PrefsChanged(null, null);
        }

        private void BtnResetPauseHotkey_Click(object sender, RoutedEventArgs e)
        {
            TxtPauseHotkey.Text = "F12";
            PrefsChanged(null, null);
        }

        // --- Hotkey Listening ---
        private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
        {
            _isListeningForHotkey = true;
            GlobalOverlay.Visibility = Visibility.Visible;
            _currentHotkeyBox = sender as TextBox;
            _macroService.SuspendHotkeys();
        }

        private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e) => EndHotkeyListening();

        private void EndHotkeyListening()
        {
            if (!_isListeningForHotkey) return;
            _isListeningForHotkey = false;
            GlobalOverlay.Visibility = Visibility.Collapsed;
            _currentHotkeyBox = null;
            
            Keyboard.ClearFocus();
            RootGrid.Focus();

            System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(50);
                BypassInput.SendKey(0x11, false);
                BypassInput.SendKey(0x12, false);
                BypassInput.SendKey(0x10, false);
            });

            _macroService.ResumeHotkeys();
        }

        private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.Escape) { EndHotkeyListening(); return; }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LWin || key == Key.RWin || key == Key.Clear || key == Key.OemClear || key == Key.Apps)
                return;

            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            var winFormsKey = (System.Windows.Forms.Keys)virtualKey;

            var modifiers = Keyboard.Modifiers;
            string mapped = "";
            if (modifiers.HasFlag(ModifierKeys.Control)) mapped += "Control+";
            if (modifiers.HasFlag(ModifierKeys.Alt)) mapped += "Alt+";
            if (modifiers.HasFlag(ModifierKeys.Shift)) mapped += "Shift+";
            mapped += winFormsKey.ToString();

            if (sender is TextBox tb)
            {
                tb.Text = mapped;
                PrefsChanged(null, null);
                EndHotkeyListening();
            }
        }

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Keyboard.ClearFocus();
            RootGrid.Focus();
        }

        // --- Quick-Move Grid ---
        private void BtnMoveTL_Click(object sender, RoutedEventArgs e) { SetPos(0, 0); }
        private void BtnMoveTC_Click(object sender, RoutedEventArgs e) { SetPos((SystemParameters.WorkArea.Width - _mainWindow.ActualWidth) / 2, 0); }
        private void BtnMoveTR_Click(object sender, RoutedEventArgs e) { SetPos(SystemParameters.WorkArea.Width - _mainWindow.ActualWidth, 0); }
        
        private void BtnMoveCL_Click(object sender, RoutedEventArgs e) { SetPos(0, (SystemParameters.WorkArea.Height - _mainWindow.ActualHeight) / 2); }
        private void BtnMoveCC_Click(object sender, RoutedEventArgs e) { SetPos((SystemParameters.WorkArea.Width - _mainWindow.ActualWidth) / 2, (SystemParameters.WorkArea.Height - _mainWindow.ActualHeight) / 2); }
        private void BtnMoveCR_Click(object sender, RoutedEventArgs e) { SetPos(SystemParameters.WorkArea.Width - _mainWindow.ActualWidth, (SystemParameters.WorkArea.Height - _mainWindow.ActualHeight) / 2); }

        private void BtnMoveBL_Click(object sender, RoutedEventArgs e) { SetPos(0, SystemParameters.WorkArea.Height - _mainWindow.ActualHeight); }
        private void BtnMoveBC_Click(object sender, RoutedEventArgs e) { SetPos((SystemParameters.WorkArea.Width - _mainWindow.ActualWidth) / 2, SystemParameters.WorkArea.Height - _mainWindow.ActualHeight); }
        private void BtnMoveBR_Click(object sender, RoutedEventArgs e) { SetPos(SystemParameters.WorkArea.Width - _mainWindow.ActualWidth, SystemParameters.WorkArea.Height - _mainWindow.ActualHeight); }

        private void SetPos(double x, double y)
        {
            _mainWindow.Left = x;
            _mainWindow.Top = y;
            SaveQuickPos();
        }

        private async void SaveQuickPos()
        {
            await System.Threading.Tasks.Task.Delay(50);
            _settings.X = _mainWindow.Left;
            _settings.Y = _mainWindow.Top;
            UpdatePositionUI();
            _settings.Save();
        }

        // --- Toggle Buttons ---
        private void BtnToggleMatchLogic_Click(object sender, RoutedEventArgs e)
        {
            _settings.MatchAllConditions = !_settings.MatchAllConditions;
            string tip = _settings.MatchAllConditions
                ? "Macro restarts only when ALL enabled\nconditions are met."
                : "Macro restarts when ANY enabled condition\nis met.";
            BtnToggleMatchLogic.Content = _settings.MatchAllConditions ? "Match: ALL" : "Match: ANY";
            BtnToggleMatchLogic.Tag = tip;
            RefreshHoverTooltip(tip);
            SaveSettingsFromUI();
        }

        private void BtnToggleRestartMode_Click(object sender, RoutedEventArgs e)
        {
            _settings.UseSmartRestart = !_settings.UseSmartRestart;
            string tip = _settings.UseSmartRestart
                ? "Once the condition is met, the macro will wait\nfor it to disappear before allowing further restarts."
                : "Triggers instantly and repeatedly as long as\ncondition is met.";
            BtnToggleRestartMode.Content = _settings.UseSmartRestart ? "Logic: SEQUENTIAL" : "Logic: REPETITIVE";
            BtnToggleRestartMode.Tag = tip;
            BtnToggleRestrictedMode.Visibility = _settings.UseSmartRestart ? Visibility.Visible : Visibility.Collapsed;
            RefreshHoverTooltip(tip);
            SaveSettingsFromUI();
        }

        private void BtnToggleRestrictedMode_Click(object sender, RoutedEventArgs e)
        {
            _settings.WaitConditionToRestart = !_settings.WaitConditionToRestart;
            string tip = _settings.WaitConditionToRestart
                ? "Macro pauses at the end of the timeline\nand waits for conditions before looping."
                : "Macro loops naturally regardless of\nconditions.";
            BtnToggleRestrictedMode.Content = _settings.WaitConditionToRestart ? "Restricted: ON" : "Restricted: OFF";
            BtnToggleRestrictedMode.Tag = tip;
            RefreshHoverTooltip(tip);
            SaveSettingsFromUI();
        }

        // --- Conditions ---
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
            if (sender is FrameworkElement fe && fe.DataContext is RestartCondition cond)
            {
                cond.IsEnabled = !cond.IsEnabled;
                SaveSettingsFromUI();
                RefreshConditionsList();
                e.Handled = true;
            }
        }

        private void BtnAddCondition_Click(object sender, RoutedEventArgs e)
        {
            if (_addWindow != null)
            {
                if (_addWindow.WindowState == WindowState.Minimized) _addWindow.WindowState = WindowState.Normal;
                _addWindow.Activate();
                return;
            }

            _addWindow = new ConditionEditorWindow(new RestartCondition(), "Add Condition", name => IsNameDuplicate(name, null), _settings);
            _addWindow.Owner = this;
            _addWindow.Closed += (s, args) =>
            {
                var ed = (ConditionEditorWindow)s;
                if (ed.IsSaved)
                {
                    if (_settings.RestartConditions == null) _settings.RestartConditions = new List<RestartCondition>();
                    _settings.RestartConditions.Add(ed.Condition);
                    SaveSettingsFromUI();
                    RefreshConditionsList();
                }
                _addWindow = null;
            };
            _addWindow.Show();
        }

        private void BtnEditCondition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.DataContext is RestartCondition cond)
            {
                if (_editWindows.TryGetValue(cond, out var existing))
                {
                    if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    return;
                }

                var editor = new ConditionEditorWindow(cond, "Edit Condition", name => IsNameDuplicate(name, cond), _settings);
                editor.Owner = this;
                _editWindows[cond] = editor;
                editor.Closed += (s, args) =>
                {
                    var ed = (ConditionEditorWindow)s;
                    if (ed.IsSaved)
                    {
                        SaveSettingsFromUI();
                        RefreshConditionsList();
                    }
                    _editWindows.Remove(cond);
                };
                editor.Show();
                e.Handled = true;
            }
        }

        private bool IsNameDuplicate(string name, RestartCondition current)
        {
            if (_settings.RestartConditions == null) return false;
            foreach (var c in _settings.RestartConditions)
            {
                if (!object.ReferenceEquals(c, current) && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void BtnDeleteCondition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement btn && btn.DataContext is RestartCondition cond)
            {
                bool confirmed = _settings.HideDeleteConfirmation;
                if (!confirmed)
                {
                    bool dontAsk;
                    confirmed = ThemedConfirmWindow.Show(this, $"Are you sure you want to delete the condition \"{cond.Name}\"?", out dontAsk);
                    if (dontAsk && confirmed)
                    {
                        _settings.HideDeleteConfirmation = true;
                        ChkShowConfirmations.IsChecked = false;
                        _settings.Save();
                    }
                }

                if (confirmed)
                {
                    _settings.RestartConditions.Remove(cond);
                    SaveSettingsFromUI();
                    RefreshConditionsList();
                }
                e.Handled = true;
            }
        }

        // --- Import/Export ---
        private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", DefaultExt = ".json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    SaveSettingsFromUI();
                    string dir = System.IO.Path.GetDirectoryName(dlg.FileName);
                    if (dir != null && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(dlg.FileName, json);
                }
                catch { }
            }
        }

        private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = System.IO.File.ReadAllText(dlg.FileName);
                    var imported = System.Text.Json.JsonSerializer.Deserialize<Settings>(json);
                    if (imported != null)
                    {
                        _settings.RecHotkey = imported.RecHotkey;
                        _settings.PlayHotkey = imported.PlayHotkey;
                        _settings.PauseHotkey = imported.PauseHotkey;
                        _settings.CustomSpeed = imported.CustomSpeed;
                        _settings.LoopCount = imported.LoopCount;
                        _settings.AlwaysOnTop = imported.AlwaysOnTop;
                        _settings.LoopContinuous = imported.LoopContinuous;
                        _settings.UseCustomSpeed = imported.UseCustomSpeed;
                        _settings.HideDeleteConfirmation = imported.HideDeleteConfirmation;
                        _settings.ConditionsPollingInterval = imported.ConditionsPollingInterval;
                        _settings.UseSmartRestart = imported.UseSmartRestart;
                        _settings.WaitConditionToRestart = imported.WaitConditionToRestart;
                        _settings.MatchAllConditions = imported.MatchAllConditions;
                        _settings.RestartConditions = imported.RestartConditions;
                        _settings.IsDarkTheme = imported.IsDarkTheme;
                        _settings.UseSmartRestart = imported.UseSmartRestart;

                        ThemeEngine.Apply(_settings.IsDarkTheme);
                        LoadSettingsToUI();
                        SetActiveTab(_isBasicTab);
                        _macroService.RegisterHotkeys(_settings);
                        _mainWindow.Topmost = _settings.AlwaysOnTop;
                        _settings.Save();
                    }
                }
                catch { }
            }
        }

        private void BtnImportInformaalTask_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "InformaalTask Scripts (*.txt)|*.txt|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(dlg.FileName);
                    _macroService.ImportInformaalTask(lines);
                    _mainWindow.RefreshTitleBar();
                    ThemedConfirmWindow.Show(this, $"Successfully converted InformaalTask macro:\n{System.IO.Path.GetFileName(dlg.FileName)}", out _, "OK", showDontAsk: false, isPositive: true);
                }
                catch (Exception ex)
                {
                    ThemedConfirmWindow.Show(this, "Failed to parse InformaalTask file: " + ex.Message, out _, "OK", showDontAsk: false, isPositive: false);
                }
            }
        }

        private void BtnImportTinyTask_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "TinyTask Recordings (*.rec)|*.rec|All Files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    byte[] rawData = System.IO.File.ReadAllBytes(dlg.FileName);
                    _macroService.ImportTinyTask(rawData);
                    _mainWindow.RefreshTitleBar();
                    ThemedConfirmWindow.Show(this, $"Successfully extracted generic TinyTask records from:\n{System.IO.Path.GetFileName(dlg.FileName)}", out _, "OK", showDontAsk: false, isPositive: true);
                }
                catch (Exception ex)
                {
                    ThemedConfirmWindow.Show(this, "Failed to parse TinyTask binary: " + ex.Message, out _, "OK", showDontAsk: false, isPositive: false);
                }
            }
        }

        // --- Window Controls ---
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.IsLoaded)
            {
                _settings.SettingsWindowX = this.Left;
                _settings.SettingsWindowY = this.Top;
                _settings.Save();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) 
        {
            SaveSettingsFromUI();
            _settings.Save();
            this.Close();
        }

        // --- Tooltip ---
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var element = Mouse.DirectlyOver as FrameworkElement;
            string tip = null;
            while (element != null)
            {
                if (element is ComboBoxItem) { element = VisualTreeHelper.GetParent(element) as FrameworkElement; continue; }
                if (element.Tag is string tagStr && !string.IsNullOrEmpty(tagStr)) { tip = tagStr; break; }
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            if (!string.IsNullOrEmpty(tip))
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
                return;
            }
            HoverTooltip.IsOpen = false;
        }

        protected override void OnMouseLeave(MouseEventArgs e) { base.OnMouseLeave(e); HoverTooltip.IsOpen = false; }

        private void RefreshHoverTooltip(string newTip)
        {
            if (HoverTooltip.IsOpen)
            {
                TxtHoverTooltip.Text = newTip;
                TxtHoverTooltip.UpdateLayout();
                double w = TxtHoverTooltip.ActualWidth + 20;
                double h = TxtHoverTooltip.ActualHeight + 12;
                var pos = PointToScreen(Mouse.GetPosition(this));
                double targetX = pos.X + 15;
                double targetY = pos.Y + 15;
                if (targetX + w > SystemParameters.VirtualScreenWidth) targetX = pos.X - w - 5;
                if (targetY + h > SystemParameters.VirtualScreenHeight) targetY = pos.Y - h - 5;
                HoverTooltip.HorizontalOffset = targetX;
                HoverTooltip.VerticalOffset = targetY;
            }
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !NumberOnlyRegex.IsMatch(e.Text);
        }
    }
}
