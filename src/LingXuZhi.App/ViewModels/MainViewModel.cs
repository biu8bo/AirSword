using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using LingXuZhi.App.Services;
using LingXuZhi.Core.Configuration;
using LingXuZhi.Core.Gestures;
using LingXuZhi.Platform.Camera;
using LingXuZhi.Vision.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LingXuZhi.App.ViewModels;

public sealed record CameraDeviceOption(int Index, string Name);

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const string LandmarkPlaceholder = "   x: —      y: —      v: —";

    private readonly ICameraSource _camera;
    private readonly HandTrackingService _tracking;
    private readonly GestureControlService _gestures;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _statsTimer;
    private readonly object _frameLock = new();
    private readonly object _resultLock = new();
    private CameraFrame? _pendingFrame;
    private HandTrackingResult? _pendingResult;
    private int _frameCount;
    private int _resultCount;
    private long _lastStatsTicks;
    private bool _initialized;
    private bool _pipelineFaultLogged;
    private bool _lastDetected;

    public AppSettings Settings { get; }

    public ObservableCollection<CameraDeviceOption> CameraDevices { get; } = new();

    public ObservableCollection<string> Resolutions { get; } = new() { "640 × 480", "1280 × 720", "1920 × 1080" };

    /// <summary>21 个关键点坐标行,检测到手时实时刷新。</summary>
    public ObservableCollection<string> LandmarkLines { get; } = new(
        Enumerable.Range(0, 21).Select(i => $"P{i:D2}{LandmarkPlaceholder}"));

    /// <summary>运行日志,底部日志面板数据源。</summary>
    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    private WriteableBitmap? _previewBitmap;

    [ObservableProperty]
    private string _fpsText = "FPS —";

    [ObservableProperty]
    private string _frameResolution = "—";

    [ObservableProperty]
    private string _processingTime = "— ms";

    [ObservableProperty]
    private string _palmScore = "—";

    [ObservableProperty]
    private string _gestureState = "空闲";

    /// <summary>识别状态:检测到/未检测到/多手警告。</summary>
    [ObservableProperty]
    private string _detectionState = "未检测到";

    /// <summary>处理 FPS(识别管线吞吐),与采集 FPS 区分。</summary>
    [ObservableProperty]
    private string _processFpsText = "—";

    [ObservableProperty]
    private string _boundingBoxText = "—";

    [ObservableProperty]
    private string _rotationText = "—";

    [ObservableProperty]
    private string _stateTransition = "—";

    [ObservableProperty]
    private string _rawPointerText = "—";

    [ObservableProperty]
    private string _smoothedPointerText = "—";

    [ObservableProperty]
    private string _deadZoneText = "—";

    [ObservableProperty]
    private string _pinchDistanceText = "—";

    [ObservableProperty]
    private string _mouseDeltaText = "—";

    [ObservableProperty]
    private string _scrollCountText = "0";

    [ObservableProperty]
    private string _clickCountText = "0";

    /// <summary>最新识别结果,预览控件叠加层数据源。</summary>
    [ObservableProperty]
    private HandTrackingResult? _overlayResult;

    /// <summary>最新手势动作(含平滑指针),预览叠加用。</summary>
    [ObservableProperty]
    private MouseAction? _lastMouseAction;

    /// <summary>首帧出画前为 false,预览区显示 Loading。</summary>
    [ObservableProperty]
    private bool _isPreviewReady;

    [ObservableProperty]
    private CameraDeviceOption? _selectedDevice;

    [ObservableProperty]
    private string _selectedResolution = "1280 × 720";

    public MainViewModel(
        ICameraSource camera,
        AppSettings settings,
        HandTrackingService tracking,
        GestureControlService gestures)
    {
        _camera = camera;
        Settings = settings;
        _tracking = tracking;
        _gestures = gestures;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _camera.FrameArrived += OnFrameArrived;
        _tracking.ResultReady += OnTrackingResult;
        _tracking.PipelineFaulted += OnPipelineFaulted;
        Settings.PropertyChanged += OnSettingsChanged;

        // 全局异常同步到底部日志面板(Log 内部已做线程调度)
        Diagnostics.GlobalExceptionHandler.OnExceptionLogged = msg => Log($"异常:{msg}");

        _lastStatsTicks = Environment.TickCount64;
        _statsTimer = _dispatcher.CreateTimer();
        _statsTimer.Interval = TimeSpan.FromMilliseconds(500);
        _statsTimer.Tick += (_, _) => UpdateStats();
        _statsTimer.Start();
    }

    /// <summary>枚举摄像头设备并启动默认设备预览。</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        Log("应用启动,正在探测摄像头设备…");
        _tracking.Start();
        Log("视觉识别管线已启动(OpenCV DNN,双模型)");
        var devices = await Task.Run(() => _camera.EnumerateDevices());
        foreach (var index in devices)
            CameraDevices.Add(new CameraDeviceOption(index, $"摄像头 {index}"));
        Log(devices.Count > 0 ? $"发现 {devices.Count} 个摄像头设备" : "未发现可用摄像头");

        // 触发 OnSelectedDeviceChanged → 启动采集
        SelectedDevice = CameraDevices.FirstOrDefault();
    }

    partial void OnSelectedDeviceChanged(CameraDeviceOption? value)
    {
        if (value is null)
            return;
        Settings.CameraIndex = value.Index;
        RestartCamera();
    }

    partial void OnSelectedResolutionChanged(string value)
    {
        var parts = value.Split('×', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h))
            return;
        Settings.FrameWidth = w;
        Settings.FrameHeight = h;
        if (_camera.IsRunning)
        {
            Log($"切换分辨率:{w} × {h}");
            RestartCamera();
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.Mirror):
                _camera.Mirror = Settings.Mirror;
                Log($"镜像翻转:{(Settings.Mirror ? "开" : "关")}");
                break;
            case nameof(AppSettings.MouseSimulationEnabled):
                Log($"鼠标仿真:{(Settings.MouseSimulationEnabled ? "开" : "关")}");
                break;
        }
    }

    private void RestartCamera()
    {
        var index = Settings.CameraIndex;
        var width = Settings.FrameWidth;
        var height = Settings.FrameHeight;
        var mirror = Settings.Mirror;

        IsPreviewReady = false;
        Log($"启动摄像头 {index}({width} × {height})");

        // Stop 会 Join 采集线程,放后台执行避免卡 UI
        Task.Run(() =>
        {
            _camera.Stop();
            _camera.Mirror = mirror;
            _camera.Start(index, width, height);
        });
    }

    private void OnFrameArrived(CameraFrame frame)
    {
        Interlocked.Increment(ref _frameCount);

        // 送识别管线(容量 1,处理慢时自动丢旧帧)
        _tracking.Enqueue(new ImageFrame(frame.Bgra, frame.Width, frame.Height, ImagePixelFormat.Bgra32));

        bool queueRender;
        lock (_frameLock)
        {
            // UI 渲染慢于采集时只保留最新帧
            queueRender = _pendingFrame is null;
            _pendingFrame = frame;
        }
        if (queueRender)
            _dispatcher.TryEnqueue(RenderPendingFrame);
    }

    private void OnTrackingResult(HandTrackingResult result)
    {
        Interlocked.Increment(ref _resultCount);

        bool queueUpdate;
        lock (_resultLock)
        {
            queueUpdate = _pendingResult is null;
            _pendingResult = result;
        }
        if (queueUpdate)
            _dispatcher.TryEnqueue(ApplyPendingResult);
    }

    private void OnPipelineFaulted(Exception ex)
    {
        if (_pipelineFaultLogged)
            return;
        _pipelineFaultLogged = true;
        Log($"识别管线异常:{ex.Message}");
    }

    private void ApplyPendingResult()
    {
        HandTrackingResult result;
        lock (_resultLock)
        {
            if (_pendingResult is null)
                return;
            result = _pendingResult;
            _pendingResult = null;
        }

        OverlayResult = result;
        ProcessingTime = $"检测 {result.DetectMs:F1} + 关键点 {result.LandmarkMs:F1} = {result.TotalMs:F1} ms";

        var action = _gestures.Process(result.Landmarks, result.FrameWidth, result.FrameHeight);
        LastMouseAction = action;
        GestureState = FormatGesture(action);
        StateTransition = _gestures.LastTransition;
        RawPointerText = $"{action.RawNormalized.X:F3}, {action.RawNormalized.Y:F3}";
        SmoothedPointerText = $"{action.SmoothedNormalized.X:F3}, {action.SmoothedNormalized.Y:F3}";
        DeadZoneText = action.InDeadZone ? "命中" : "否";
        PinchDistanceText = action.PinchLeftDistance < 10
            ? $"拇食 {action.PinchLeftDistance:F3} · 食中 {action.PinchRightDistance:F3} / 阈 {Settings.PinchThreshold:F2}"
            : "—";
        MouseDeltaText = $"{_gestures.LastMouseDx:F1}, {_gestures.LastMouseDy:F1}";
        ScrollCountText = _gestures.ScrollEventCount.ToString();
        ClickCountText = _gestures.ClickEventCount.ToString();

        if (result.Detected)
        {
            var palm = result.Palm!;
            var landmarks = result.Landmarks.Landmarks;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < landmarks.Count && i < 21; i++)
            {
                var p = landmarks[i];
                LandmarkLines[i] = $"P{i:D2}   x: {p.X,6:F1}  y: {p.Y,6:F1}  v: {p.Visibility:F2}";
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }

            PalmScore = palm.Score.ToString("F3");
            BoundingBoxText = $"({minX:F0}, {minY:F0}) - ({maxX:F0}, {maxY:F0})";
            RotationText = $"{palm.RotationDegrees:F1}°";
            DetectionState = result.PalmCount > 1 ? $"检测到 {result.PalmCount} 只手(取最高分)" : "检测到";
            _lastDetected = true;
        }
        else if (_lastDetected)
        {
            _lastDetected = false;
            PalmScore = "—";
            BoundingBoxText = "—";
            RotationText = "—";
            DetectionState = "未检测到";
            for (var i = 0; i < 21; i++)
                LandmarkLines[i] = $"P{i:D2}{LandmarkPlaceholder}";
        }
        else
        {
            DetectionState = "未检测到";
        }
    }

    private static string FormatGesture(MouseAction action) => action.State switch
    {
        MachineState.Moving => "食指·移动",
        MachineState.PinchPending => "捏合·待定(单击/拖拽)",
        MachineState.Dragging => "捏合·拖拽中",
        MachineState.RightClick => "比耶并拢·右键",
        MachineState.Scroll => action.ObservedGesture == GestureKind.OpenPalmThumbIn ? "四指张开·下滚" : "五指张开·上滚",
        _ => action.ObservedGesture switch
        {
            GestureKind.Pointer => "食指(去抖中)",
            GestureKind.PinchLeft => "左键(去抖中)",
            GestureKind.PinchRight => "右键(去抖中)",
            GestureKind.OpenPalm => "上滚(去抖中)",
            GestureKind.OpenPalmThumbIn => "下滚(去抖中)",
            _ => "空闲",
        },
    };

    private void RenderPendingFrame()
    {
        CameraFrame frame;
        lock (_frameLock)
        {
            if (_pendingFrame is null)
                return;
            frame = _pendingFrame.Value;
            _pendingFrame = null;
        }

        if (PreviewBitmap is null
            || PreviewBitmap.PixelWidth != frame.Width
            || PreviewBitmap.PixelHeight != frame.Height)
        {
            PreviewBitmap = new WriteableBitmap(frame.Width, frame.Height);
            FrameResolution = $"{frame.Width} × {frame.Height}";
            Log($"预览出画:{FrameResolution}");
        }

        using (var stream = PreviewBitmap.PixelBuffer.AsStream())
            stream.Write(frame.Bgra, 0, frame.Bgra.Length);
        PreviewBitmap.Invalidate();

        if (!IsPreviewReady)
            IsPreviewReady = true;
    }

    private void UpdateStats()
    {
        var now = Environment.TickCount64;
        var elapsedMs = now - _lastStatsTicks;
        _lastStatsTicks = now;

        var frames = Interlocked.Exchange(ref _frameCount, 0);
        var results = Interlocked.Exchange(ref _resultCount, 0);
        var fps = elapsedMs > 0 ? frames * 1000.0 / elapsedMs : 0;
        var processFps = elapsedMs > 0 ? results * 1000.0 / elapsedMs : 0;
        FpsText = _camera.IsRunning ? $"FPS {fps:F0}" : "FPS —";
        ProcessFpsText = _camera.IsRunning ? $"{processFps:F0}" : "—";
    }

    private const int MaxLogLines = 500;

    /// <summary>线程安全地追加一条日志(自动打时间戳,超限裁剪最旧行)。</summary>
    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        if (_dispatcher.HasThreadAccess)
            AppendLog(line);
        else
            _dispatcher.TryEnqueue(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > MaxLogLines)
            LogLines.RemoveAt(0);
    }

    public void Dispose()
    {
        Diagnostics.GlobalExceptionHandler.OnExceptionLogged = null;
        _statsTimer.Stop();
        Settings.PropertyChanged -= OnSettingsChanged;
        _camera.FrameArrived -= OnFrameArrived;
        _tracking.ResultReady -= OnTrackingResult;
        _tracking.PipelineFaulted -= OnPipelineFaulted;
    }
}
