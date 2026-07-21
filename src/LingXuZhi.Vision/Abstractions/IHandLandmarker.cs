namespace LingXuZhi.Vision.Abstractions;

/// <summary>单个手部关键点(归一化坐标)。</summary>
public readonly record struct HandLandmark(float X, float Y, float Z);

/// <summary>ROI 图像 → 21 个手部关键点。阶段 2 实现 OpenCV DNN 版本。</summary>
public interface IHandLandmarker
{
    IReadOnlyList<HandLandmark> Landmark(ReadOnlyMemory<byte> bgraRoi, int width, int height);
}
