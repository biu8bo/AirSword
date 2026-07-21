namespace LingXuZhi.Core.Gestures;

/// <summary>
/// 带去抖的手势状态机。
/// 拇指-食指捏合进入 PinchPending:原地松开为单击(延迟一个双击窗口确认)、
/// 窗口内再次捏合并松开为双击、捏住移动超过阈值转为拖拽(LeftDown…Move…LeftUp)。
/// 张开手掌进入滚轮:五指张开持续上滚,拇指内收持续下滚。
/// </summary>
public sealed class GestureStateMachine
{
    private const long ScrollRepeatMs = 160;

    private readonly Func<int> _debounceFrames;
    private readonly Func<double> _dragThreshold;
    private readonly Func<int> _doublePinchWindowMs;
    private readonly Func<long> _clock;

    private MachineState _state = MachineState.Idle;
    private GestureKind _pendingKind = GestureKind.Idle;
    private int _pendingCount;
    private bool _rightClickConsumed;
    private MachineState _lastEmitted = MachineState.Idle;

    private readonly Queue<MouseActionKind> _events = new();
    private Vec2 _pinchAnchor;
    private bool _doubleCandidate;
    private long _pendingClickAt = -1;
    private long _nextScrollAt;

    public MachineState State => _state;

    public string LastTransition { get; private set; } = "—";

    public GestureStateMachine(
        Func<int> debounceFrames,
        Func<double>? dragThreshold = null,
        Func<int>? doublePinchWindowMs = null,
        Func<long>? clock = null)
    {
        _debounceFrames = debounceFrames;
        _dragThreshold = dragThreshold ?? (() => 0.03);
        _doublePinchWindowMs = doublePinchWindowMs ?? (() => 350);
        _clock = clock ?? (() => Environment.TickCount64);
    }

    public GestureStateMachine(int debounceFrames = 3)
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

        // 单击延迟确认:双击窗口内没有第二次捏合,补发单击
        if (_pendingClickAt >= 0 && now - _pendingClickAt >= _doublePinchWindowMs())
        {
            _pendingClickAt = -1;
            _events.Enqueue(MouseActionKind.LeftClick);
        }

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
                    // 点击待定期间冻结指针,保证单击/双击落点不漂移
                    if (_pendingClickAt < 0)
                        action = MouseActionKind.Move;
                    break;

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
    /// 手部丢失时复位。返回需要补发的收尾动作:
    /// 拖拽中断补 LeftUp,待定单击补 LeftClick。
    /// </summary>
    public MouseActionKind Reset()
    {
        var flush = MouseActionKind.None;
        if (_state == MachineState.Dragging)
            flush = MouseActionKind.LeftUp;
        else if (_pendingClickAt >= 0 || _doubleCandidate)
            flush = MouseActionKind.LeftClick;

        _state = MachineState.Idle;
        _pendingKind = GestureKind.Idle;
        _pendingCount = 0;
        _rightClickConsumed = false;
        _events.Clear();
        _doubleCandidate = false;
        _pendingClickAt = -1;
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
                    if (_doubleCandidate)
                    {
                        // 前一次捏合是独立单击,先补发再进入拖拽
                        _events.Enqueue(MouseActionKind.LeftClick);
                        _doubleCandidate = false;
                    }
                    _events.Enqueue(MouseActionKind.LeftDown);
                    TransitionTo(MachineState.Dragging);
                }
                return;
            }

            if (!DebounceReached(obs.Kind))
                return;

            // 拇食捏合过程中中指跟着靠拢 → 实为三指捏合(右键),不是一次左键点击
            if (target == MachineState.RightClick)
            {
                _doubleCandidate = false;
                TransitionTo(target);
                return;
            }

            // 原地松开:窗口内第二次捏合 → 双击;否则挂起等窗口确认单击
            if (_doubleCandidate)
            {
                _events.Enqueue(MouseActionKind.DoubleClick);
                _doubleCandidate = false;
            }
            else
            {
                _pendingClickAt = now;
            }
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

            if (!DebounceReached(obs.Kind))
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
        {
            _pinchAnchor = smoothedPointer;
            if (_pendingClickAt >= 0 && now - _pendingClickAt <= _doublePinchWindowMs())
            {
                _doubleCandidate = true;
                _pendingClickAt = -1;
            }
            else
            {
                _doubleCandidate = false;
            }
        }

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

    private bool DebounceReached(GestureKind kind)
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

        if (_pendingCount >= Math.Max(1, _debounceFrames()))
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
