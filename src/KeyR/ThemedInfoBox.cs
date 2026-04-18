using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SupTask;

public class ThemedInfoBox : Window, IComponentConnector
{
	private bool _hasSecondaryButton;

	internal TextBlock TxtTitle;

	internal TextBlock TxtMessage;

	internal Button BtnSecondary;

	internal Button BtnPrimary;

	private bool _contentLoaded;

	public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

	public ThemedInfoBox(string title, string message, string primaryText, string secondaryText = null)
	{
		InitializeComponent();
		TxtTitle.Text = title;
		TxtMessage.Text = message;
		BtnPrimary.Content = primaryText;
		if (!string.IsNullOrEmpty(secondaryText))
		{
			_hasSecondaryButton = true;
			BtnSecondary.Content = secondaryText;
			BtnSecondary.Visibility = Visibility.Visible;
		}
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void BtnPrimary_Click(object sender, RoutedEventArgs e)
	{
		Result = ((!_hasSecondaryButton) ? MessageBoxResult.OK : MessageBoxResult.Yes);
		base.DialogResult = true;
		Close();
	}

	private void BtnSecondary_Click(object sender, RoutedEventArgs e)
	{
		Result = MessageBoxResult.No;
		base.DialogResult = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/SupTask;component/themedinfobox.xaml", UriKind.Relative);
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
			((ThemedInfoBox)target).MouseLeftButtonDown += Window_MouseLeftButtonDown;
			break;
		case 2:
			TxtTitle = (TextBlock)target;
			break;
		case 3:
			TxtMessage = (TextBlock)target;
			break;
		case 4:
			BtnSecondary = (Button)target;
			BtnSecondary.Click += BtnSecondary_Click;
			break;
		case 5:
			BtnPrimary = (Button)target;
			BtnPrimary.Click += BtnPrimary_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}

