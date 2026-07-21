namespace LingXuZhi.Platform.Camera;

/// <summary>一帧 BGRA 图像数据。</summary>
public readonly record struct CameraFrame(byte[] Bgra, int Width, int Height);

/// <summary>摄像头帧采集抽象,阶段 1 由 OpenCV VideoCapture 实现,预留 MediaFoundation 切换。</summary>
public interface ICameraSource : IDisposable
{
    /// <summary>采集线程上触发,处理需自行调度回 UI 线程。</summary>
    event Action<CameraFrame>? FrameArrived;

    /// <summary>水平镜像翻转,可在运行中切换。</summary>
    bool Mirror { get; set; }

    bool IsRunning { get; }

    /// <summary>探测本机可用摄像头,返回可打开的设备索引列表。</summary>
    IReadOnlyList<int> EnumerateDevices(int maxProbe = 5);

    void Start(int cameraIndex, int width, int height);

    void Stop();
}
