using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SupTask;

public class ThemedConfirmWindow : Window, IComponentConnector
{
	internal TextBlock TxtMessage;

	internal CheckBox ChkDontAsk;

	internal Button BtnCancel;

	internal Button BtnConfirm;

	private bool _contentLoaded;

	public bool DontAskAgain { get; private set; }

	public ThemedConfirmWindow(string message, string confirmText = "Delete")
	{
		InitializeComponent();
		TxtMessage.Text = message;
		BtnConfirm.Content = confirmText;
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void BtnConfirm_Click(object sender, RoutedEventArgs e)
	{
		DontAskAgain = ChkDontAsk.IsChecked == true;
		base.DialogResult = true;
		Close();
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		DontAskAgain = ChkDontAsk.IsChecked == true;
		base.DialogResult = false;
		Close();
	}

	public static bool Show(Window owner, string message, out bool dontAskAgain, string confirmText = "Delete", bool showDontAsk = true, bool isPositive = false)
	{
		ThemedConfirmWindow themedConfirmWindow = new ThemedConfirmWindow(message, confirmText);
		themedConfirmWindow.Owner = owner;
		if (!showDontAsk)
		{
			themedConfirmWindow.ChkDontAsk.Visibility = Visibility.Collapsed;
		}
		if (isPositive)
		{
			themedConfirmWindow.BtnConfirm.Style = (Style)themedConfirmWindow.Resources["PositiveBtn"];
		}
		bool valueOrDefault = themedConfirmWindow.ShowDialog() == true;
		dontAskAgain = themedConfirmWindow.DontAskAgain;
		return valueOrDefault;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/themedconfirmwindow.xaml", UriKind.Relative);
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
			((ThemedConfirmWindow)target).MouseLeftButtonDown += Window_MouseLeftButtonDown;
			break;
		case 2:
			TxtMessage = (TextBlock)target;
			break;
		case 3:
			ChkDontAsk = (CheckBox)target;
			break;
		case 4:
			BtnCancel = (Button)target;
			BtnCancel.Click += BtnCancel_Click;
			break;
		case 5:
			BtnConfirm = (Button)target;
			BtnConfirm.Click += BtnConfirm_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

