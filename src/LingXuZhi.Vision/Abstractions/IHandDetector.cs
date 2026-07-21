namespace LingXuZhi.Vision.Abstractions;

/// <summary>手掌区域(归一化坐标,0-1)。</summary>
public readonly record struct HandRegion(float X, float Y, float Width, float Height, float Score);

/// <summary>输入帧 → 手掌边界框。阶段 2 实现 OpenCV DNN 版本。</summary>
public interface IHandDetector
{
    IReadOnlyList<HandRegion> Detect(ReadOnlyMemory<byte> bgraFrame, int width, int height);
}
