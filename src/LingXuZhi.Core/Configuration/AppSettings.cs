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

    /// <summary>指针灵敏度倍率(以画面中心为锚放大偏移)。</summary>
    [ObservableProperty]
    private double _sensitivity = 1.5;

    /// <summary>平滑强度 0~1:越大越稳。映射 EMA α = 1 - Smoothing。</summary>
    [ObservableProperty]
    private double _smoothing = 0.7;

    /// <summary>屏幕中央死区半径(摄像头画面像素)。</summary>
    [ObservableProperty]
    private double _deadZoneRadius = 40;

    /// <summary>启用鼠标仿真,默认开启。</summary>
    [ObservableProperty]
    private bool _mouseSimulationEnabled = true;

    /// <summary>
    /// 捏合判定阈值(相对手掌宽度,腕→中指根)。
    /// 指尖关键点在指腹中心,实际捏住时两点距离约为掌宽的 10%~20%,故默认 0.12。
    /// </summary>
    [ObservableProperty]
    private double _pinchThreshold = 0.12;

    /// <summary>状态机去抖所需连续帧数。</summary>
    [ObservableProperty]
    private double _debounceFrames = 3;

    /// <summary>滚轮每次触发滚动行数。</summary>
    [ObservableProperty]
    private double _scrollLines = 3;

    /// <summary>捏合拖拽启动阈值(归一化画面距离):捏住移动超过该值进入拖拽。</summary>
    [ObservableProperty]
    private double _dragThreshold = 0.03;

    /// <summary>双击窗口(ms):松开后在窗口内再次捏合并松开判定为双击。</summary>
    [ObservableProperty]
    private double _doublePinchWindowMs = 350;

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
