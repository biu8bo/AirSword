using Autofac;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LingXuZhi.App.Views;

public sealed partial class MainPage : Page
{
    private bool _helpOpen;

    public MainViewModel ViewModel { get; }

    /// <summary>标题栏拖拽区元素,供 MainWindow.SetTitleBar 注册(不含帮助按钮,保证其可点击)。</summary>
    public UIElement TitleBarElement => DragRegion;

    public MainPage()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
        LoadTitleLogo();
        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeAsync();
            // 初始化期间控件获焦可能把右栏滚走,恢复到顶部
            RightPanelScroll.ChangeView(null, 0, null, disableAnimation: true);
        };
    }

    private async void OnHelpClick(object sender, RoutedEventArgs e)
    {
        if (_helpOpen)
            return;
        _helpOpen = true;
        try
        {
            HelpDialog.XamlRoot = XamlRoot;
            await HelpDialog.ShowAsync();
        }
        finally
        {
            _helpOpen = false;
        }
    }

    private void LoadTitleLogo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (File.Exists(path))
            TitleLogo.Source = new BitmapImage(new Uri(path));
    }
}
