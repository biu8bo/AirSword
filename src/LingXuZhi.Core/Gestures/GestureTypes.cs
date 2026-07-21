namespace LingXuZhi.Core.Gestures;

/// <summary>Core 层二维点(归一化或像素坐标,由调用方约定)。</summary>
public readonly record struct Vec2(float X, float Y)
{
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
    Pointer,          // 食指伸直指向 → 移动
    PinchLeft,        // 拇指-食指捏合 → 左键单击 / 拖拽
    PinchRight,       // 食指+中指伸直后指尖并拢 → 右键单击
    OpenPalm,         // 五指张开(拇指张开)→ 滚轮向上
    OpenPalmThumbIn,  // 四指张开、拇指内收 → 滚轮向下
}

/// <summary>瞬时手势判定结果(无状态机)。捏合距离为相对手掌宽度的归一化值,用于调试与阈值校准。</summary>
public sealed record GestureObservation(
    GestureKind Kind,
    Vec2 PointerNormalized,
    Vec2 PalmCenterNormalized,
    float PinchLeftDistance,
    float PinchRightDistance);

public enum MachineState
{
    Idle,
    Moving,
    PinchPending,   // 已捏合,等待判定:原地松开=单击,移动=拖拽
    Dragging,       // 捏合中移动,左键按住
    RightClick,
    Scroll,
}

/// <summary>状态机输出的动作意图。</summary>
public enum MouseActionKind
{
    None,
    Move,
    LeftDown,
    LeftUp,
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
    Vec2 SmoothedNormalized,
    float PinchLeftDistance = float.MaxValue,
    float PinchRightDistance = float.MaxValue);
