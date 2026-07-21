using LingXuZhi.Core.Gestures;

namespace LingXuZhi.Core.Tracking;

/// <summary>归一化摄像头坐标 → 屏幕像素。镜像时水平翻转。</summary>
public sealed class CoordinateMapper
{
    private readonly Func<bool> _mirror;
    private readonly Func<double> _sensitivity;
    private readonly Func<(int Width, int Height)> _screenSize;

    public CoordinateMapper(
        Func<(int Width, int Height)> screenSize,
        Func<bool> mirror,
        Func<double> sensitivity)
    {
        _screenSize = screenSize;
        _mirror = mirror;
        _sensitivity = sensitivity;
    }

    /// <summary>
    /// 线性映射。灵敏度以画面中心为锚:中心不变,偏离中心的位移 × sensitivity。
    /// </summary>
    public (float X, float Y) ToScreen(Vec2 normalized)
    {
        var (sw, sh) = _screenSize();
        var nx = _mirror() ? 1f - normalized.X : normalized.X;
        var ny = normalized.Y;

        var sens = (float)Math.Clamp(_sensitivity(), 0.1, 5.0);
        // 以 0.5 为中心放大偏移
        nx = 0.5f + (nx - 0.5f) * sens;
        ny = 0.5f + (ny - 0.5f) * sens;
        nx = Math.Clamp(nx, 0f, 1f);
        ny = Math.Clamp(ny, 0f, 1f);

        return (nx * (sw - 1), ny * (sh - 1));
    }
}
