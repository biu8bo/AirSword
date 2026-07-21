using LingXuZhi.Core.Gestures;
using LingXuZhi.Core.Tracking;

namespace LingXuZhi.Core.Pipeline;

/// <summary>阶段2关键点 → 平滑 → 死区 → 状态机 → MouseAction。</summary>
public sealed class GesturePipeline
{
    private readonly IGestureRecognizer _recognizer;
    private readonly GestureStateMachine _stateMachine;
    private readonly ISmoother _smoother;
    private readonly DeadZoneFilter _deadZone;
    private readonly CoordinateMapper _mapper;
    private readonly Func<int> _scrollLines;

    public GesturePipeline(
        IGestureRecognizer recognizer,
        GestureStateMachine stateMachine,
        ISmoother smoother,
        DeadZoneFilter deadZone,
        CoordinateMapper mapper,
        Func<int> scrollLines)
    {
        _recognizer = recognizer;
        _stateMachine = stateMachine;
        _smoother = smoother;
        _deadZone = deadZone;
        _mapper = mapper;
        _scrollLines = scrollLines;
    }

    public string LastTransition => _stateMachine.LastTransition;

    public MachineState State => _stateMachine.State;

    public MouseAction Process(IReadOnlyList<Vec2>? landmarksNormalized)
    {
        if (landmarksNormalized is null || landmarksNormalized.Count < 21)
        {
            _smoother.Reset();
            _stateMachine.Reset();
            return new MouseAction(
                MouseActionKind.None, 0, 0, 0, false,
                MachineState.Idle, GestureKind.Idle, default, default);
        }

        var obs = _recognizer.Recognize(landmarksNormalized);
        var smoothed = _smoother.Filter(obs.PointerNormalized);
        var inDeadZone = _deadZone.IsInside(smoothed);
        var (sx, sy) = _mapper.ToScreen(smoothed);

        var (state, action, scrollSign) = _stateMachine.Update(obs);

        if (action == MouseActionKind.Move && inDeadZone)
            action = MouseActionKind.None;

        var scrollDelta = 0;
        if (action == MouseActionKind.Scroll && scrollSign != 0)
            scrollDelta = scrollSign * Math.Max(1, _scrollLines());

        return new MouseAction(
            action, sx, sy, scrollDelta, inDeadZone,
            state, obs.Kind, obs.PointerNormalized, smoothed);
    }

    public void Reset()
    {
        _smoother.Reset();
        _stateMachine.Reset();
    }
}
