using Autofac;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LingXuZhi.App.Controls;

public sealed partial class SettingsPanelControl : UserControl
{
    public MainViewModel ViewModel { get; }

    public SettingsPanelControl()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
    }

    public string Format2(double value) => value.ToString("0.00");

    public string FormatPx(double value) => $"{value:F0} px";

    public string FormatInt(double value) => $"{value:F0}";
}
