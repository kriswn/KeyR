using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SupTask;

public class PrefsWindow : Window, IComponentConnector
{
	private Settings _settings;

	internal TextBox TxtRecHotkey;

	internal TextBox TxtPlayHotkey;

	internal TextBox TxtLoopCount;

	internal CheckBox ChkCustomSpeed;

	internal TextBox TxtSpeed;

	internal CheckBox ChkAlwaysOnTop;

	internal CheckBox ChkContinuous;

	internal TextBox TxtPosX;

	internal TextBox TxtPosY;

	internal Button BtnCancel;

	internal Button BtnSave;

	private bool _contentLoaded;

	public PrefsWindow(Settings originalSettings)
	{
		InitializeComponent();
		_settings = originalSettings;
		TxtRecHotkey.Text = _settings.RecHotkey;
		TxtPlayHotkey.Text = _settings.PlayHotkey;
		TxtLoopCount.Text = _settings.LoopCount.ToString();
		TxtSpeed.Text = _settings.CustomSpeed.ToString();
		ChkCustomSpeed.IsChecked = _settings.UseCustomSpeed;
		ChkContinuous.IsChecked = _settings.LoopContinuous;
		ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
		TxtPosX.Text = ((int)_settings.X).ToString();
		TxtPosY.Text = ((int)_settings.Y).ToString();
		UpdateUI();
	}

	private void ChkContinuous_Checked(object sender, RoutedEventArgs e)
	{
		UpdateUI();
	}

	private void ChkCustomSpeed_Checked(object sender, RoutedEventArgs e)
	{
		UpdateUI();
	}

	private void UpdateUI()
	{
		if (ChkContinuous.IsChecked == true)
		{
			TxtLoopCount.IsEnabled = false;
		}
		else
		{
			TxtLoopCount.IsEnabled = true;
		}
		TxtSpeed.IsEnabled = ChkCustomSpeed.IsChecked == true;
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if ((originalSource is Border || originalSource is Grid) ? true : false)
		{
			DragMove();
		}
	}

	private void BtnSave_Click(object sender, RoutedEventArgs e)
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
		_settings.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
		if (double.TryParse(TxtPosX.Text, out var result3))
		{
			double num = SystemParameters.VirtualScreenWidth - (base.Owner?.ActualWidth ?? 300.0);
			if (result3 < 0.0)
			{
				result3 = 0.0;
			}
			if (result3 > num)
			{
				result3 = Math.Max(0.0, num);
			}
			_settings.X = result3;
			if (base.Owner != null)
			{
				base.Owner.Left = result3;
			}
		}
		if (double.TryParse(TxtPosY.Text, out var result4))
		{
			double num2 = SystemParameters.VirtualScreenHeight - (base.Owner?.ActualHeight ?? 110.0);
			if (result4 < 0.0)
			{
				result4 = 0.0;
			}
			if (result4 > num2)
			{
				result4 = Math.Max(0.0, num2);
			}
			_settings.Y = result4;
			if (base.Owner != null)
			{
				base.Owner.Top = result4;
			}
		}
		base.DialogResult = true;
		Close();
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private void BtnMoveLeft_Click(object sender, RoutedEventArgs e)
	{
		if (base.Owner != null)
		{
			base.Owner.Left = 0.0;
		}
		SyncPos();
	}

	private void BtnMoveHCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (base.Owner != null)
		{
			Window owner = base.Owner;
			Rect workArea = SystemParameters.WorkArea;
			owner.Left = (workArea.Width - base.Owner.ActualWidth) / 2.0;
		}
		SyncPos();
	}

	private void BtnMoveRight_Click(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (base.Owner != null)
		{
			Window owner = base.Owner;
			Rect workArea = SystemParameters.WorkArea;
			owner.Left = workArea.Width - base.Owner.ActualWidth;
		}
		SyncPos();
	}

	private void BtnMoveTop_Click(object sender, RoutedEventArgs e)
	{
		if (base.Owner != null)
		{
			base.Owner.Top = 0.0;
		}
		SyncPos();
	}

	private void BtnMoveVCenter_Click(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (base.Owner != null)
		{
			Window owner = base.Owner;
			Rect workArea = SystemParameters.WorkArea;
			owner.Top = (workArea.Height - base.Owner.ActualHeight) / 2.0;
		}
		SyncPos();
	}

	private void BtnMoveBottom_Click(object sender, RoutedEventArgs e)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		if (base.Owner != null)
		{
			Window owner = base.Owner;
			Rect workArea = SystemParameters.WorkArea;
			owner.Top = workArea.Height - base.Owner.ActualHeight;
		}
		SyncPos();
	}

	private void SyncPos()
	{
		if (base.Owner != null)
		{
			TxtPosX.Text = ((int)base.Owner.Left).ToString();
			TxtPosY.Text = ((int)base.Owner.Top).ToString();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/prefswindow.xaml", UriKind.Relative);
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
			((PrefsWindow)target).MouseLeftButtonDown += Window_MouseLeftButtonDown;
			break;
		case 2:
			TxtRecHotkey = (TextBox)target;
			break;
		case 3:
			TxtPlayHotkey = (TextBox)target;
			break;
		case 4:
			TxtLoopCount = (TextBox)target;
			break;
		case 5:
			ChkCustomSpeed = (CheckBox)target;
			ChkCustomSpeed.Checked += ChkCustomSpeed_Checked;
			ChkCustomSpeed.Unchecked += ChkCustomSpeed_Checked;
			break;
		case 6:
			TxtSpeed = (TextBox)target;
			break;
		case 7:
			ChkAlwaysOnTop = (CheckBox)target;
			break;
		case 8:
			ChkContinuous = (CheckBox)target;
			ChkContinuous.Checked += ChkContinuous_Checked;
			ChkContinuous.Unchecked += ChkContinuous_Checked;
			break;
		case 9:
			TxtPosX = (TextBox)target;
			break;
		case 10:
			TxtPosY = (TextBox)target;
			break;
		case 11:
			((Button)target).Click += BtnMoveLeft_Click;
			break;
		case 12:
			((Button)target).Click += BtnMoveHCenter_Click;
			break;
		case 13:
			((Button)target).Click += BtnMoveRight_Click;
			break;
		case 14:
			((Button)target).Click += BtnMoveTop_Click;
			break;
		case 15:
			((Button)target).Click += BtnMoveVCenter_Click;
			break;
		case 16:
			((Button)target).Click += BtnMoveBottom_Click;
			break;
		case 17:
			BtnCancel = (Button)target;
			BtnCancel.Click += BtnCancel_Click;
			break;
		case 18:
			BtnSave = (Button)target;
			BtnSave.Click += BtnSave_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}



