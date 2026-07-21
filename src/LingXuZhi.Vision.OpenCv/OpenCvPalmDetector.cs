using LingXuZhi.Core.Configuration;
using LingXuZhi.Vision.Abstractions;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace LingXuZhi.Vision.OpenCv;

/// <summary>
/// MediaPipe 手掌检测(OpenCV Zoo palm_detection_mediapipe_2023feb)。
/// 输入 1×192×192×3 NHWC RGB /255;输出 2016 锚点的框/7 关键点增量与分数。
/// 后处理逻辑对齐 opencv_zoo/models/palm_detection_mediapipe/mp_palmdet.py。
/// </summary>
public sealed class OpenCvPalmDetector : IHandDetector
{
    private const int InputSize = 192;
    private static readonly float[][] Anchors = BuildAnchors();

    private readonly Net _net;
    private readonly string[] _outNames;
    private readonly AppSettings _settings;

    private float ScoreThreshold => (float)_settings.DetectionScoreThreshold;

    private float NmsThreshold => (float)_settings.NmsThreshold;

    public OpenCvPalmDetector(VisionOptions options, AppSettings settings)
    {
        _settings = settings;
        _net = CvDnn.ReadNet(options.PalmDetectionModelPath)
            ?? throw new InvalidOperationException($"无法加载手掌检测模型:{options.PalmDetectionModelPath}");
        _outNames = _net.GetUnconnectedOutLayersNames()!;
    }

    /// <summary>推理在调用线程同步执行(管线本身运行于后台线程,避免额外线程切换)。</summary>
    public Task<PalmDetectionResult> DetectAsync(ImageFrame frame, CancellationToken ct = default)
        => Task.FromResult(Detect(frame));

    public void Dispose() => _net.Dispose();

    private PalmDetectionResult Detect(ImageFrame frame)
    {
        using var bgr = VisionMat.ToBgr(frame);
        int w = bgr.Width, h = bgr.Height;

        // 等比缩放 + 居中补边到 192×192(对齐参考实现:int 截断)
        var ratio = Math.Min((float)InputSize / h, (float)InputSize / w);
        int rw = (int)(w * ratio), rh = (int)(h * ratio);
        using var resized = bgr.Resize(new Size(rw, rh));
        int padW = InputSize - rw, padH = InputSize - rh;
        int left = padW / 2, top = padH / 2;
        using var padded = new Mat();
        Cv2.CopyMakeBorder(resized, padded, top, padH - top, left, padW - left, BorderTypes.Constant, Scalar.Black);
        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);
        using var blob = VisionMat.ToNhwcBlob(rgb);
        int padBiasX = (int)(left / ratio), padBiasY = (int)(top / ratio);

        var outputs = _outNames.Select(_ => new Mat()).ToArray();
        try
        {
            _net.SetInput(blob);
            _net.Forward(outputs, _outNames);

            // 按末维大小识别输出:18 = 框+关键点增量,1 = 分数
            var deltas = outputs.First(m => m.Size(2) == 18);
            var scoreMat = outputs.First(m => m.Size(2) == 1);
            return Decode(deltas, scoreMat, Math.Max(w, h), padBiasX, padBiasY);
        }
        finally
        {
            foreach (var m in outputs)
                m.Dispose();
        }
    }

    private PalmDetectionResult Decode(Mat deltas, Mat scoreMat, float scale, int padBiasX, int padBiasY)
    {
        var count = deltas.Size(1);
        var rects = new List<Rect2d>();
        var scores = new List<float>();
        var candidates = new List<PalmDetection>();

        for (var i = 0; i < count; i++)
        {
            var score = 1f / (1f + MathF.Exp(-scoreMat.At<float>(0, i, 0)));
            if (score < ScoreThreshold)
                continue;

            var anchor = Anchors[i];
            float cx = deltas.At<float>(0, i, 0) / InputSize;
            float cy = deltas.At<float>(0, i, 1) / InputSize;
            float dw = deltas.At<float>(0, i, 2) / InputSize;
            float dh = deltas.At<float>(0, i, 3) / InputSize;

            var x1 = (cx - dw / 2 + anchor[0]) * scale - padBiasX;
            var y1 = (cy - dh / 2 + anchor[1]) * scale - padBiasY;
            var x2 = (cx + dw / 2 + anchor[0]) * scale - padBiasX;
            var y2 = (cy + dh / 2 + anchor[1]) * scale - padBiasY;

            var landmarks = new Point2D[7];
            for (var k = 0; k < 7; k++)
            {
                var lx = (deltas.At<float>(0, i, 4 + k * 2) / InputSize + anchor[0]) * scale - padBiasX;
                var ly = (deltas.At<float>(0, i, 5 + k * 2) / InputSize + anchor[1]) * scale - padBiasY;
                landmarks[k] = new Point2D(lx, ly, score);
            }

            rects.Add(new Rect2d(x1, y1, x2 - x1, y2 - y1));
            scores.Add(score);
            candidates.Add(new PalmDetection(x1, y1, x2, y2, landmarks, score, ComputeRotation(landmarks)));
        }

        if (candidates.Count == 0)
            return PalmDetectionResult.Empty;

        CvDnn.NMSBoxes(rects, scores, ScoreThreshold, NmsThreshold, out var keep);
        var palms = keep.Select(i => candidates[i]).OrderByDescending(p => p.Score).ToArray();
        return new PalmDetectionResult(palms);
    }

    /// <summary>手掌基点(0)→中指根(2)方向 → 使手竖直的旋转角(度),对齐参考实现。</summary>
    private static float ComputeRotation(IReadOnlyList<Point2D> palmLandmarks)
    {
        var p1 = palmLandmarks[0];
        var p2 = palmLandmarks[2];
        var radians = Math.PI / 2 - Math.Atan2(-(p2.Y - p1.Y), p2.X - p1.X);
        radians -= 2 * Math.PI * Math.Floor((radians + Math.PI) / (2 * Math.PI));
        return (float)(radians * 180.0 / Math.PI);
    }

    /// <summary>SSD 锚点:24×24 网格 ×2 + 12×12 网格 ×6 = 2016,中心归一化坐标。</summary>
    private static float[][] BuildAnchors()
    {
        var anchors = new List<float[]>(2016);
        for (var y = 0; y < 24; y++)
            for (var x = 0; x < 24; x++)
                for (var r = 0; r < 2; r++)
                    anchors.Add(new[] { (x + 0.5f) / 24f, (y + 0.5f) / 24f });
        for (var y = 0; y < 12; y++)
            for (var x = 0; x < 12; x++)
                for (var r = 0; r < 6; r++)
                    anchors.Add(new[] { (x + 0.5f) / 12f, (y + 0.5f) / 12f });
        return anchors.ToArray();
    }
}
