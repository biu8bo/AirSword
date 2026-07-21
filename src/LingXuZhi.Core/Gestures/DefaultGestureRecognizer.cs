namespace LingXuZhi.Core.Gestures;

/// <summary>无状态手势判定:21 归一化关键点 → GestureObservation。</summary>
public sealed class DefaultGestureRecognizer : IGestureRecognizer
{
    private readonly Func<double> _pinchThreshold;

    public DefaultGestureRecognizer(Func<double> pinchThreshold)
    {
        _pinchThreshold = pinchThreshold;
    }

    public DefaultGestureRecognizer(double pinchThreshold = 0.05)
        : this(() => pinchThreshold)
    {
    }

    public GestureObservation Recognize(IReadOnlyList<Vec2> landmarks)
    {
        if (landmarks.Count < 21)
            return Idle(default, default);

        var pointer = FingerGeometry.SwordPointer(landmarks);
        var palm = FingerGeometry.PalmCenter(landmarks);
        var pinchLeft = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.IndexTip, FingerGeometry.ThumbTip);
        var pinchRight = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.IndexTip, FingerGeometry.MiddleTip);
        var threshold = (float)_pinchThreshold();

        var indexExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.IndexTip, FingerGeometry.IndexMcp);
        var middleExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.MiddleTip, FingerGeometry.MiddleMcp);
        var ringBent = FingerGeometry.IsBent(landmarks, FingerGeometry.RingTip, FingerGeometry.RingMcp);
        var pinkyBent = FingerGeometry.IsBent(landmarks, FingerGeometry.PinkyTip, FingerGeometry.PinkyMcp);
        var thumbBent = FingerGeometry.IsBent(landmarks, FingerGeometry.ThumbTip, 2); // MCP≈关节2
        var thumbExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.ThumbTip, 2);
        var ringExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.RingTip, FingerGeometry.RingMcp);
        var pinkyExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.PinkyTip, FingerGeometry.PinkyMcp);

        // 优先级: 捏合 > 剑指 > 张开 > 空闲
        if (pinchLeft < threshold && middleExt)
            return new GestureObservation(GestureKind.PinchLeft, pointer, palm, pinchLeft);

        if (pinchRight < threshold && indexExt && middleExt)
            return new GestureObservation(GestureKind.PinchRight, pointer, palm, pinchRight);

        // 剑指: 食中伸直并拢,无名/小指弯曲,拇指收拢
        var tipsClose = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.IndexTip, FingerGeometry.MiddleTip) < 0.35f;
        if (indexExt && middleExt && ringBent && pinkyBent && thumbBent && tipsClose)
            return new GestureObservation(GestureKind.Pointer, pointer, palm, pinchLeft);

        // 五指张开
        if (indexExt && middleExt && ringExt && pinkyExt && thumbExt)
            return new GestureObservation(GestureKind.OpenPalm, pointer, palm, pinchLeft);

        return Idle(pointer, palm);
    }

    private static GestureObservation Idle(Vec2 pointer, Vec2 palm)
        => new(GestureKind.Idle, pointer, palm, float.MaxValue);
}
