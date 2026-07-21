using LingXuZhi.Core.Gestures;

namespace LingXuZhi.Core.Tracking;

public interface ISmoother
{
    Vec2 Filter(Vec2 raw);
    void Reset();
}

/// <summary>指数移动平均: out = α * raw + (1-α) * prev。α 越大越跟手、越小越平滑。</summary>
public sealed class EmaSmoother : ISmoother
{
    private readonly Func<double> _alpha;
    private Vec2 _prev;
    private bool _initialized;

    public EmaSmoother(Func<double> alpha)
    {
        _alpha = alpha;
    }

    public EmaSmoother(double alpha = 0.3)
        : this(() => alpha)
    {
    }

    public Vec2 Filter(Vec2 raw)
    {
        var a = (float)Math.Clamp(_alpha(), 0.01, 1.0);
        if (!_initialized)
        {
            _prev = raw;
            _initialized = true;
            return raw;
        }

        _prev = new Vec2(
            a * raw.X + (1 - a) * _prev.X,
            a * raw.Y + (1 - a) * _prev.Y);
        return _prev;
    }

    public void Reset()
    {
        _initialized = false;
        _prev = default;
    }
}

/// <summary>卡尔曼平滑预留占位,本期不实现。</summary>
public interface IKalmanSmoother : ISmoother
{
}
