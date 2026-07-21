using LingXuZhi.Core.Gestures;
using LingXuZhi.Core.Pipeline;
using LingXuZhi.Core.Tracking;
using Xunit;

namespace LingXuZhi.Core.Tests;

public class FingerGeometryTests
{
    [Fact]
    public void PointerPosition_IsIndexTip()
    {
        var pts = FlatHand();
        pts[8] = new Vec2(0.4f, 0.2f);
        var p = FingerGeometry.PointerPosition(pts);
        Assert.Equal(0.4f, p.X, 3);
        Assert.Equal(0.2f, p.Y, 3);
    }

    [Fact]
    public void IsExtended_WhenTipFartherThanMcp()
    {
        var pts = FlatHand();
        Assert.True(FingerGeometry.IsExtended(pts, 8, 5));
        Assert.True(FingerGeometry.IsBent(pts, 16, 13)); // ring tip near mcp
    }

    /// <summary>构造简化手型:食中伸直,无名小指弯曲,拇指收拢(基础手型)。</summary>
    internal static Vec2[] FlatHand()
    {
        var pts = new Vec2[21];
        pts[0] = new Vec2(0.5f, 0.8f); // wrist
        pts[2] = new Vec2(0.45f, 0.7f);
        pts[4] = new Vec2(0.42f, 0.68f); // thumb tip bent
        pts[5] = new Vec2(0.48f, 0.55f);
        pts[8] = new Vec2(0.48f, 0.25f); // index tip extended
        pts[9] = new Vec2(0.52f, 0.55f);
        pts[12] = new Vec2(0.52f, 0.25f); // middle tip extended
        pts[13] = new Vec2(0.56f, 0.58f);
        pts[16] = new Vec2(0.56f, 0.55f); // ring bent
        pts[17] = new Vec2(0.60f, 0.60f);
        pts[20] = new Vec2(0.60f, 0.58f); // pinky bent
        return pts;
    }

    /// <summary>五指全部张开(拇指也张开)。</summary>
    internal static Vec2[] OpenPalm()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.30f, 0.35f);
        pts[2] = new Vec2(0.38f, 0.50f);
        pts[16] = new Vec2(0.62f, 0.25f);
        pts[20] = new Vec2(0.70f, 0.28f);
        return pts;
    }

    /// <summary>四指张开、拇指弯向掌心(指尖横穿手掌,落在无名/小指根附近)。</summary>
    internal static Vec2[] OpenPalmThumbIn()
    {
        var pts = OpenPalm();
        pts[2] = new Vec2(0.45f, 0.70f);
        pts[4] = new Vec2(0.56f, 0.58f); // thumb tip crossing palm, near pinky mcp
        return pts;
    }

    /// <summary>指向手型:仅食指伸直,中指收起。</summary>
    internal static Vec2[] IndexPointerHand()
    {
        var pts = FlatHand();
        pts[12] = new Vec2(0.53f, 0.53f); // middle bent
        return pts;
    }

    /// <summary>比耶手势:食中伸直但指尖分开,拇指张开(不再是移动手势)。</summary>
    internal static Vec2[] VictorySign()
    {
        var pts = FlatHand();
        pts[8] = new Vec2(0.40f, 0.25f);  // 两指尖明显分开
        pts[4] = new Vec2(0.30f, 0.35f);  // thumb extended
        pts[2] = new Vec2(0.38f, 0.50f);
        return pts;
    }

    /// <summary>拇指-食指捏合,中指伸直。</summary>
    internal static Vec2[] PinchLeft()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.490f, 0.250f);
        pts[8] = new Vec2(0.492f, 0.250f); // almost overlapping → pinch
        pts[12] = new Vec2(0.55f, 0.22f);
        return pts;
    }

    /// <summary>拇指-食指捏合,其余手指全部弯曲(不影响判定)。</summary>
    internal static Vec2[] PinchLeftOtherFingersBent()
    {
        var pts = PinchLeft();
        pts[12] = new Vec2(0.53f, 0.53f); // middle bent
        return pts;
    }

    /// <summary>拇指+食指+中指三指捏合。</summary>
    internal static Vec2[] PinchRight()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.490f, 0.250f);
        pts[8] = new Vec2(0.492f, 0.250f);
        pts[12] = new Vec2(0.494f, 0.250f); // three tips together
        return pts;
    }

    /// <summary>仅拇指-中指捏合,食指伸直(不再是右键手势)。</summary>
    internal static Vec2[] ThumbMiddleOnlyPinch()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.520f, 0.250f);
        pts[12] = new Vec2(0.522f, 0.250f); // thumb-middle overlapping
        pts[8] = new Vec2(0.44f, 0.30f);    // index far away
        return pts;
    }
}

