using LingXuZhi.Vision.Abstractions;
using OpenCvSharp;

namespace LingXuZhi.Vision.OpenCv;

/// <summary>旋转 ROI 上下文:模型输入图 + 反变换所需的几何信息。</summary>
internal sealed class RoiContext : IDisposable
{
    public required Mat InputRgb { get; init; }

    /// <summary>旋转空间中的手掌兴趣框 [x1,y1,x2,y2]。</summary>
    public required float[] RotatedPalmBox { get; init; }

    public required float AngleDegrees { get; init; }

    /// <summary>2×3 旋转矩阵(旋转空间 → 裁剪图空间)。</summary>
    public required double[,] RotationMatrix { get; init; }

    public required int PadBiasX { get; init; }

    public required int PadBiasY { get; init; }

    public void Dispose() => InputRgb.Dispose();
}

/// <summary>
/// 用手掌 7 关键点计算旋转矩形 → 仿射变换 → 裁剪出正立手部图。
/// 逻辑对齐 opencv_zoo mp_handpose.py 的 _preprocess/_cropAndPadFromPalm。
/// </summary>
internal static class RoiExtractor
{
    private const int InputSize = 224;
    private const float PreEnlargeFactor = 4f;
    private const float BoxEnlargeFactor = 3f;
    private const float BoxShiftY = -0.4f;

    /// <summary>失败(手掌框完全出画)返回 null。</summary>
    public static RoiContext? Extract(Mat bgr, PalmDetection palm)
    {
        // 1. 以手掌框为中心扩大 4 倍裁剪,补边到对角线边长的方图(保证旋转不裁角)
        var palmBox = new[] { palm.X1, palm.Y1, palm.X2, palm.Y2 };
        var cropped = CropAndPad(bgr, palmBox, forRotation: true, out var box1, out var biasX, out var biasY);
        if (cropped is null)
            return null;

        using var stage = cropped;
        Cv2.CvtColor(stage, stage, ColorConversionCodes.BGR2RGB);

        // 2. 手掌基点→中指根方向计算旋转角,绕框中心旋转使手竖直
        Span<float> lmx = stackalloc float[7];
        Span<float> lmy = stackalloc float[7];
        for (var i = 0; i < 7; i++)
        {
            lmx[i] = palm.Landmarks[i].X - biasX;
            lmy[i] = palm.Landmarks[i].Y - biasY;
        }

        var radians = Math.PI / 2 - Math.Atan2(-(lmy[2] - lmy[0]), lmx[2] - lmx[0]);
        radians -= 2 * Math.PI * Math.Floor((radians + Math.PI) / (2 * Math.PI));
        var angle = (float)(radians * 180.0 / Math.PI);

        var centerX = (box1[0] - biasX + box1[2] - biasX) / 2f;
        var centerY = (box1[1] - biasY + box1[3] - biasY) / 2f;
        using var rotMat = Cv2.GetRotationMatrix2D(new Point2f(centerX, centerY), angle, 1.0);
        var m = new double[2, 3];
        for (var r = 0; r < 2; r++)
            for (var c = 0; c < 3; c++)
                m[r, c] = rotMat.At<double>(r, c);

        using var rotated = new Mat();
        Cv2.WarpAffine(stage, rotated, rotMat, new Size(stage.Cols, stage.Rows));

        // 3. 旋转后的 7 关键点包围盒 → 上移 0.4 + 扩大 3 倍 → 裁剪补方
        Span<float> rx = stackalloc float[7];
        Span<float> ry = stackalloc float[7];
        for (var i = 0; i < 7; i++)
        {
            rx[i] = (float)(lmx[i] * m[0, 0] + lmy[i] * m[0, 1] + m[0, 2]);
            ry[i] = (float)(lmx[i] * m[1, 0] + lmy[i] * m[1, 1] + m[1, 2]);
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (var i = 0; i < 7; i++)
        {
            minX = Math.Min(minX, rx[i]);
            minY = Math.Min(minY, ry[i]);
            maxX = Math.Max(maxX, rx[i]);
            maxY = Math.Max(maxY, ry[i]);
        }

        var rotatedBox = new[] { minX, minY, maxX, maxY };
        var crop = CropAndPad(rotated, rotatedBox, forRotation: false, out var box2, out _, out _);
        if (crop is null)
            return null;

        using var cropOwned = crop;
        var input = new Mat();
        Cv2.Resize(cropOwned, input, new Size(InputSize, InputSize), interpolation: InterpolationFlags.Area);

        return new RoiContext
        {
            InputRgb = input,
            RotatedPalmBox = new[] { (float)box2[0], (float)box2[1], (float)box2[2], (float)box2[3] },
            AngleDegrees = angle,
            RotationMatrix = m,
            PadBiasX = biasX,
            PadBiasY = biasY,
        };
    }

    /// <summary>平移 + 扩大边界框后裁剪,并补边成方图。outBox 为裁剪用的整数框(平移扩大 + 图内裁剪后)。</summary>
    private static Mat? CropAndPad(Mat image, float[] box, bool forRotation, out int[] outBox, out int biasX, out int biasY)
    {
        float w = box[2] - box[0], h = box[3] - box[1];
        var shiftY = forRotation ? 0f : BoxShiftY * h;
        var enlarge = forRotation ? PreEnlargeFactor : BoxEnlargeFactor;

        var cx = (box[0] + box[2]) / 2f;
        var cy = (box[1] + box[3]) / 2f + shiftY;
        float halfW = w * enlarge / 2f, halfH = h * enlarge / 2f;

        var x1 = Math.Clamp((int)(cx - halfW), 0, image.Cols);
        var y1 = Math.Clamp((int)(cy - halfH), 0, image.Rows);
        var x2 = Math.Clamp((int)(cx + halfW), 0, image.Cols);
        var y2 = Math.Clamp((int)(cy + halfH), 0, image.Rows);
        outBox = new[] { x1, y1, x2, y2 };
        biasX = 0;
        biasY = 0;
        if (x2 - x1 <= 0 || y2 - y1 <= 0)
            return null;

        using var cropped = new Mat(image, new Rect(x1, y1, x2 - x1, y2 - y1));
        var sideLen = forRotation
            ? (int)Math.Sqrt((double)cropped.Rows * cropped.Rows + (double)cropped.Cols * cropped.Cols)
            : Math.Max(cropped.Rows, cropped.Cols);
        int padW = sideLen - cropped.Cols, padH = sideLen - cropped.Rows;
        int left = padW / 2, top = padH / 2;

        var padded = new Mat();
        Cv2.CopyMakeBorder(cropped, padded, top, padH - top, left, padW - left, BorderTypes.Constant, Scalar.Black);
        biasX = x1 - left;
        biasY = y1 - top;
        return padded;
    }
}
