using Autofac;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace LingXuZhi.App.Controls;

public sealed partial class CameraPreviewControl : UserControl
{
    public MainViewModel ViewModel { get; }

    public CameraPreviewControl()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
    }
}
