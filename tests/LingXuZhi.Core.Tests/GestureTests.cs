using LingXuZhi.Core.Gestures;
using LingXuZhi.Core.Tracking;
using Xunit;

namespace LingXuZhi.Core.Tests;

public class FingerGeometryTests
{
    [Fact]
    public void SwordPointer_IsMidpointOfIndexAndMiddleTips()
    {
        var pts = FlatHand();
        pts[8] = new Vec2(0.4f, 0.2f);
        pts[12] = new Vec2(0.6f, 0.2f);
        var mid = FingerGeometry.SwordPointer(pts);
        Assert.Equal(0.5f, mid.X, 3);
        Assert.Equal(0.2f, mid.Y, 3);
    }

    [Fact]
    public void IsExtended_WhenTipFartherThanMcp()
    {
        var pts = FlatHand();
        Assert.True(FingerGeometry.IsExtended(pts, 8, 5));
        Assert.True(FingerGeometry.IsBent(pts, 16, 13)); // ring tip near mcp
    }

    /// <summary>构造简化手型:食中伸直,无名小指弯曲,拇指收拢。</summary>
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

    internal static Vec2[] OpenPalm()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.30f, 0.35f);
        pts[2] = new Vec2(0.38f, 0.50f);
        pts[16] = new Vec2(0.62f, 0.25f);
        pts[20] = new Vec2(0.70f, 0.28f);
        return pts;
    }

    internal static Vec2[] PinchLeft()
    {
        var pts = FlatHand();
        pts[4] = new Vec2(0.490f, 0.250f);
        pts[8] = new Vec2(0.492f, 0.250f); // almost overlapping → pinch
        pts[12] = new Vec2(0.55f, 0.22f);  // middle extended
        return pts;
    }
}

public class GestureRecognizerTests
{
    private readonly DefaultGestureRecognizer _sut = new(0.05);

    [Fact]
    public void Recognize_SwordGesture()
    {
        var obs = _sut.Recognize(FingerGeometryTests.FlatHand());
        Assert.Equal(GestureKind.Pointer, obs.Kind);
    }

    [Fact]
    public void Recognize_OpenPalm()
    {
        var obs = _sut.Recognize(FingerGeometryTests.OpenPalm());
        Assert.Equal(GestureKind.OpenPalm, obs.Kind);
    }

    [Fact]
    public void Recognize_PinchLeft()
    {
        var obs = _sut.Recognize(FingerGeometryTests.PinchLeft());
        Assert.Equal(GestureKind.PinchLeft, obs.Kind);
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
    [Fact]
    public void RequiresDebounceFrames_BeforeMoving()
    {
        var sm = new GestureStateMachine(3);
        var pointer = new GestureObservation(GestureKind.Pointer, new Vec2(0.5f, 0.3f), new Vec2(0.5f, 0.5f), 1);

        var r1 = sm.Update(pointer);
        Assert.Equal(MachineState.Idle, r1.State);

        sm.Update(pointer);
        var r3 = sm.Update(pointer);
        Assert.Equal(MachineState.Moving, r3.State);
        Assert.Equal(MouseActionKind.Move, r3.Action);
    }

    [Fact]
    public void LeftClick_FiresOnceUntilRelease()
    {
        var sm = new GestureStateMachine(1);
        sm.Update(new GestureObservation(GestureKind.Pointer, default, default, 1));
        Assert.Equal(MachineState.Moving, sm.State);

        var pinch = new GestureObservation(GestureKind.PinchLeft, default, default, 0.02f);
        var click = sm.Update(pinch);
        Assert.Equal(MachineState.LeftClick, click.State);
        Assert.Equal(MouseActionKind.LeftClick, click.Action);

        var again = sm.Update(pinch);
        Assert.Equal(MouseActionKind.None, again.Action);
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
