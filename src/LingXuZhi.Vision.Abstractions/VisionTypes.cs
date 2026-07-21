namespace LingXuZhi.Vision.Abstractions;

public enum ImagePixelFormat
{
    Bgra32,
    Bgr24,
}

/// <summary>与视觉实现解耦的图像帧(不暴露 OpenCV 类型)。</summary>
public readonly record struct ImageFrame(byte[] Data, int Width, int Height, ImagePixelFormat PixelFormat)
{
    public int Channels => PixelFormat == ImagePixelFormat.Bgra32 ? 4 : 3;
}

/// <summary>二维点,坐标为原图像素坐标。</summary>
public readonly record struct Point2D(float X, float Y, float Visibility);

/// <summary>单个手掌检测结果:边界框 + 7 关键点 + 置信度 + 旋转角(度)。</summary>
public sealed record PalmDetection(
    float X1,
    float Y1,
    float X2,
    float Y2,
    IReadOnlyList<Point2D> Landmarks,
    float Score,
    float RotationDegrees);

public sealed record PalmDetectionResult(IReadOnlyList<PalmDetection> Palms)
{
    public static readonly PalmDetectionResult Empty = new(Array.Empty<PalmDetection>());
}

/// <summary>21 关键点结果,坐标为原图像素坐标。</summary>
public sealed record HandLandmarkResult(bool Detected, IReadOnlyList<Point2D> Landmarks, float Confidence)
{
    public static readonly HandLandmarkResult NotDetected = new(false, Array.Empty<Point2D>(), 0f);
}