public class GestureRecognizerTests
{
    private readonly DefaultGestureRecognizer _sut = new(0.05);

    [Fact]
    public void Recognize_IndexPointer()
    {
        var obs = _sut.Recognize(FingerGeometryTests.IndexPointerHand());
        Assert.Equal(GestureKind.Pointer, obs.Kind);
    }

    [Fact]
    public void Recognize_TwoFingersExtended_IsNotPointer()
    {
        // 中指也伸直(比耶)不再是移动手势
        var obs = _sut.Recognize(FingerGeometryTests.VictorySign());
        Assert.Equal(GestureKind.Idle, obs.Kind);
    }

    [Fact]
    public void Recognize_OpenPalm_AsScrollUp()
    {
        var obs = _sut.Recognize(FingerGeometryTests.OpenPalm());
        Assert.Equal(GestureKind.OpenPalm, obs.Kind);
    }

    [Fact]
    public void Recognize_OpenPalmThumbIn_AsScrollDown()
    {
        var obs = _sut.Recognize(FingerGeometryTests.OpenPalmThumbIn());
        Assert.Equal(GestureKind.OpenPalmThumbIn, obs.Kind);
    }

    [Fact]
    public void Recognize_ThumbAcrossPalm_IsNotOpenPalm()
    {
        // 回归:拇指折向掌心时指尖横穿手掌,离腕仍远,
        // 旧的「指尖到腕距离」判定会把它误判为拇指张开(五指)
        var pts = FingerGeometryTests.OpenPalm();
        pts[2] = new Vec2(0.46f, 0.72f);
        pts[4] = new Vec2(0.58f, 0.60f); // 指尖贴着小指根,但到腕距离是关节2的 2.4 倍
        var obs = _sut.Recognize(pts);
        Assert.Equal(GestureKind.OpenPalmThumbIn, obs.Kind);
    }

    [Fact]
    public void Recognize_PinchLeft()
    {
        var obs = _sut.Recognize(FingerGeometryTests.PinchLeft());
        Assert.Equal(GestureKind.PinchLeft, obs.Kind);
    }

    [Fact]
    public void Recognize_PinchLeft_IgnoresOtherFingers()
    {
        var obs = _sut.Recognize(FingerGeometryTests.PinchLeftOtherFingersBent());
        Assert.Equal(GestureKind.PinchLeft, obs.Kind);
    }

    [Fact]
    public void Recognize_PinchRight_ThreeFingers()
    {
        var obs = _sut.Recognize(FingerGeometryTests.PinchRight());
        Assert.Equal(GestureKind.PinchRight, obs.Kind);
    }

    [Fact]
    public void Recognize_ThumbMiddleOnly_IsNotRightClick()
    {
        // 右键需三指捏合,仅拇中捏合不触发
        var obs = _sut.Recognize(FingerGeometryTests.ThumbMiddleOnlyPinch());
        Assert.NotEqual(GestureKind.PinchRight, obs.Kind);
        Assert.NotEqual(GestureKind.PinchLeft, obs.Kind);
    }

    [Fact]
    public void Recognize_IdleWhenEmpty()
    {
        var obs = _sut.Recognize(Array.Empty<Vec2>());
        Assert.Equal(GestureKind.Idle, obs.Kind);
    }
}

public class GestureStateMachineTests
{
    private long _now;

    private GestureStateMachine Create(int debounce = 1, double dragThreshold = 0.03, int windowMs = 350)
        => new(() => debounce, () => dragThreshold, () => windowMs, () => _now);

    private static GestureObservation Obs(GestureKind kind, float x = 0.5f, float y = 0.5f)
        => new(kind, new Vec2(x, y), new Vec2(0.5f, 0.6f), 0.02f, 0.5f);

