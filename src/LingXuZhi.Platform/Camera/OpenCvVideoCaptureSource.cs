using System.Runtime.InteropServices;
using OpenCvSharp;

namespace LingXuZhi.Platform.Camera;

/// <summary>基于 OpenCV VideoCapture(DirectShow)的摄像头采集,后台线程循环抓帧。</summary>
public sealed class OpenCvVideoCaptureSource : ICameraSource
{
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
        for (var i = 0; i < maxProbe; i++)
        {
            using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
            if (probe.IsOpened())
                found.Add(i);
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
        using var capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
            return;

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
