using Autofac;
using LingXuZhi.App.Hosting;
using LingXuZhi.App.Views;
using Microsoft.UI.Xaml;

namespace LingXuZhi.App;

public partial class App : Application
{
    public static IContainer Container { get; private set; } = null!;

    private static readonly string CrashLogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    private Window? _window;

    public App()
    {
        InitializeComponent();

        UnhandledException += (_, e) =>
        {
            WriteCrashLog("XAML UnhandledException", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            File.AppendAllText(CrashLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}\n{ex}\n\n");
        }
        catch
        {
            // 崩溃记录失败时无能为力,避免二次异常
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Container = AppHost.Build();
        _window = new MainWindow();
        _window.Closed += (_, _) => Container.Dispose();
        _window.Activate();
    }
}
