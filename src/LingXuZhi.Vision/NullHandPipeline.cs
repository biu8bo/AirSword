using LingXuZhi.Vision.Abstractions;

namespace LingXuZhi.Vision;

/// <summary>阶段 1 占位实现:恒返回空结果,阶段 2 由 OpenCV DNN 实现替换。</summary>
public sealed class NullHandDetector : IHandDetector
{
    public IReadOnlyList<HandRegion> Detect(ReadOnlyMemory<byte> bgraFrame, int width, int height)
        => Array.Empty<HandRegion>();
}

/// <inheritdoc cref="NullHandDetector"/>
public sealed class NullHandLandmarker : IHandLandmarker
{
    public IReadOnlyList<HandLandmark> Landmark(ReadOnlyMemory<byte> bgraRoi, int width, int height)
        => Array.Empty<HandLandmark>();
}
