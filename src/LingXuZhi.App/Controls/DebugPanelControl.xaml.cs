using Autofac;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LingXuZhi.App.Controls;

public sealed partial class DebugPanelControl : UserControl
{
    public MainViewModel ViewModel { get; }

    public DebugPanelControl()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
    }

    public string ClickScrollText(string clicks, string scrolls) => $"{clicks} / {scrolls}";
}
