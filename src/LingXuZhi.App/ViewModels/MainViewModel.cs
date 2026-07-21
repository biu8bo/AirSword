using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using LingXuZhi.Core.Configuration;
using LingXuZhi.Platform.Camera;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LingXuZhi.App.ViewModels;

public sealed record CameraDeviceOption(int Index, string Name);

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ICameraSource _camera;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _statsTimer;
    private readonly object _frameLock = new();
    private CameraFrame? _pendingFrame;
    private int _frameCount;
    private long _lastStatsTicks;
    private bool _initialized;

    public AppSettings Settings { get; }

    public ObservableCollection<CameraDeviceOption> CameraDevices { get; } = new();

    public ObservableCollection<string> Resolutions { get; } = new() { "640 × 480", "1280 × 720", "1920 × 1080" };

    /// <summary>21 个关键点占位行,阶段 2 填充真实坐标。</summary>
    public ObservableCollection<string> LandmarkLines { get; } = new(
        Enumerable.Range(0, 21).Select(i => $"P{i:D2}   x: —      y: —      z: —"));

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

    [ObservableProperty]
    private CameraDeviceOption? _selectedDevice;

    [ObservableProperty]
    private string _selectedResolution = "1280 × 720";

    public MainViewModel(ICameraSource camera, AppSettings settings)
    {
        _camera = camera;
        Settings = settings;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _camera.FrameArrived += OnFrameArrived;
        Settings.PropertyChanged += OnSettingsChanged;

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
                Log($"鼠标仿真:{(Settings.MouseSimulationEnabled ? "开(阶段 3 生效)" : "关")}");
                break;
        }
    }

    private void RestartCamera()
    {
        var index = Settings.CameraIndex;
        var width = Settings.FrameWidth;
        var height = Settings.FrameHeight;
        var mirror = Settings.Mirror;

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
    }

    private void UpdateStats()
    {
        var now = Environment.TickCount64;
        var elapsedMs = now - _lastStatsTicks;
        _lastStatsTicks = now;

        var frames = Interlocked.Exchange(ref _frameCount, 0);
        var fps = elapsedMs > 0 ? frames * 1000.0 / elapsedMs : 0;
        FpsText = _camera.IsRunning ? $"FPS {fps:F0}" : "FPS —";
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
        _statsTimer.Stop();
        Settings.PropertyChanged -= OnSettingsChanged;
        _camera.FrameArrived -= OnFrameArrived;
    }
}
