namespace LingXuZhi.Core.Gestures;

/// <summary>无状态手势判定:21 归一化关键点 → GestureObservation。</summary>
public sealed class DefaultGestureRecognizer : IGestureRecognizer
{
    private readonly Func<double> _pinchThreshold;

    public DefaultGestureRecognizer(Func<double> pinchThreshold)
    {
        _pinchThreshold = pinchThreshold;
    }

    public DefaultGestureRecognizer(double pinchThreshold = 0.18)
        : this(() => pinchThreshold)
    {
    }

    public GestureObservation Recognize(IReadOnlyList<Vec2> landmarks)
    {
        if (landmarks.Count < 21)
            return Idle(default, default);

        var pointer = FingerGeometry.PointerPosition(landmarks);
        var palm = FingerGeometry.PalmCenter(landmarks);
        var pinchLeft = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.ThumbTip, FingerGeometry.IndexTip);
        // PinchRightDistance 复用为食中指尖距,供右键(比耶并拢)调试显示
        var tipClose = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.IndexTip, FingerGeometry.MiddleTip);
        var threshold = (float)_pinchThreshold();

        var indexExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.IndexTip, FingerGeometry.IndexMcp);
        var middleExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.MiddleTip, FingerGeometry.MiddleMcp);
        var ringExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.RingTip, FingerGeometry.RingMcp);
        var pinkyExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.PinkyTip, FingerGeometry.PinkyMcp);
        var thumbExt = FingerGeometry.IsThumbOpen(landmarks);
        var fourOpen = indexExt && middleExt && ringExt && pinkyExt;

        // 优先级: 左键捏合 > 右键并拢 > 张开滚轮 > 指向 > 空闲。
        // 左键:拇食距离低于阈值(其他手指不参与)。放在并拢之前,避免弯中指时误触右键。
        if (pinchLeft < threshold)
            return new GestureObservation(GestureKind.PinchLeft, pointer, palm, pinchLeft, tipClose);

        // 右键:食指+中指伸直(比耶)后指尖并拢;四指全开时归滚轮,不抢右键
        if (indexExt && middleExt && tipClose < threshold && !fourOpen)
            return new GestureObservation(GestureKind.PinchRight, pointer, palm, pinchLeft, tipClose);

        // 四指张开时由拇指决定滚轮方向
        if (fourOpen)
        {
            var kind = thumbExt ? GestureKind.OpenPalm : GestureKind.OpenPalmThumbIn;
            return new GestureObservation(kind, pointer, palm, pinchLeft, tipClose);
        }

        // 指向:食指单指伸直、中指收起
        if (indexExt && !middleExt)
            return new GestureObservation(GestureKind.Pointer, pointer, palm, pinchLeft, tipClose);

        return new GestureObservation(GestureKind.Idle, pointer, palm, pinchLeft, tipClose);
    }

    private static GestureObservation Idle(Vec2 pointer, Vec2 palm)
        => new(GestureKind.Idle, pointer, palm, float.MaxValue, float.MaxValue);
}
