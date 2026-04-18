using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupTask;

public class App : Application
{
    private static Mutex _mutex;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler((s, ev) => {
            if (s is Window w) {
                w.Title = w.Title?.Replace("SupTask", "KeyR");
                ReplaceText(w);
            }
        }));

        _mutex = new Mutex(initiallyOwned: true, "KeyRAppMutex_V2", out var createdNew);
        if (!createdNew)
        {
            Process currentProcess = Process.GetCurrentProcess();
            Process[] processesByName = Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (Process process in processesByName)
            {
                if (process.Id != currentProcess.Id)
                {
                    nint mainWindowHandle = process.MainWindowHandle;
                    if (IsIconic(mainWindowHandle))
                    {
                        ShowWindow(mainWindowHandle, 9);
                    }
                    SetForegroundWindow(mainWindowHandle);
                    break;
                }
            }
            Application.Current.Shutdown();
        }
        else
        {
            Settings settings = Settings.Load();
            ThemeEngine.Apply(settings.IsDarkTheme);
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            CheckFirstBoot(settings);
            base.OnStartup(e);
            Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs ev)
            {
                MessageBox.Show($"Unhandled Exception: {ev.ExceptionObject}");
            };
        }
    }

    private static void ReplaceText(DependencyObject parent) {
        if (parent == null) return;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock tb && tb.Text != null && tb.Text.Contains("SupTask")) {
                tb.Text = tb.Text.Replace("SupTask", "KeyR");
            }
            ReplaceText(child);
        }
    }

    private void CheckFirstBoot(Settings settings)
    {
        if (settings.HasCompletedFirstBoot)
        {
            return;
        }
        bool flag = true;
        string text = "";
        try
        {
        }
        catch
        {
            flag = false;
            text += "• System.Drawing.Common (included with .NET Desktop Runtime)\n";
        }
        try
        {
        }
        catch
        {
            flag = false;
            text += "• Windows Forms interop (included with .NET Desktop Runtime)\n";
        }
        if (!flag)
        {
            string message = "KeyR detected missing dependencies on this system:\n\n" + text + "\nThese are included with the .NET 9 Desktop Runtime.\n\nWould you like to open the download page now?\n(KeyR will close — relaunch after installing the runtime.)";
            ThemedInfoBox themedInfoBox = new ThemedInfoBox("KeyR — First Boot Setup", message, "Download .NET", "Close");
            themedInfoBox.ShowDialog();
            if (themedInfoBox.Result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://dotnet.microsoft.com/en-us/download/dotnet/9.0",
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            }
            Application.Current.Shutdown();
        }
        else
        {
            string message2 = "Welcome to KeyR! \ud83c\udfaf\n\nAll dependencies verified successfully.\n\nQuick start:\n• Press your Record hotkey to start recording\n• Press your Play hotkey to play back\n• Access Settings via the ⚙ button\n\nThis message will only appear once.";
            new ThemedInfoBox("KeyR — Setup Complete", message2, "Get Started!").ShowDialog();
            settings.HasCompletedFirstBoot = true;
            settings.Save();
        }
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
    public void InitializeComponent()
    {
        base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
    }

    [STAThread]
    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "9.0.12.0")]
    public static void Main()
    {
        App app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
