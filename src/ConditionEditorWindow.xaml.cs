using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeyR
{
    public partial class ConditionEditorWindow : Window
    {
        public bool IsSaved { get; private set; } = false;
        public RestartCondition Condition { get; private set; }

        private Func<string, bool> _isDuplicate;
        private Settings _settings;

        public ConditionEditorWindow(RestartCondition existingCondition, string title, Func<string, bool> isNameDuplicate, Settings settings)
        {
            InitializeComponent();
            _isDuplicate = isNameDuplicate;
            _settings = settings;
            
            TxtWindowTitle.Text = title;
            Condition = existingCondition ?? new RestartCondition();

            ThemeEngine.ApplyFontScale(this, ThemeEngine.FontScale);

            // Restore position
            if (_settings.ConditionWindowX != -1 && _settings.ConditionWindowY != -1)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = _settings.ConditionWindowX;
                this.Top = _settings.ConditionWindowY;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            PopulateData();
        }

        private void PopulateData()
        {
            TxtName.Text = string.IsNullOrEmpty(Condition.Name) ? "New Condition" : Condition.Name;
            CmbType.SelectedIndex = (int)Condition.Type;
            TxtSeconds.Text = Condition.TimePassedSeconds.ToString();
            
            TxtImagePath.Text = Condition.ImagePath;
            TxtTolerance.Text = Condition.Tolerance.ToString();

            TxtMatchText.Text = Condition.MatchedText;
            CmbMatchType.SelectedIndex = Condition.InvertMatch ? 1 : 0;

            ChkFullScreen.IsChecked = Condition.IsFullScreen;
            TxtX1.Text = Condition.X1.ToString();
            TxtY1.Text = Condition.Y1.ToString();
            TxtX2.Text = Condition.X2.ToString();
            TxtY2.Text = Condition.Y2.ToString();
            
            UpdateView();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.IsLoaded && _settings != null)
            {
                _settings.ConditionWindowX = this.Left;
                _settings.ConditionWindowY = this.Top;
                _settings.Save();
            }
        }

        private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateView();
        }

        private void UpdateView()
        {
            if (PanelTime == null) return;
            
            PanelTime.Visibility = Visibility.Collapsed;
            PanelImage.Visibility = Visibility.Collapsed;
            PanelText.Visibility = Visibility.Collapsed;
            PanelRegion.Visibility = Visibility.Collapsed;

            if (CmbType.SelectedIndex == 0)
            {
                PanelTime.Visibility = Visibility.Visible;
            }
            else if (CmbType.SelectedIndex == 1)
            {
                PanelImage.Visibility = Visibility.Visible;
                PanelRegion.Visibility = Visibility.Visible;
            }
            else if (CmbType.SelectedIndex == 2)
            {
                PanelText.Visibility = Visibility.Visible;
                PanelRegion.Visibility = Visibility.Visible;
            }
        }

        private void ChkFullScreen_Changed(object sender, RoutedEventArgs e)
        {
            if (GridCoordinates != null)
            {
                GridCoordinates.Visibility = ChkFullScreen.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                TxtImagePath.Text = dlg.FileName;
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnCancel_Click(object sender, RoutedEventArgs e) => this.Close();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Please enter a name for the condition.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isDuplicate != null && _isDuplicate(TxtName.Text.Trim()))
            {
                ThemedConfirmWindow.Show(this, "A condition with this name already exists.", out _, "OK", showDontAsk: false, isPositive: true);
                this.Activate();
                this.Focus();
                return;
            }

            Condition.Name = TxtName.Text.Trim();
            Condition.Type = (ConditionType)CmbType.SelectedIndex;

            if (int.TryParse(TxtSeconds.Text, out int secs)) Condition.TimePassedSeconds = secs;
            
            Condition.ImagePath = TxtImagePath.Text;
            if (int.TryParse(TxtTolerance.Text, out int tol)) Condition.Tolerance = tol;

            Condition.MatchedText = TxtMatchText.Text;
            Condition.InvertMatch = CmbMatchType.SelectedIndex == 1;

            Condition.IsFullScreen = ChkFullScreen.IsChecked == true;
            
            if (int.TryParse(TxtX1.Text, out int x1)) Condition.X1 = x1;
            if (int.TryParse(TxtY1.Text, out int y1)) Condition.Y1 = y1;
            if (int.TryParse(TxtX2.Text, out int x2)) Condition.X2 = x2;
            if (int.TryParse(TxtY2.Text, out int y2)) Condition.Y2 = y2;

            this.IsSaved = true;
            this.Close();
        }

        private async void BtnSelectRegion_Click(object sender, RoutedEventArgs e)
        {
            var owner = this.Owner;
            this.WindowState = WindowState.Minimized;
            if (owner != null) owner.WindowState = WindowState.Minimized;

            await System.Threading.Tasks.Task.Delay(300);

            var overlay = new RegionSelectOverlay();
            if (overlay.ShowDialog() == true)
            {
                TxtX1.Text = overlay.SelectedRect.X.ToString();
                TxtY1.Text = overlay.SelectedRect.Y.ToString();
                TxtX2.Text = (overlay.SelectedRect.X + overlay.SelectedRect.Width).ToString();
                TxtY2.Text = (overlay.SelectedRect.Y + overlay.SelectedRect.Height).ToString();
            }

            this.WindowState = WindowState.Normal;
            if (owner != null) owner.WindowState = WindowState.Normal;
            this.Activate();
        }

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

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }

    public class RegionSelectOverlay : Window
    {
        public Int32Rect SelectedRect { get; private set; }
        private System.Windows.Point _startPoint;
        private System.Windows.Shapes.Rectangle _selectionBox;
        private Canvas _canvas;

        public RegionSelectOverlay()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
            this.Topmost = true;
            this.WindowState = WindowState.Maximized;
            this.Cursor = Cursors.Cross;

            _canvas = new Canvas { Background = Brushes.Transparent };
            this.Content = _canvas;

            _selectionBox = new System.Windows.Shapes.Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)),
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_selectionBox);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(_canvas);
            Canvas.SetLeft(_selectionBox, _startPoint.X);
            Canvas.SetTop(_selectionBox, _startPoint.Y);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            _selectionBox.Visibility = Visibility.Visible;
            _canvas.CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_canvas.IsMouseCaptured)
            {
                var pos = e.GetPosition(_canvas);
                double x = Math.Min(pos.X, _startPoint.X);
                double y = Math.Min(pos.Y, _startPoint.Y);
                double w = Math.Abs(pos.X - _startPoint.X);
                double h = Math.Abs(pos.Y - _startPoint.Y);
                Canvas.SetLeft(_selectionBox, x);
                Canvas.SetTop(_selectionBox, y);
                _selectionBox.Width = w;
                _selectionBox.Height = h;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _canvas.ReleaseMouseCapture();
            SelectedRect = new Int32Rect(
                (int)Canvas.GetLeft(_selectionBox),
                (int)Canvas.GetTop(_selectionBox),
                (int)_selectionBox.Width,
                (int)_selectionBox.Height);
            this.DialogResult = true;
            this.Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { this.DialogResult = false; this.Close(); }
        }
    }
}
