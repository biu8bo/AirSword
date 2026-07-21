namespace LingXuZhi.Vision.OpenCv;

/// <summary>OpenCV DNN 模型路径配置,默认取输出目录下 Models/(随本项目拷贝)。</summary>
public sealed record VisionOptions(string PalmDetectionModelPath, string HandLandmarkModelPath)
{
    public static VisionOptions Default => new(
        Path.Combine(AppContext.BaseDirectory, "Models", "palm_detection_mediapipe_2023feb.onnx"),
        Path.Combine(AppContext.BaseDirectory, "Models", "handpose_estimation_mediapipe_2023feb.onnx"));
}
