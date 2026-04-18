using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace KeyR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex _mutex = null;
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        const string appName = "KeyRAppMutex_V2";
        bool createdNew;

        _mutex = new Mutex(true, appName, out createdNew);

        if (!createdNew)
        {
            // App is already running
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id)
                {
                    IntPtr hWnd = process.MainWindowHandle;
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, 9); // SW_RESTORE
                    }
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
            Application.Current.Shutdown();
            return;
        }

        var settings = Settings.Load();
        ThemeEngine.Apply(settings.IsDarkTheme);

        // First-boot dependency check using Themed window
        CheckFirstBoot(settings);

        base.OnStartup(e);
        Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

        AppDomain.CurrentDomain.UnhandledException += (s, ev) => 
        {
            MessageBox.Show($"Unhandled Exception: {ev.ExceptionObject}");
        };
    }

    /// <summary>
    /// On first boot, verify essential dependencies are available and prompt the user 
    /// to download them if missing. Subsequent boots skip this check.
    /// </summary>
    private void CheckFirstBoot(Settings settings)
    {
        // Skip if already initialized
        if (settings.HasCompletedFirstBoot) return;

        bool allGood = true;
        string missingItems = "";

        // Check 1: .NET Desktop Runtime (System.Drawing.Common is the heaviest dependency)
        try
        {
            // Probe for System.Drawing.Common — needed for image capture / OCR
            var _ = typeof(System.Drawing.Bitmap);
        }
        catch
        {
            allGood = false;
            missingItems += "• System.Drawing.Common (included with .NET Desktop Runtime)\n";
        }

        // Check 2: Verify the Windows Forms interop assemblies loaded (used for keyboard hooks)
        try
        {
            var _ = typeof(System.Windows.Forms.Keys);
        }
        catch
        {
            allGood = false;
            missingItems += "• Windows Forms interop (included with .NET Desktop Runtime)\n";
        }

        if (!allGood)
        {
            var msg = "KeyR detected missing dependencies on this system:\n\n" +
                      missingItems + "\n" +
                      "These are included with the .NET 9 Desktop Runtime.\n\n" +
                      "Would you like to open the download page now?\n" +
                      "(KeyR will close — relaunch after installing the runtime.)";

            var box = new ThemedInfoBox("KeyR — First Boot Setup", msg, "Download .NET", "Close");
            box.ShowDialog();

            if (box.Result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://dotnet.microsoft.com/en-us/download/dotnet/9.0",
                        UseShellExecute = true
                    });
                }
                catch { }
            }

            Application.Current.Shutdown();
            return;
        }

        // First boot passed — show welcome message
        var welcomeMsg = "Welcome to KeyR! 🎯\n\n" +
                         "All dependencies verified successfully.\n\n" +
                         "Quick start:\n" +
                         "• Press your Record hotkey to start recording\n" +
                         "• Press your Play hotkey to play back\n" +
                         "• Access Settings via the ⚙ button\n\n" +
                         "This message will only appear once.";
                         
        var welcomeBox = new ThemedInfoBox("KeyR — Setup Complete", welcomeMsg, "Get Started!");
        welcomeBox.ShowDialog();

        // Update settings flag so we don't check again
        settings.HasCompletedFirstBoot = true;
        settings.Save();
    }
}
