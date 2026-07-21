using System.Diagnostics;
using System.Threading.Channels;
using LingXuZhi.Vision.Abstractions;

namespace LingXuZhi.App.Services;

/// <summary>一帧的完整识别结果与耗时拆分。</summary>
public sealed record HandTrackingResult(
    PalmDetection? Palm,
    HandLandmarkResult Landmarks,
    int PalmCount,
    double DetectMs,
    double LandmarkMs,
    int FrameWidth,
    int FrameHeight)
{
    public double TotalMs => DetectMs + LandmarkMs;

    public bool Detected => Palm is not null && Landmarks.Detected;
}

/// <summary>
/// 识别管线:帧 → 手掌检测 → 取最高分手掌 → 21 关键点。
/// 后台单线程消费,容量 1 的 Channel 丢旧帧(处理慢时跳帧不堆积)。
/// </summary>
public sealed class HandTrackingService : IDisposable
{
    private readonly IHandDetector _detector;
    private readonly IHandLandmarker _landmarker;
    private readonly Channel<ImageFrame> _frames = Channel.CreateBounded<ImageFrame>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    /// <summary>处理线程上触发,订阅方自行调度回 UI 线程。</summary>
    public event Action<HandTrackingResult>? ResultReady;

    /// <summary>管线内部错误(如模型加载后推理失败),处理线程上触发。</summary>
    public event Action<Exception>? PipelineFaulted;

    public HandTrackingService(IHandDetector detector, IHandLandmarker landmarker)
    {
        _detector = detector;
        _landmarker = landmarker;
    }

    public void Start() => _loop ??= Task.Run(ProcessLoopAsync);

    public void Enqueue(ImageFrame frame) => _frames.Writer.TryWrite(frame);

    public void Dispose()
    {
        _cts.Cancel();
        _frames.Writer.TryComplete();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // 取消引发的异常无需处理
        }
        _cts.Dispose();
    }

    private async Task ProcessLoopAsync()
    {
        var sw = new Stopwatch();
        try
        {
            await foreach (var frame in _frames.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    sw.Restart();
                    var palms = await _detector.DetectAsync(frame, _cts.Token);
                    var detectMs = sw.Elapsed.TotalMilliseconds;

                    var best = palms.Palms.Count > 0 ? palms.Palms[0] : null;
                    var landmarks = HandLandmarkResult.NotDetected;
                    sw.Restart();
                    if (best is not null)
                        landmarks = await _landmarker.DetectAsync(frame, best, _cts.Token);
                    var landmarkMs = sw.Elapsed.TotalMilliseconds;

                    ResultReady?.Invoke(new HandTrackingResult(
                        best, landmarks, palms.Palms.Count, detectMs, landmarkMs, frame.Width, frame.Height));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    PipelineFaulted?.Invoke(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
    }
}
