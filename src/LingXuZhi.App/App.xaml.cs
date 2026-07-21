using Autofac;
using LingXuZhi.App.Diagnostics;
using LingXuZhi.App.Hosting;
using LingXuZhi.App.Views;
using Microsoft.UI.Xaml;

namespace LingXuZhi.App;

public partial class App : Application
{
    public static IContainer Container { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        InitializeComponent();
        GlobalExceptionHandler.Register(this);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Container = AppHost.Build();
        _window = new MainWindow();
        _window.Closed += (_, _) => Container.Dispose();
        _window.Activate();
    }
}
