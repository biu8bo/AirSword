using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.UI;

namespace LingXuZhi.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 用页面内的自定义标题栏替代系统标题栏,系统只保留窗口按钮
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(RootPage.TitleBarElement);
        ConfigureCaptionButtons();
        ApplyWindowIcon();

        // 按工作区自适应:占 90% 并居中,小屏幕直接最大化,避免窗口超出屏幕被裁切
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var width = (int)(workArea.Width * 0.9);
        var height = (int)(workArea.Height * 0.9);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width, height));

        if (workArea.Width < 1500 && AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Maximize();
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
    }

    /// <summary>系统窗口按钮(最小化/最大化/关闭)配色对齐深色主题。</summary>
    private void ConfigureCaptionButtons()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        var textPrimary = Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9);   // Slate-100
        var textSecondary = Color.FromArgb(0xFF, 0x94, 0xA3, 0xB8); // Slate-400
        var panel = Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B);         // Slate-800
        var muted = Color.FromArgb(0xFF, 0x27, 0x2F, 0x42);

        titleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonForegroundColor = textPrimary;
        titleBar.ButtonInactiveForegroundColor = textSecondary;
        titleBar.ButtonHoverBackgroundColor = panel;
        titleBar.ButtonHoverForegroundColor = textPrimary;
        titleBar.ButtonPressedBackgroundColor = muted;
        titleBar.ButtonPressedForegroundColor = textPrimary;
    }
}