    [Fact]
    public void RequiresDebounceFrames_BeforeMoving()
    {
        var sm = Create(debounce: 3);
        var pointer = Obs(GestureKind.Pointer);

        var r1 = sm.Update(pointer, pointer.PointerNormalized);
        Assert.Equal(MachineState.Idle, r1.State);

        sm.Update(pointer, pointer.PointerNormalized);
        var r3 = sm.Update(pointer, pointer.PointerNormalized);
        Assert.Equal(MachineState.Moving, r3.State);
        Assert.Equal(MouseActionKind.Move, r3.Action);
    }

    [Fact]
    public void QuickPinchRelease_EmitsSingleClick_AfterWindow()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        var pinch = sm.Update(Obs(GestureKind.PinchLeft), pos);
        Assert.Equal(MachineState.PinchPending, pinch.State);
        Assert.Equal(MouseActionKind.None, pinch.Action);

        var release = sm.Update(Obs(GestureKind.Pointer), pos);
        Assert.Equal(MachineState.Moving, release.State);
        Assert.Equal(MouseActionKind.None, release.Action); // 双击窗口内挂起

        _now += 400; // 窗口过期
        var fired = sm.Update(Obs(GestureKind.Pointer), pos);
        Assert.Equal(MouseActionKind.LeftClick, fired.Action);

        var after = sm.Update(Obs(GestureKind.Pointer), pos);
        Assert.Equal(MouseActionKind.Move, after.Action);
    }

    [Fact]
    public void TwoQuickPinches_EmitDoubleClick()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        sm.Update(Obs(GestureKind.PinchLeft), pos);
        sm.Update(Obs(GestureKind.Pointer), pos); // 第一次松开

        _now += 100; // 窗口内二次捏合
        sm.Update(Obs(GestureKind.PinchLeft), pos);
        var release = sm.Update(Obs(GestureKind.Pointer), pos);
        Assert.Equal(MouseActionKind.DoubleClick, release.Action);

        _now += 500;
        var idle = sm.Update(Obs(GestureKind.Pointer), pos);
        Assert.Equal(MouseActionKind.Move, idle.Action); // 无残留单击
    }

    [Fact]
    public void PinchAndMove_StartsDrag_ThenReleases()
    {
        var sm = Create();

        sm.Update(Obs(GestureKind.PinchLeft), new Vec2(0.5f, 0.5f));

        var down = sm.Update(Obs(GestureKind.PinchLeft), new Vec2(0.6f, 0.5f));
        Assert.Equal(MachineState.Dragging, down.State);
        Assert.Equal(MouseActionKind.LeftDown, down.Action);

        var move = sm.Update(Obs(GestureKind.PinchLeft), new Vec2(0.62f, 0.5f));
        Assert.Equal(MouseActionKind.Move, move.Action);

        var up = sm.Update(Obs(GestureKind.Pointer), new Vec2(0.62f, 0.5f));
        Assert.Equal(MouseActionKind.LeftUp, up.Action);
        Assert.Equal(MachineState.Moving, up.State);
    }

    [Fact]
    public void Reset_WhileDragging_FlushesLeftUp()
    {
        var sm = Create();
        sm.Update(Obs(GestureKind.PinchLeft), new Vec2(0.5f, 0.5f));
        sm.Update(Obs(GestureKind.PinchLeft), new Vec2(0.6f, 0.5f)); // dragging

        Assert.Equal(MouseActionKind.LeftUp, sm.Reset());
        Assert.Equal(MachineState.Idle, sm.State);
    }

    [Fact]
    public void RightClick_FiresOnceUntilRelease()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        var click = sm.Update(Obs(GestureKind.PinchRight), pos);
        Assert.Equal(MachineState.RightClick, click.State);
        Assert.Equal(MouseActionKind.RightClick, click.Action);

        var again = sm.Update(Obs(GestureKind.PinchRight), pos);
        Assert.Equal(MouseActionKind.None, again.Action);
    }

    [Fact]
    public void PinchLeftThenThreeFingers_BecomesRightClick_WithoutLeftClick()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        // 三指捏合成形过程:先识别到拇食捏合,再变为三指捏合
        sm.Update(Obs(GestureKind.PinchLeft), pos);
        var right = sm.Update(Obs(GestureKind.PinchRight), pos);
        Assert.Equal(MachineState.RightClick, right.State);
        Assert.Equal(MouseActionKind.RightClick, right.Action);

        // 松开并等待双击窗口过期,不应补发左键单击
        sm.Update(Obs(GestureKind.Idle), pos);
        _now += 500;
        var after = sm.Update(Obs(GestureKind.Idle), pos);
        Assert.Equal(MouseActionKind.None, after.Action);
    }

    [Fact]
    public void OpenPalm_ScrollsUp_WithRepeatInterval()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        var first = sm.Update(Obs(GestureKind.OpenPalm), pos);
        Assert.Equal(MachineState.Scroll, first.State);
        Assert.Equal(MouseActionKind.Scroll, first.Action);
        Assert.Equal(1, first.ScrollDelta);

        var tooSoon = sm.Update(Obs(GestureKind.OpenPalm), pos);
        Assert.Equal(MouseActionKind.None, tooSoon.Action);

        _now += 200;
        var repeat = sm.Update(Obs(GestureKind.OpenPalm), pos);
        Assert.Equal(MouseActionKind.Scroll, repeat.Action);
        Assert.Equal(1, repeat.ScrollDelta);
    }

    [Fact]
    public void OpenPalmThumbIn_ScrollsDown()
    {
        var sm = Create();
        var pos = new Vec2(0.5f, 0.5f);

        var first = sm.Update(Obs(GestureKind.OpenPalmThumbIn), pos);
        Assert.Equal(MachineState.Scroll, first.State);
        Assert.Equal(MouseActionKind.Scroll, first.Action);
        Assert.Equal(-1, first.ScrollDelta);
    }
}

