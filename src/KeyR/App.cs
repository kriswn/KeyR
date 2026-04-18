using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;

namespace SupTask;

public class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
    {
        System.Windows.EventManager.RegisterClassHandler(typeof(System.Windows.Window), System.Windows.Window.LoadedEvent, new System.Windows.RoutedEventHandler((s, ev) => {
            if (s is System.Windows.Window w) {
                if (w.Title != null) w.Title = w.Title.Replace("SupTask", "KeyR");
                ReplaceTextForWindow(w);
            }
        }));
		base.OnStartup(e);
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ev)
		{
			MessageBox.Show($"Unhandled Exception: {ev.ExceptionObject}");
		};
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

