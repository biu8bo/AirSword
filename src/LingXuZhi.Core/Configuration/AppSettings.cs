using CommunityToolkit.Mvvm.ComponentModel;

namespace LingXuZhi.Core.Configuration;

/// <summary>应用参数模型,设置面板双向绑定的数据源。</summary>
public partial class AppSettings : ObservableObject
{
    [ObservableProperty]
    private int _cameraIndex;

    [ObservableProperty]
    private int _frameWidth = 1280;

    [ObservableProperty]
    private int _frameHeight = 720;

    /// <summary>镜像翻转,自拍视角下移动方向符合直觉,默认开启。</summary>
    [ObservableProperty]
    private bool _mirror = true;

    /// <summary>指针灵敏度倍率。</summary>
    [ObservableProperty]
    private double _sensitivity = 1.0;

    /// <summary>平滑系数,0 = 不平滑,1 = 最大平滑。</summary>
    [ObservableProperty]
    private double _smoothing = 0.5;

    /// <summary>屏幕中央死区半径(像素)。</summary>
    [ObservableProperty]
    private double _deadZoneRadius = 40;

    /// <summary>启用鼠标仿真(阶段 3 生效,本阶段仅 UI)。</summary>
    [ObservableProperty]
    private bool _mouseSimulationEnabled;

    /// <summary>手掌检测分数阈值,调高后远/小的手不识别。</summary>
    [ObservableProperty]
    private double _detectionScoreThreshold = 0.5;

    /// <summary>手掌检测 NMS IoU 阈值。</summary>
    [ObservableProperty]
    private double _nmsThreshold = 0.3;

    [ObservableProperty]
    private bool _drawSkeleton = true;

    [ObservableProperty]
    private bool _drawBoundingBox = true;
}
