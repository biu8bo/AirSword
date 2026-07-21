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

        var pointer = FingerGeometry.PointerPosition(landmarks);
        var palm = FingerGeometry.PalmCenter(landmarks);
        var pinchLeft = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.ThumbTip, FingerGeometry.IndexTip);
        var pinchRight = FingerGeometry.NormalizedPinchDistance(landmarks, FingerGeometry.ThumbTip, FingerGeometry.MiddleTip);
        var threshold = (float)_pinchThreshold();

        // 优先级: 捏合 > 张开 > 指向 > 空闲。
        // 三指捏合(拇+食+中同时靠拢) → 右键;仅拇食捏合 → 左键;其余手指姿态不参与判定。
        if (pinchLeft < threshold && pinchRight < threshold)
            return new GestureObservation(GestureKind.PinchRight, pointer, palm, pinchLeft, pinchRight);

        if (pinchLeft < threshold)
            return new GestureObservation(GestureKind.PinchLeft, pointer, palm, pinchLeft, pinchRight);

        var indexExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.IndexTip, FingerGeometry.IndexMcp);
        var middleExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.MiddleTip, FingerGeometry.MiddleMcp);
        var ringExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.RingTip, FingerGeometry.RingMcp);
        var pinkyExt = FingerGeometry.IsExtended(landmarks, FingerGeometry.PinkyTip, FingerGeometry.PinkyMcp);
        var thumbExt = FingerGeometry.IsThumbOpen(landmarks);

        // 四指张开时由拇指决定滚轮方向: 拇指张开 → 向上;拇指内收 → 向下
        if (indexExt && middleExt && ringExt && pinkyExt)
        {
            var kind = thumbExt ? GestureKind.OpenPalm : GestureKind.OpenPalmThumbIn;
            return new GestureObservation(kind, pointer, palm, pinchLeft, pinchRight);
        }

        // 指向: 食指单指伸直、中指收起,其余手指不参与判定;指针跟随食指尖
        if (indexExt && !middleExt)
            return new GestureObservation(GestureKind.Pointer, pointer, palm, pinchLeft, pinchRight);

        return new GestureObservation(GestureKind.Idle, pointer, palm, pinchLeft, pinchRight);
    }

    private static GestureObservation Idle(Vec2 pointer, Vec2 palm)
        => new(GestureKind.Idle, pointer, palm, float.MaxValue, float.MaxValue);
}
