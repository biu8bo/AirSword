using LingXuZhi.Core.Gestures;
using LingXuZhi.Core.Tracking;

namespace LingXuZhi.Core.Pipeline;

/// <summary>阶段2关键点 → 捏合锁定 → 跳变防护 → 平滑 → 死区 → 状态机 → MouseAction。</summary>
public sealed class GesturePipeline
{
    /// <summary>跟丢宽限帧数:短暂丢失期间冻结指针与状态,不复位,避免重捕获后闪跳。</summary>
    private const int LostGraceFrames = 6;

    /// <summary>单帧跳变判定阈值(归一化距离):超过视为疑似坏帧/重捕获跳变。</summary>
    private const float JumpThreshold = 0.12f;

    /// <summary>跳变需连续出现的帧数,达到才接受为真实大位移。</summary>
    private const int JumpConfirmFrames = 3;

    /// <summary>捏合锁定进入区 = 捏合阈值 + 该余量:拇指与指尖接近到此距离即锁定指针。</summary>
    private const float PinchApproachMargin = 0.12f;

    /// <summary>锁定时指针回退的帧数,取回手指开始弯曲前的位置。</summary>
    private const int LockLookbackFrames = 6;

    /// <summary>指针位置历史容量。</summary>
    private const int HistoryCapacity = 16;

    private readonly IGestureRecognizer _recognizer;
    private readonly GestureStateMachine _stateMachine;
    private readonly ISmoother _smoother;
    private readonly DeadZoneFilter _deadZone;
    private readonly CoordinateMapper _mapper;
    private readonly Func<int> _scrollLines;
    private readonly Func<double> _pinchThreshold;

    private int _lostFrames;
    private bool _tracking;
    private Vec2 _lastAccepted;
    private Vec2 _lastSmoothed;
    private int _jumpFrames;

    private readonly List<Vec2> _pointerHistory = new(HistoryCapacity);
    private bool _pinchLocked;
    private Vec2 _lockedPointer;
    private Vec2 _lockedMcp;

    public GesturePipeline(
        IGestureRecognizer recognizer,
        GestureStateMachine stateMachine,
        ISmoother smoother,
        DeadZoneFilter deadZone,
        CoordinateMapper mapper,
        Func<int> scrollLines,
        Func<double>? pinchThreshold = null)
    {
        _recognizer = recognizer;
        _stateMachine = stateMachine;
        _smoother = smoother;
        _deadZone = deadZone;
        _mapper = mapper;
        _scrollLines = scrollLines;
        _pinchThreshold = pinchThreshold ?? (() => 0.12);
    }

    public string LastTransition => _stateMachine.LastTransition;

    public MachineState State => _stateMachine.State;

    public MouseAction Process(IReadOnlyList<Vec2>? landmarksNormalized)
    {
        if (landmarksNormalized is null || landmarksNormalized.Count < 21)
            return ProcessLost();

        var obs = _recognizer.Recognize(landmarksNormalized);
        _lostFrames = 0;

        var stabilized = StabilizePointer(obs, landmarksNormalized);
        var guarded = FilterJump(stabilized);
        var smoothed = _smoother.Filter(guarded);
        _lastSmoothed = smoothed;
        var inDeadZone = _deadZone.IsInside(smoothed);
        var (sx, sy) = _mapper.ToScreen(smoothed);

        var (state, action, scrollSign) = _stateMachine.Update(obs, smoothed);

        if (action == MouseActionKind.Move && inDeadZone)
            action = MouseActionKind.None;

        var scrollDelta = 0;
        if (action == MouseActionKind.Scroll && scrollSign != 0)
            scrollDelta = scrollSign * Math.Max(1, _scrollLines());

        return new MouseAction(
            action, sx, sy, scrollDelta, inDeadZone,
            state, obs.Kind, obs.PointerNormalized, smoothed,
            obs.PinchLeftDistance, obs.PinchRightDistance);
    }

    public void Reset()
    {
        _smoother.Reset();
        _stateMachine.Reset();
        _tracking = false;
        _lostFrames = 0;
        _jumpFrames = 0;
        ResetPinchLock();
    }

