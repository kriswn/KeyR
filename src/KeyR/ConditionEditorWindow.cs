using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace SupTask;

public class ConditionEditorWindow : Window, IComponentConnector
{
	private Func<string, bool> _isDuplicate;

	private Settings _settings;

	internal Popup HoverTooltip;

	internal TextBlock TxtHoverTooltip;

	internal TextBlock TxtWindowTitle;

	internal TextBox TxtName;

	internal ComboBox CmbType;

	internal StackPanel PanelTime;

	internal TextBox TxtSeconds;

	internal StackPanel PanelImage;

	internal TextBox TxtImagePath;

	internal TextBox TxtTolerance;

	internal StackPanel PanelText;

	internal ComboBox CmbMatchType;

	internal TextBox TxtMatchText;

	internal StackPanel PanelRegion;

	internal CheckBox ChkFullScreen;

	internal Grid GridCoordinates;

	internal TextBox TxtX1;

	internal TextBox TxtY1;

	internal TextBox TxtX2;

	internal TextBox TxtY2;

	private bool _contentLoaded;

	public bool IsSaved { get; private set; }

	public RestartCondition Condition { get; private set; }

	public ConditionEditorWindow(RestartCondition existingCondition, string title, Func<string, bool> isNameDuplicate, Settings settings)
	{
		InitializeComponent();
		_isDuplicate = isNameDuplicate;
		_settings = settings;
		TxtWindowTitle.Text = title.ToUpper();
		Condition = existingCondition ?? new RestartCondition();
		if (_settings.ConditionWindowX != -1.0 && _settings.ConditionWindowY != -1.0)
		{
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = _settings.ConditionWindowX;
			base.Top = _settings.ConditionWindowY;
		}
		else
		{
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		}
		PopulateData();
	}

	private void PopulateData()
	{
		TxtName.Text = (string.IsNullOrEmpty(Condition.Name) ? "New Condition" : Condition.Name);
		CmbType.SelectedIndex = (int)Condition.Type;
		TxtSeconds.Text = Condition.TimePassedSeconds.ToString();
		TxtImagePath.Text = Condition.ImagePath;
		TxtTolerance.Text = Condition.Tolerance.ToString();
		TxtMatchText.Text = Condition.MatchedText;
		CmbMatchType.SelectedIndex = (Condition.InvertMatch ? 1 : 0);
		ChkFullScreen.IsChecked = Condition.IsFullScreen;
		TxtX1.Text = Condition.X1.ToString();
		TxtY1.Text = Condition.Y1.ToString();
		TxtX2.Text = Condition.X2.ToString();
		TxtY2.Text = Condition.Y2.ToString();
		UpdateView();
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void Window_LocationChanged(object sender, EventArgs e)
	{
		if (base.IsLoaded && _settings != null)
		{
			_settings.ConditionWindowX = base.Left;
			_settings.ConditionWindowY = base.Top;
			_settings.Save();
		}
	}

	private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateView();
	}

