using System.Windows;
using System.Windows.Input;

namespace KeyR;

public partial class ThemedInfoBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;
    private bool _hasSecondaryButton = false;

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
            this.DragMove();
        }
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        Result = _hasSecondaryButton ? MessageBoxResult.Yes : MessageBoxResult.OK;
        this.DialogResult = true;
        this.Close();
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        this.DialogResult = false;
        this.Close();
    }
}
