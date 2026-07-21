using System.Runtime.InteropServices;
using OpenCvSharp;

namespace LingXuZhi.Platform.Camera;

/// <summary>
/// 基于 OpenCV VideoCapture 的摄像头采集。
/// Windows 优先 Media Foundation(MSMF),失败再回退 DirectShow(DSHOW)。
/// </summary>
public sealed class OpenCvVideoCaptureSource : ICameraSource
{
    /// <summary>打开设备时的后端优先级:MSMF 更稳且少刷 DSHOW 探测警告。</summary>
    private static readonly VideoCaptureAPIs[] PreferredApis =
    {
        VideoCaptureAPIs.MSMF,
        VideoCaptureAPIs.DSHOW,
    };

    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private volatile bool _mirror;

    public event Action<CameraFrame>? FrameArrived;

    public bool Mirror
    {
        get => _mirror;
        set => _mirror = value;
    }

    public bool IsRunning => _thread is { IsAlive: true };

    public IReadOnlyList<int> EnumerateDevices(int maxProbe = 5)
    {
        var found = new List<int>();

        // 探测不存在的 index 时 OpenCV 会打 WARN,压到 ERROR 避免调试输出刷屏
        var previousLevel = Cv2.GetLogLevel();
        Cv2.SetLogLevel(LogLevel.ERROR);
        try
        {
            var consecutiveMiss = 0;
            for (var i = 0; i < maxProbe; i++)
            {
                using var probe = TryOpen(i);
                if (probe.IsOpened())
                {
                    found.Add(i);
                    consecutiveMiss = 0;
                }
                else
                {
                    consecutiveMiss++;
                    // 已找到设备后遇到空槽即可停止;开头连续空也提前结束
                    if (consecutiveMiss >= 2)
                        break;
                }
            }
        }
        finally
        {
            Cv2.SetLogLevel(previousLevel);
        }

        return found;
    }

    public void Start(int cameraIndex, int width, int height)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _thread = new Thread(() => CaptureLoop(cameraIndex, width, height, token))
        {
            IsBackground = true,
            Name = "CameraCapture",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(3));
        _cts?.Dispose();
        _cts = null;
        _thread = null;
    }

    public void Dispose() => Stop();

    private void CaptureLoop(int cameraIndex, int width, int height, CancellationToken token)
    {
        // 切换摄像头时设备可能尚未释放,短暂重试并压低打开失败日志
        var previousLevel = Cv2.GetLogLevel();
        Cv2.SetLogLevel(LogLevel.ERROR);
        VideoCapture? capture = null;
        try
        {
            for (var attempt = 0; attempt < 5 && !token.IsCancellationRequested; attempt++)
            {
                capture = TryOpen(cameraIndex);
                if (capture.IsOpened())
                    break;
                capture.Dispose();
                capture = null;
                Thread.Sleep(150);
            }
        }
        finally
        {
            Cv2.SetLogLevel(previousLevel);
        }

        if (capture is null || !capture.IsOpened())
            return;

        using (capture)
        {
            capture.Set(VideoCaptureProperties.FrameWidth, width);
            capture.Set(VideoCaptureProperties.FrameHeight, height);
            capture.Set(VideoCaptureProperties.Fps, 30);

            using var bgr = new Mat();
            using var bgra = new Mat();
            while (!token.IsCancellationRequested)
            {
                if (!capture.Read(bgr) || bgr.Empty())
                {
                    Thread.Sleep(5);
                    continue;
                }

                if (_mirror)
                    Cv2.Flip(bgr, bgr, FlipMode.Y);

                Cv2.CvtColor(bgr, bgra, ColorConversionCodes.BGR2BGRA);

                var buffer = new byte[(int)bgra.Total() * bgra.ElemSize()];
                Marshal.Copy(bgra.Data, buffer, 0, buffer.Length);
                FrameArrived?.Invoke(new CameraFrame(buffer, bgra.Width, bgra.Height));
            }
        }
    }

    /// <summary>按优先级尝试打开摄像头,全部失败返回未打开的实例(调用方 Dispose)。</summary>
    private static VideoCapture TryOpen(int cameraIndex)
    {
        foreach (var api in PreferredApis)
        {
            var capture = new VideoCapture(cameraIndex, api);
            if (capture.IsOpened())
                return capture;
            capture.Dispose();
        }

        return new VideoCapture();
    }
}
