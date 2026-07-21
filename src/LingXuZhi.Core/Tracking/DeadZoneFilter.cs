using LingXuZhi.Core.Gestures;

namespace LingXuZhi.Core.Tracking;

/// <summary>以画面中心为锚点的死区:平滑后点距中心 &lt; 半径(归一化)则视为静止。</summary>
public sealed class DeadZoneFilter
{
    private readonly Func<double> _radiusNormalized;

    public DeadZoneFilter(Func<double> radiusNormalized)
    {
        _radiusNormalized = radiusNormalized;
    }

    public DeadZoneFilter(double radiusNormalized = 0.05)
        : this(() => radiusNormalized)
    {
    }

    public bool IsInside(Vec2 normalizedPoint)
    {
        var r = (float)_radiusNormalized();
        var dx = normalizedPoint.X - 0.5f;
        var dy = normalizedPoint.Y - 0.5f;
        return MathF.Sqrt(dx * dx + dy * dy) <= r;
    }
}
