namespace LingXuZhi.Vision.Abstractions;

/// <summary>整帧 → 手掌检测(边界框 + 7 关键点 + 置信度)。</summary>
public interface IHandDetector : IDisposable
{
    Task<PalmDetectionResult> DetectAsync(ImageFrame frame, CancellationToken ct = default);
}
