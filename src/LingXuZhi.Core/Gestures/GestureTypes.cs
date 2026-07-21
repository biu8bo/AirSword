namespace LingXuZhi.Core.Gestures;

/// <summary>Core 层二维点(归一化或像素坐标,由调用方约定)。</summary>
public readonly record struct Vec2(float X, float Y)
{
    public static Vec2 Midpoint(Vec2 a, Vec2 b) => new((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f);

    public float DistanceTo(Vec2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

public enum GestureKind
{
    Idle,
    Pointer,      // 剑指移动
    PinchLeft,    // 食指-拇指捏合 → 左键
    PinchRight,   // 食指-中指捏合 → 右键
    OpenPalm,     // 手掌张开 → 滚轮模式
}

/// <summary>瞬时手势判定结果(无状态机)。</summary>
public sealed record GestureObservation(
    GestureKind Kind,
    Vec2 PointerNormalized,
    Vec2 PalmCenterNormalized,
    float PinchDistance);

public enum MachineState
{
    Idle,
    Moving,
    LeftClick,
    RightClick,
    Scroll,
}

/// <summary>状态机输出的动作意图。</summary>
public enum MouseActionKind
{
    None,
    Move,
    LeftClick,
    RightClick,
    Scroll,
}

public sealed record MouseAction(
    MouseActionKind Kind,
    float ScreenX,
    float ScreenY,
    int ScrollDelta,
    bool InDeadZone,
    MachineState State,
    GestureKind ObservedGesture,
    Vec2 RawNormalized,
    Vec2 SmoothedNormalized);