	private void UpdateView()
	{
		if (PanelTime != null)
		{
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
	}

	private void ChkFullScreen_Changed(object sender, RoutedEventArgs e)
	{
		if (GridCoordinates != null)
		{
			GridCoordinates.Visibility = ((ChkFullScreen.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			TxtImagePath.Text = openFileDialog.FileName;
		}
	}

	private void BtnMinimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void BtnSave_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(TxtName.Text))
		{
			MessageBox.Show("Please enter a name for the condition.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (_isDuplicate != null && _isDuplicate(TxtName.Text.Trim()))
		{
			ThemedConfirmWindow.Show(this, "A condition with this name already exists.", out var _, "OK", showDontAsk: false, isPositive: true);
			Activate();
			Focus();
			return;
		}
		Condition.Name = TxtName.Text.Trim();
		Condition.Type = (ConditionType)CmbType.SelectedIndex;
		if (int.TryParse(TxtSeconds.Text, out var result))
		{
			Condition.TimePassedSeconds = result;
		}
		Condition.ImagePath = TxtImagePath.Text;
		if (int.TryParse(TxtTolerance.Text, out var result2))
		{
			Condition.Tolerance = result2;
		}
		Condition.MatchedText = TxtMatchText.Text;
		Condition.InvertMatch = CmbMatchType.SelectedIndex == 1;
		Condition.IsFullScreen = ChkFullScreen.IsChecked == true;
		if (int.TryParse(TxtX1.Text, out var result3))
		{
			Condition.X1 = result3;
		}
		if (int.TryParse(TxtY1.Text, out var result4))
		{
			Condition.Y1 = result4;
		}
		if (int.TryParse(TxtX2.Text, out var result5))
		{
			Condition.X2 = result5;
		}
		if (int.TryParse(TxtY2.Text, out var result6))
		{
			Condition.Y2 = result6;
		}
		IsSaved = true;
		Close();
	}

	private async void BtnSelectRegion_Click(object sender, RoutedEventArgs e)
	{
		Window owner = base.Owner;
		base.WindowState = WindowState.Minimized;
		if (owner != null)
		{
			owner.WindowState = WindowState.Minimized;
		}
		await Task.Delay(300);
		RegionSelectOverlay regionSelectOverlay = new RegionSelectOverlay();
		if (regionSelectOverlay.ShowDialog() == true)
		{
			TextBox txtX = TxtX1;
			Int32Rect selectedRect = regionSelectOverlay.SelectedRect;
			txtX.Text = selectedRect.X.ToString();
			TextBox txtY = TxtY1;
			selectedRect = regionSelectOverlay.SelectedRect;
			txtY.Text = selectedRect.Y.ToString();
			TextBox txtX2 = TxtX2;
			selectedRect = regionSelectOverlay.SelectedRect;
			int x = selectedRect.X;
			selectedRect = regionSelectOverlay.SelectedRect;
			txtX2.Text = (x + selectedRect.Width).ToString();
			TextBox txtY2 = TxtY2;
			selectedRect = regionSelectOverlay.SelectedRect;
			int y = selectedRect.Y;
			selectedRect = regionSelectOverlay.SelectedRect;
			txtY2.Text = (y + selectedRect.Height).ToString();
		}
		base.WindowState = WindowState.Normal;
		if (owner != null)
		{
			owner.WindowState = WindowState.Normal;
		}
		Activate();
	}

	protected override void OnMouseMove(MouseEventArgs e)
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

	protected override void OnMouseLeave(MouseEventArgs e)
	{
		base.OnMouseLeave(e);
		HoverTooltip.IsOpen = false;
	}

	private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/conditioneditorwindow.xaml", UriKind.Relative);
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
			((ConditionEditorWindow)target).MouseLeftButtonDown += Window_MouseLeftButtonDown;
			((ConditionEditorWindow)target).LocationChanged += Window_LocationChanged;
			break;
		case 2:
			HoverTooltip = (Popup)target;
			break;
		case 3:
			TxtHoverTooltip = (TextBlock)target;
			break;
		case 4:
			TxtWindowTitle = (TextBlock)target;
			break;
		case 5:
			((Button)target).Click += BtnMinimize_Click;
			break;
		case 6:
			((Button)target).Click += BtnCancel_Click;
			break;
		case 7:
			TxtName = (TextBox)target;
			break;
		case 8:
			CmbType = (ComboBox)target;
			CmbType.SelectionChanged += CmbType_SelectionChanged;
			break;
		case 9:
			PanelTime = (StackPanel)target;
			break;
		case 10:
			TxtSeconds = (TextBox)target;
			TxtSeconds.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 11:
			PanelImage = (StackPanel)target;
			break;
		case 12:
			TxtImagePath = (TextBox)target;
			break;
		case 13:
			((Button)target).Click += BtnBrowseImage_Click;
			break;
		case 14:
			TxtTolerance = (TextBox)target;
			TxtTolerance.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 15:
			PanelText = (StackPanel)target;
			break;
		case 16:
			CmbMatchType = (ComboBox)target;
			break;
		case 17:
			TxtMatchText = (TextBox)target;
			break;
		case 18:
			PanelRegion = (StackPanel)target;
			break;
		case 19:
			ChkFullScreen = (CheckBox)target;
			ChkFullScreen.Checked += ChkFullScreen_Changed;
			ChkFullScreen.Unchecked += ChkFullScreen_Changed;
			break;
		case 20:
			GridCoordinates = (Grid)target;
			break;
		case 21:
			TxtX1 = (TextBox)target;
			TxtX1.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 22:
			TxtY1 = (TextBox)target;
			TxtY1.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 23:
			TxtX2 = (TextBox)target;
			TxtX2.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 24:
			TxtY2 = (TextBox)target;
			TxtY2.PreviewTextInput += NumberOnly_PreviewTextInput;
			break;
		case 25:
			((Button)target).Click += BtnSelectRegion_Click;
			break;
		case 26:
			((Button)target).Click += BtnSave_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}