    /// <summary>
    /// 捏合锁定:防止捏合时食指弯向拇指带偏指针,导致点击落点偏移。
    /// 拇指与食/中指尖接近到锁定区时,指针锚定到手指开始弯曲前(回退数帧)的位置;
    /// 锁定期间指针只随食指根(掌指关节)平移 —— 弯手指不动、移手才动,拖拽因此不受影响。
    /// 指尖远离且状态机不处于捏合相关状态后解除锁定。
    /// </summary>
    private Vec2 StabilizePointer(GestureObservation obs, IReadOnlyList<Vec2> landmarks)
    {
        var mcp = landmarks[FingerGeometry.IndexMcp];
        var pinchDist = MathF.Min(obs.PinchLeftDistance, obs.PinchRightDistance);
        var enter = (float)_pinchThreshold() + PinchApproachMargin;
        var exit = enter * 1.25f;

        if (!_pinchLocked)
        {
            if (pinchDist < enter)
            {
                _pinchLocked = true;
                _lockedPointer = HistoryLookback(obs.PointerNormalized);
                _lockedMcp = mcp;
            }
            else
            {
                PushHistory(obs.PointerNormalized);
                return obs.PointerNormalized;
            }
        }

        var machineHolds = _stateMachine.State
            is MachineState.PinchPending or MachineState.Dragging or MachineState.RightClick;
        if (pinchDist > exit && !machineHolds)
        {
            ResetPinchLock();
            PushHistory(obs.PointerNormalized);
            return obs.PointerNormalized;
        }

        return new Vec2(
            _lockedPointer.X + (mcp.X - _lockedMcp.X),
            _lockedPointer.Y + (mcp.Y - _lockedMcp.Y));
    }

    private void PushHistory(Vec2 pointer)
    {
        if (_pointerHistory.Count >= HistoryCapacity)
            _pointerHistory.RemoveAt(0);
        _pointerHistory.Add(pointer);
    }

    private Vec2 HistoryLookback(Vec2 fallback)
    {
        if (_pointerHistory.Count == 0)
            return fallback;
        var index = Math.Max(0, _pointerHistory.Count - 1 - LockLookbackFrames);
        return _pointerHistory[index];
    }

    private void ResetPinchLock()
    {
        _pinchLocked = false;
        _pointerHistory.Clear();
    }

    /// <summary>
    /// 手部丢失帧:宽限期内冻结(保持状态与平滑历史,输出 None),
    /// 超过宽限才真正复位,并补发收尾动作(拖拽中断 LeftUp / 待定单击 LeftClick)。
    /// </summary>
    private MouseAction ProcessLost()
    {
        if (_tracking && _lostFrames < LostGraceFrames)
        {
            _lostFrames++;
            return new MouseAction(
                MouseActionKind.None, 0, 0, 0, false,
                _stateMachine.State, GestureKind.Idle, default, _lastSmoothed);
        }

        _smoother.Reset();
        var flush = _tracking ? _stateMachine.Reset() : MouseActionKind.None;
        _tracking = false;
        _lostFrames = 0;
        _jumpFrames = 0;
        ResetPinchLock();
        return new MouseAction(
            flush, 0, 0, 0, false,
            MachineState.Idle, GestureKind.Idle, default, default);
    }

    /// <summary>
    /// 跳变防护:与上次接受位置的单帧距离超过阈值时,先按旧位置处理;
    /// 连续多帧维持在新位置才接受(视为真实大位移),并复位平滑器快速跟上。
    /// 用于抑制坏关键点帧与跟丢重捕获导致的指针闪跳。
    /// </summary>
    private Vec2 FilterJump(Vec2 raw)
    {
        if (!_tracking)
        {
            _tracking = true;
            _lastAccepted = raw;
            _jumpFrames = 0;
            return raw;
        }

        if (raw.DistanceTo(_lastAccepted) <= JumpThreshold)
        {
            _lastAccepted = raw;
            _jumpFrames = 0;
            return raw;
        }

        _jumpFrames++;
        if (_jumpFrames < JumpConfirmFrames)
            return _lastAccepted;

        _lastAccepted = raw;
        _jumpFrames = 0;
        _smoother.Reset();
        return raw;
    }
}
