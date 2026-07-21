using Autofac;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LingXuZhi.App.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    /// <summary>自定义标题栏元素,供 MainWindow.SetTitleBar 注册为拖拽区。</summary>
    public UIElement TitleBarElement => AppTitleBar;

    public MainPage()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeAsync();
            // 初始化期间控件获焦可能把右栏滚走,恢复到顶部
            RightPanelScroll.ChangeView(null, 0, null, disableAnimation: true);
        };
    }
}
