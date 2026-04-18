using System.Windows;
using System.Windows.Input;

namespace KeyR
{
    public partial class ThemedConfirmWindow : Window
    {
        public bool DontAskAgain { get; private set; } = false;

        public ThemedConfirmWindow(string message, string confirmText = "Delete")
        {
            InitializeComponent();
            TxtMessage.Text = message;
            BtnConfirm.Content = confirmText;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            DontAskAgain = ChkDontAsk.IsChecked == true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DontAskAgain = ChkDontAsk.IsChecked == true;
            this.DialogResult = false;
            this.Close();
        }

        public static bool Show(Window owner, string message, out bool dontAskAgain, string confirmText = "Delete", bool showDontAsk = true, bool isPositive = false)
        {
            var win = new ThemedConfirmWindow(message, confirmText);
            win.Owner = owner;
            if (!showDontAsk) win.ChkDontAsk.Visibility = Visibility.Collapsed;
            if (isPositive) win.BtnConfirm.Style = (Style)win.Resources["PositiveBtn"];
            
            var result = win.ShowDialog() == true;
            dontAskAgain = win.DontAskAgain;
            return result;
        }
    }
}