public class GesturePipelineStabilityTests
{
    private static GesturePipeline CreatePipeline()
        => new(
            new DefaultGestureRecognizer(0.05),
            new GestureStateMachine(1),
            new EmaSmoother(1.0), // α=1 直通,便于断言位置
            new DeadZoneFilter(0.0),
            new CoordinateMapper(() => (1000, 1000), () => false, () => 1.0),
            () => 3,
            () => 0.05);

    /// <summary>整手平移 dx,保持食指指向手型。</summary>
    private static Vec2[] HandAt(float dx)
    {
        var pts = FingerGeometryTests.IndexPointerHand();
        for (var i = 0; i < pts.Length; i++)
            pts[i] = new Vec2(pts[i].X + dx, pts[i].Y);
        return pts;
    }

    [Fact]
    public void SingleFrameJump_IsSuppressed()
    {
        var pipeline = CreatePipeline();
        var baseline = pipeline.Process(HandAt(0));
        Assert.Equal(MachineState.Moving, baseline.State);
        var basePos = baseline.SmoothedNormalized;

        // 单帧坏点:整手瞬移 0.3,下一帧回到原位 → 指针不应跟着闪跳
        var glitch = pipeline.Process(HandAt(0.3f));
        Assert.Equal(basePos.X, glitch.SmoothedNormalized.X, 3);

        var back = pipeline.Process(HandAt(0));
        Assert.Equal(basePos.X, back.SmoothedNormalized.X, 3);
    }

    [Fact]
    public void PersistentJump_IsAcceptedAfterConfirmFrames()
    {
        var pipeline = CreatePipeline();
        pipeline.Process(HandAt(0));

        // 连续 3 帧维持在新位置 → 视为真实大位移,指针跟上
        pipeline.Process(HandAt(0.3f));
        pipeline.Process(HandAt(0.3f));
        var third = pipeline.Process(HandAt(0.3f));
        var expected = FingerGeometry.PointerPosition(HandAt(0.3f));
        Assert.Equal(expected.X, third.SmoothedNormalized.X, 3);
    }

    [Fact]
    public void PinchLock_AnchorsPointer_AgainstFingerBendDrift()
    {
        var pipeline = CreatePipeline();

        // 数帧食指指向,填充指针历史
        for (var i = 0; i < 8; i++)
            pipeline.Process(FingerGeometryTests.IndexPointerHand());
        var aim = FingerGeometry.PointerPosition(FingerGeometryTests.IndexPointerHand());

        // 捏合:食指尖大幅弯向拇指(掉到手掌处),但手整体未动(食指根不变)
        var pinched = DriftedPinchHand();
        var r = pipeline.Process(pinched);
        Assert.Equal(MachineState.PinchPending, r.State);
        // 指针应锚定在弯指前的瞄准位置,而不是跟着指尖掉下去
        Assert.Equal(aim.X, r.SmoothedNormalized.X, 2);
        Assert.Equal(aim.Y, r.SmoothedNormalized.Y, 2);
    }

