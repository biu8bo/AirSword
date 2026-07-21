namespace LingXuZhi.Vision.Abstractions;

/// <summary>
/// 整帧 + 手掌检测 → 21 关键点(已反变换回原图坐标)。
/// 说明:入参取整帧而非裁剪 ROI,因为关键点反变换需要旋转 ROI 的完整上下文(旋转矩阵/padding 偏移)。
/// </summary>
public interface IHandLandmarker : IDisposable
{
    Task<HandLandmarkResult> DetectAsync(ImageFrame frame, PalmDetection palm, CancellationToken ct = default);
}
