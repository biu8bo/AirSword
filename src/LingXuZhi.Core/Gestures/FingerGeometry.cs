namespace LingXuZhi.Core.Gestures;

/// <summary>手指伸直/弯曲与捏合距离的纯函数工具。</summary>
public static class FingerGeometry
{
    // MediaPipe 拓扑: 拇指 1-2-3-4, 食指 5-6-7-8, 中指 9-10-11-12, 无名 13-14-15-16, 小指 17-18-19-20
    public const int Wrist = 0;
    public const int ThumbTip = 4;
    public const int IndexMcp = 5;
    public const int IndexTip = 8;
    public const int MiddleMcp = 9;
    public const int MiddleTip = 12;
    public const int RingMcp = 13;
    public const int RingTip = 16;
    public const int PinkyMcp = 17;
    public const int PinkyTip = 20;

    /// <summary>
    /// 伸直判定: 指尖到腕距离 > 指根 MCP 到腕距离 × ratio。
    /// </summary>
    public static bool IsExtended(IReadOnlyList<Vec2> pts, int tip, int mcp, float ratio = 1.5f)
    {
        if (pts.Count < 21)
            return false;
        var wrist = pts[Wrist];
        var tipLen = pts[tip].DistanceTo(wrist);
        var mcpLen = pts[mcp].DistanceTo(wrist);
        if (mcpLen < 1e-4f)
            return false;
        return tipLen / mcpLen > ratio;
    }

    public static bool IsBent(IReadOnlyList<Vec2> pts, int tip, int mcp, float ratio = 1.5f)
        => !IsExtended(pts, tip, mcp, ratio);

    /// <summary>捏合距离按手掌宽度(腕→中指 MCP)归一化。</summary>
    public static float NormalizedPinchDistance(IReadOnlyList<Vec2> pts, int a, int b)
    {
        if (pts.Count < 21)
            return float.MaxValue;
        var palm = pts[Wrist].DistanceTo(pts[MiddleMcp]);
        if (palm < 1e-4f)
            return float.MaxValue;
        return pts[a].DistanceTo(pts[b]) / palm;
    }

    public static Vec2 SwordPointer(IReadOnlyList<Vec2> pts)
        => Vec2.Midpoint(pts[IndexTip], pts[MiddleTip]);

    public static Vec2 PalmCenter(IReadOnlyList<Vec2> pts)
        => pts[MiddleMcp];
}
