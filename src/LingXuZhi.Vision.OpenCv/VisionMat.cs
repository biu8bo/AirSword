using System.Runtime.InteropServices;
using LingXuZhi.Vision.Abstractions;
using OpenCvSharp;

namespace LingXuZhi.Vision.OpenCv;

/// <summary>ImageFrame ↔ Mat 转换与 NHWC blob 构造(模型输入为 NHWC,不能用 BlobFromImage 的 NCHW)。</summary>
internal static class VisionMat
{
    /// <summary>ImageFrame → BGR Mat(拷贝,调用方负责 Dispose)。</summary>
    public static Mat ToBgr(ImageFrame frame)
    {
        var type = frame.PixelFormat == ImagePixelFormat.Bgra32 ? MatType.CV_8UC4 : MatType.CV_8UC3;
        using var wrapper = Mat.FromPixelData(frame.Height, frame.Width, type, frame.Data);
        if (frame.PixelFormat == ImagePixelFormat.Bgr24)
            return wrapper.Clone();

        var bgr = new Mat();
        Cv2.CvtColor(wrapper, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    /// <summary>8UC3 RGB 方图 → 1×H×W×3 CV_32F blob,/255 归一化(调用方负责 Dispose)。</summary>
    public static Mat ToNhwcBlob(Mat rgb8u)
    {
        using var f32 = new Mat();
        rgb8u.ConvertTo(f32, MatType.CV_32FC3, 1.0 / 255.0);

        var blob = new Mat(new[] { 1, rgb8u.Rows, rgb8u.Cols, 3 }, MatType.CV_32F);
        var count = rgb8u.Rows * rgb8u.Cols * 3;
        var buffer = new float[count];
        Marshal.Copy(f32.Data, buffer, 0, count);
        Marshal.Copy(buffer, 0, blob.Data, count);
        return blob;
    }
}