    [Fact]
    public void PinchLock_FollowsWholeHand_ForDrag()
    {
        var pipeline = CreatePipeline();
        for (var i = 0; i < 8; i++)
            pipeline.Process(FingerGeometryTests.IndexPointerHand());
        var aim = FingerGeometry.PointerPosition(FingerGeometryTests.IndexPointerHand());

        pipeline.Process(DriftedPinchHand()); // 锁定 + PinchPending

        // 捏住整手平移 0.1:指针随手移动(按食指根位移),进入拖拽
        var moved = DriftedPinchHand();
        for (var i = 0; i < moved.Length; i++)
            moved[i] = new Vec2(moved[i].X + 0.1f, moved[i].Y);
        var drag = pipeline.Process(moved);
        Assert.Equal(MachineState.Dragging, drag.State);
        Assert.Equal(MouseActionKind.LeftDown, drag.Kind);
        Assert.Equal(aim.X + 0.1f, drag.SmoothedNormalized.X, 2);
    }

    /// <summary>食指尖弯到拇指处捏合,食指根(5)保持不动。</summary>
    private static Vec2[] DriftedPinchHand()
    {
        var pts = FingerGeometryTests.IndexPointerHand();
        pts[8] = new Vec2(0.460f, 0.550f);
        pts[4] = new Vec2(0.455f, 0.550f);
        return pts;
    }

    [Fact]
    public void BriefTrackingLoss_FreezesWithoutReset()
    {
        var pipeline = CreatePipeline();
        pipeline.Process(HandAt(0));
        Assert.Equal(MachineState.Moving, pipeline.State);

        // 宽限期内丢失:状态保持 Moving,不产生动作
        var lost = pipeline.Process(null);
        Assert.Equal(MouseActionKind.None, lost.Kind);
        Assert.Equal(MachineState.Moving, lost.State);

        // 重捕获(原位):立即恢复移动,无复位跳变
        var back = pipeline.Process(HandAt(0));
        Assert.Equal(MachineState.Moving, back.State);
        Assert.Equal(MouseActionKind.Move, back.Kind);
    }

    [Fact]
    public void ProlongedLoss_ResetsToIdle()
    {
        var pipeline = CreatePipeline();
        pipeline.Process(HandAt(0));

        for (var i = 0; i < 6; i++)
            Assert.Equal(MachineState.Moving, pipeline.Process(null).State);

        var expired = pipeline.Process(null);
        Assert.Equal(MachineState.Idle, expired.State);
    }

    [Fact]
    public void ProlongedLoss_WhileDragging_FlushesLeftUp()
    {
        var pipeline = CreatePipeline();
        var pinch = FingerGeometryTests.PinchLeft();
        pipeline.Process(pinch);

        // 捏住移动进入拖拽
        var moved = new Vec2[pinch.Length];
        for (var i = 0; i < pinch.Length; i++)
            moved[i] = new Vec2(pinch[i].X + 0.08f, pinch[i].Y);
        var drag = pipeline.Process(moved);
        Assert.Equal(MachineState.Dragging, drag.State);

        for (var i = 0; i < 6; i++)
            pipeline.Process(null);

        var expired = pipeline.Process(null);
        Assert.Equal(MouseActionKind.LeftUp, expired.Kind);
        Assert.Equal(MachineState.Idle, expired.State);
    }
}

public class TrackingTests
{
    [Fact]
    public void EmaSmoother_BlendsTowardRaw()
    {
        var s = new EmaSmoother(0.5);
        var a = s.Filter(new Vec2(0, 0));
        Assert.Equal(0, a.X);
        var b = s.Filter(new Vec2(1, 1));
        Assert.Equal(0.5f, b.X, 3);
    }

    [Fact]
    public void DeadZone_DetectsCenter()
    {
        var dz = new DeadZoneFilter(0.1);
        Assert.True(dz.IsInside(new Vec2(0.5f, 0.5f)));
        Assert.False(dz.IsInside(new Vec2(0.9f, 0.9f)));
    }

    [Fact]
    public void CoordinateMapper_AppliesMirrorAndSensitivity()
    {
        var mapper = new CoordinateMapper(() => (1000, 1000), () => true, () => 1.0);
        var (x, y) = mapper.ToScreen(new Vec2(0.25f, 0.25f));
        // mirror → 0.75 → 0.75 * 999 ≈ 749.25
        Assert.InRange(x, 749f, 750f);
        Assert.InRange(y, 249f, 250f);
    }
}
