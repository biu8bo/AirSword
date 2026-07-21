namespace LingXuZhi.Core.Gestures;

/// <summary>带去抖的手势状态机。捏合触发一次性点击后回到 Moving(若仍像剑指)或 Idle。</summary>
public sealed class GestureStateMachine
{
    private readonly Func<int> _debounceFrames;
    private MachineState _state = MachineState.Idle;
    private GestureKind _pendingKind = GestureKind.Idle;
    private int _pendingCount;
    private bool _clickConsumed;
    private bool _scrollArmed = true;
    private float _scrollAnchorY;
    private MachineState _lastEmitted = MachineState.Idle;

    public MachineState State => _state;

    public string LastTransition { get; private set; } = "—";

    public GestureStateMachine(Func<int> debounceFrames)
    {
        _debounceFrames = debounceFrames;
    }

    public GestureStateMachine(int debounceFrames = 3)
        : this(() => debounceFrames)
    {
    }

    /// <summary>
    /// 输入瞬时手势,返回本帧应执行的动作种类(Move/Click/Scroll/None)。
    /// scrollDelta 非 0 表示触发滚轮(符号:上正下负)。
    /// </summary>
    public (MachineState State, MouseActionKind Action, int ScrollDelta) Update(GestureObservation obs)
    {
        var target = MapKindToState(obs.Kind);
        ApplyDebouncedTransition(target, obs);

        var action = MouseActionKind.None;
        var scrollDelta = 0;

        switch (_state)
        {
            case MachineState.Moving:
                _clickConsumed = false;
                _scrollArmed = true;
                action = MouseActionKind.Move;
                break;

            case MachineState.LeftClick:
                if (!_clickConsumed)
                {
                    action = MouseActionKind.LeftClick;
                    _clickConsumed = true;
                }
                break;

            case MachineState.RightClick:
                if (!_clickConsumed)
                {
                    action = MouseActionKind.RightClick;
                    _clickConsumed = true;
                }
                break;

            case MachineState.Scroll:
                (action, scrollDelta) = UpdateScroll(obs);
                break;

            case MachineState.Idle:
                _clickConsumed = false;
                _scrollArmed = true;
                break;
        }

        if (_state != _lastEmitted)
        {
            LastTransition = $"{_lastEmitted} → {_state}";
            _lastEmitted = _state;
        }

        return (_state, action, scrollDelta);
    }

    public void Reset()
    {
        _state = MachineState.Idle;
        _pendingKind = GestureKind.Idle;
        _pendingCount = 0;
        _clickConsumed = false;
        _scrollArmed = true;
        LastTransition = "Reset → Idle";
        _lastEmitted = MachineState.Idle;
    }

    private void ApplyDebouncedTransition(MachineState target, GestureObservation obs)
    {
        // 点击态:保持捏合直到释放
        if (_state is MachineState.LeftClick or MachineState.RightClick)
        {
            if (obs.Kind is GestureKind.PinchLeft or GestureKind.PinchRight)
                return;

            if (obs.Kind == GestureKind.Pointer)
            {
                TransitionTo(MachineState.Moving);
                return;
            }
        }

        if (target == _state)
        {
            _pendingCount = 0;
            _pendingKind = obs.Kind;
            return;
        }

        if (_pendingKind != obs.Kind)
        {
            _pendingKind = obs.Kind;
            _pendingCount = 1;
        }
        else
        {
            _pendingCount++;
        }

        var need = Math.Max(1, _debounceFrames());
        if (_pendingCount >= need)
        {
            TransitionTo(target);
            _pendingCount = 0;
            if (_state == MachineState.Scroll)
            {
                _scrollAnchorY = obs.PalmCenterNormalized.Y;
                _scrollArmed = true;
            }
        }
    }

    private (MouseActionKind, int) UpdateScroll(GestureObservation obs)
    {
        if (obs.Kind != GestureKind.OpenPalm)
            return (MouseActionKind.None, 0);

        const float trigger = 0.04f;
        const float recenter = 0.015f;
        var dy = obs.PalmCenterNormalized.Y - _scrollAnchorY;

        if (!_scrollArmed)
        {
            if (MathF.Abs(dy) < recenter)
                _scrollArmed = true;
            return (MouseActionKind.None, 0);
        }

        if (dy < -trigger)
        {
            _scrollArmed = false;
            _scrollAnchorY = obs.PalmCenterNormalized.Y;
            return (MouseActionKind.Scroll, 1);
        }

        if (dy > trigger)
        {
            _scrollArmed = false;
            _scrollAnchorY = obs.PalmCenterNormalized.Y;
            return (MouseActionKind.Scroll, -1);
        }

        return (MouseActionKind.None, 0);
    }

    private void TransitionTo(MachineState next)
    {
        _state = next;
        _clickConsumed = false;
    }

    private static MachineState MapKindToState(GestureKind kind) => kind switch
    {
        GestureKind.Pointer => MachineState.Moving,
        GestureKind.PinchLeft => MachineState.LeftClick,
        GestureKind.PinchRight => MachineState.RightClick,
        GestureKind.OpenPalm => MachineState.Scroll,
        _ => MachineState.Idle,
    };
}
