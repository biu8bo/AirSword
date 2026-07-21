namespace LingXuZhi.Core.Gestures;

/// <summary>
/// 带去抖的手势状态机。
/// 拇指-食指捏合进入 PinchPending:原地松开立即单击,捏住移动超过阈值转为拖拽。
/// 食中指尖并拢进入右键;张开手掌进入滚轮(拇指定向)。
/// 双击不单独做手势——连续两次单击由系统自行识别。
/// </summary>
public sealed class GestureStateMachine
{
    private const long ScrollRepeatMs = 160;

    private readonly Func<int> _debounceFrames;
    private readonly Func<double> _dragThreshold;
    private readonly Func<long> _clock;

    private MachineState _state = MachineState.Idle;
    private GestureKind _pendingKind = GestureKind.Idle;
    private int _pendingCount;
    private bool _rightClickConsumed;
    private MachineState _lastEmitted = MachineState.Idle;

    private readonly Queue<MouseActionKind> _events = new();
    private Vec2 _pinchAnchor;
    private long _nextScrollAt;

    public MachineState State => _state;

    public string LastTransition { get; private set; } = "—";

    public GestureStateMachine(
        Func<int> debounceFrames,
        Func<double>? dragThreshold = null,
        Func<long>? clock = null)
    {
        _debounceFrames = debounceFrames;
        _dragThreshold = dragThreshold ?? (() => 0.05);
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public GestureStateMachine(int debounceFrames = 2)
        : this(() => debounceFrames)
    {
    }

    /// <summary>
    /// 输入瞬时手势与平滑后的指针位置,返回本帧应执行的动作。
    /// scrollDelta 非 0 表示触发滚轮(符号:上正下负)。
    /// </summary>
    public (MachineState State, MouseActionKind Action, int ScrollDelta) Update(GestureObservation obs, Vec2 smoothedPointer)
    {
        var now = _clock();
        ApplyTransition(obs, smoothedPointer, now);

        var action = MouseActionKind.None;
        var scrollDelta = 0;

        if (_events.Count > 0)
        {
            action = _events.Dequeue();
        }
        else
        {
            switch (_state)
            {
                case MachineState.Moving:
                case MachineState.Dragging:
                    action = MouseActionKind.Move;
                    break;

                case MachineState.RightClick:
                    if (!_rightClickConsumed)
                    {
                        action = MouseActionKind.RightClick;
                        _rightClickConsumed = true;
                    }
                    break;

                case MachineState.Scroll:
                    (action, scrollDelta) = ScrollTick(obs, now);
                    break;
            }
        }

        if (_state != _lastEmitted)
        {
            LastTransition = $"{_lastEmitted} → {_state}";
            _lastEmitted = _state;
        }

        return (_state, action, scrollDelta);
    }

    /// <summary>兼容重载:用观测的原始指针位置作为平滑位置。</summary>
    public (MachineState State, MouseActionKind Action, int ScrollDelta) Update(GestureObservation obs)
        => Update(obs, obs.PointerNormalized);

    /// <summary>
    /// 手部丢失时复位。拖拽中断补 LeftUp;捏合待定中丢手补 LeftClick。
    /// </summary>
    public MouseActionKind Reset()
    {
        var flush = MouseActionKind.None;
        if (_state == MachineState.Dragging)
            flush = MouseActionKind.LeftUp;
        else if (_state == MachineState.PinchPending)
            flush = MouseActionKind.LeftClick;

        _state = MachineState.Idle;
        _pendingKind = GestureKind.Idle;
        _pendingCount = 0;
        _rightClickConsumed = false;
        _events.Clear();
        LastTransition = "Reset → Idle";
        _lastEmitted = MachineState.Idle;
        return flush;
    }

    private void ApplyTransition(GestureObservation obs, Vec2 smoothedPointer, long now)
    {
        var target = MapKindToState(obs.Kind);

        if (_state == MachineState.PinchPending)
        {
            if (obs.Kind == GestureKind.PinchLeft)
            {
                ResetDebounce(obs.Kind);
                if (smoothedPointer.DistanceTo(_pinchAnchor) > (float)_dragThreshold())
                {
                    _events.Enqueue(MouseActionKind.LeftDown);
                    TransitionTo(MachineState.Dragging);
                }
                return;
            }

            // 退出捏合用更短去抖(至少 1 帧),避免快速捏合松开被漏掉
            if (!DebounceReached(obs.Kind, exitPinch: true))
                return;

            // 捏合待定中途变成右键(比耶并拢) → 取消单击,直接转右键
            if (target == MachineState.RightClick)
            {
                TransitionTo(target);
                return;
            }

            // 原地松开 → 立即单击(不做双击窗口)
            _events.Enqueue(MouseActionKind.LeftClick);
            TransitionTo(target);
            return;
        }

        if (_state == MachineState.Dragging)
        {
            if (obs.Kind == GestureKind.PinchLeft)
            {
                ResetDebounce(obs.Kind);
                return;
            }

            if (!DebounceReached(obs.Kind, exitPinch: true))
                return;

            _events.Enqueue(MouseActionKind.LeftUp);
            TransitionTo(target);
            return;
        }

        if (target == _state)
        {
            ResetDebounce(obs.Kind);
            return;
        }

        if (!DebounceReached(obs.Kind))
            return;

        if (target == MachineState.PinchPending)
            _pinchAnchor = smoothedPointer;

        if (target == MachineState.Scroll)
            _nextScrollAt = now;

        TransitionTo(target);
    }

    private (MouseActionKind, int) ScrollTick(GestureObservation obs, long now)
    {
        var direction = obs.Kind switch
        {
            GestureKind.OpenPalm => 1,
            GestureKind.OpenPalmThumbIn => -1,
            _ => 0,
        };

        if (direction == 0 || now < _nextScrollAt)
            return (MouseActionKind.None, 0);

        _nextScrollAt = now + ScrollRepeatMs;
        return (MouseActionKind.Scroll, direction);
    }

    /// <param name="exitPinch">退出捏合/拖拽时用更短阈值(默认去抖减 1,至少 1)。</param>
    private bool DebounceReached(GestureKind kind, bool exitPinch = false)
    {
        if (_pendingKind != kind)
        {
            _pendingKind = kind;
            _pendingCount = 1;
        }
        else
        {
            _pendingCount++;
        }

        var need = Math.Max(1, _debounceFrames());
        if (exitPinch)
            need = Math.Max(1, need - 1);

        if (_pendingCount >= need)
        {
            _pendingCount = 0;
            return true;
        }

        return false;
    }

    private void ResetDebounce(GestureKind kind)
    {
        _pendingKind = kind;
        _pendingCount = 0;
    }

    private void TransitionTo(MachineState next)
    {
        _state = next;
        _rightClickConsumed = false;
    }

    private static MachineState MapKindToState(GestureKind kind) => kind switch
    {
        GestureKind.Pointer => MachineState.Moving,
        GestureKind.PinchLeft => MachineState.PinchPending,
        GestureKind.PinchRight => MachineState.RightClick,
        GestureKind.OpenPalm or GestureKind.OpenPalmThumbIn => MachineState.Scroll,
        _ => MachineState.Idle,
    };
}
