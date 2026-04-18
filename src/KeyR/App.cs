using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

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
        System.Windows.EventManager.RegisterClassHandler(typeof(System.Windows.Window), System.Windows.Window.LoadedEvent, new System.Windows.RoutedEventHandler((s, ev) => {
            if (s is System.Windows.Window w) {
                if (w.Title != null) w.Title = w.Title.Replace("SupTask", "KeyR");
                ReplaceTextForWindow(w);
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
			ThemeEngine.Apply(Settings.Load().IsDarkTheme);
			base.OnStartup(e);
			AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ev)
			{
				MessageBox.Show($"Unhandled Exception: {ev.ExceptionObject}");
			};
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

    private static void ReplaceTextForWindow(System.Windows.DependencyObject parent) {
        if (parent == null) return;
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++) {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.TextBlock tb && tb.Text != null && tb.Text.Contains("SupTask")) {
                tb.Text = tb.Text.Replace("SupTask", "KeyR");
            }
            ReplaceTextForWindow(child);
        }
    }
}

