using LingXuZhi.Vision.Abstractions;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace LingXuZhi.Vision.OpenCv;

/// <summary>
/// MediaPipe 21 关键点检测(OpenCV Zoo handpose_estimation_mediapipe_2023feb)。
/// 输入 1×224×224×3 NHWC RGB /255;输出 21×3 关键点(输入空间)+ 置信度 + 惯用手 + 世界坐标。
/// 后处理反变换对齐 opencv_zoo/models/handpose_estimation_mediapipe/mp_handpose.py。
/// </summary>
public sealed class OpenCvHandLandmarker : IHandLandmarker
{
    private const int InputSize = 224;

    private readonly Net _net;
    private readonly string[] _outNames;

    /// <summary>关键点模型自身置信度阈值,与手掌检测阈值语义不同,固定用参考实现默认值。</summary>
    private const float ConfidenceThreshold = 0.8f;

    public OpenCvHandLandmarker(VisionOptions options)
    {
        _net = CvDnn.ReadNet(options.HandLandmarkModelPath)
            ?? throw new InvalidOperationException($"无法加载关键点模型:{options.HandLandmarkModelPath}");
        _outNames = _net.GetUnconnectedOutLayersNames()!;
    }

    /// <summary>推理在调用线程同步执行(管线本身运行于后台线程)。</summary>
    public Task<HandLandmarkResult> DetectAsync(ImageFrame frame, PalmDetection palm, CancellationToken ct = default)
        => Task.FromResult(Detect(frame, palm));

    public void Dispose() => _net.Dispose();

    private HandLandmarkResult Detect(ImageFrame frame, PalmDetection palm)
    {
        using var bgr = VisionMat.ToBgr(frame);
        using var roi = RoiExtractor.Extract(bgr, palm);
        if (roi is null)
            return HandLandmarkResult.NotDetected;

        using var blob = VisionMat.ToNhwcBlob(roi.InputRgb);

        // 输出顺序与参考实现一致:landmarks(1×63)、conf(1×1)、handedness(1×1)、world landmarks(1×63)
        var outputs = _outNames.Select(_ => new Mat()).ToArray();
        try
        {
            _net.SetInput(blob);
            _net.Forward(outputs, _outNames);

            var conf = outputs[1].At<float>(0, 0);
            if (conf < ConfidenceThreshold)
                return HandLandmarkResult.NotDetected;

            return new HandLandmarkResult(true, TransformBack(outputs[0], roi, conf), conf);
        }
        finally
        {
            foreach (var m in outputs)
                m.Dispose();
        }
    }

    /// <summary>模型输入空间 21 点 → 去中心缩放 → 反旋转 → 平移回原图坐标。</summary>
    private static Point2D[] TransformBack(Mat landmarkBlob, RoiContext roi, float conf)
    {
        float boxW = roi.RotatedPalmBox[2] - roi.RotatedPalmBox[0];
        float boxH = roi.RotatedPalmBox[3] - roi.RotatedPalmBox[1];
        var scale = Math.Max(boxW / InputSize, boxH / InputSize);

        // 逆时针补偿旋转(以原点为中心)
        using var coordRot = Cv2.GetRotationMatrix2D(new Point2f(0, 0), roi.AngleDegrees, 1.0);
        double r00 = coordRot.At<double>(0, 0), r01 = coordRot.At<double>(0, 1);
        double r10 = coordRot.At<double>(1, 0), r11 = coordRot.At<double>(1, 1);

        // 图像旋转矩阵求逆,把旋转空间的框中心变换回裁剪图空间
        var m = roi.RotationMatrix;
        double it0 = -(m[0, 0] * m[0, 2] + m[1, 0] * m[1, 2]);
        double it1 = -(m[0, 1] * m[0, 2] + m[1, 1] * m[1, 2]);
        double centerRx = (roi.RotatedPalmBox[0] + roi.RotatedPalmBox[2]) / 2.0;
        double centerRy = (roi.RotatedPalmBox[1] + roi.RotatedPalmBox[3]) / 2.0;
        var originalCenterX = centerRx * m[0, 0] + centerRy * m[1, 0] + it0;
        var originalCenterY = centerRx * m[0, 1] + centerRy * m[1, 1] + it1;

        var points = new Point2D[21];
        for (var i = 0; i < 21; i++)
        {
            var x = (landmarkBlob.At<float>(0, i * 3) - InputSize / 2f) * scale;
            var y = (landmarkBlob.At<float>(0, i * 3 + 1) - InputSize / 2f) * scale;

            var rx = x * r00 + y * r10;
            var ry = x * r01 + y * r11;

            points[i] = new Point2D(
                (float)(rx + originalCenterX + roi.PadBiasX),
                (float)(ry + originalCenterY + roi.PadBiasY),
                conf);
        }
        return points;
    }
}
