using System.Runtime.InteropServices;
using LingXuZhi.Core.Configuration;
using LingXuZhi.Core.Gestures;
using LingXuZhi.Core.Pipeline;
using LingXuZhi.Core.Tracking;
using LingXuZhi.Platform.Mouse;
using LingXuZhi.Vision.Abstractions;

namespace LingXuZhi.App.Services;

/// <summary>将视觉关键点接入手势管线,并按开关驱动鼠标。</summary>
public sealed class GestureControlService
{
    private readonly AppSettings _settings;
    private readonly IMouseController _mouse;
    private readonly GesturePipeline _pipeline;
    private int _frameW = 1280;
    private int _frameH = 720;
    private float _lastScreenX;
    private float _lastScreenY;
    private bool _hasLastScreen;

    public GestureControlService(AppSettings settings, IMouseController mouse)
    {
        _settings = settings;
        _mouse = mouse;

        var recognizer = new DefaultGestureRecognizer(() => _settings.PinchThreshold);
        var stateMachine = new GestureStateMachine(
            () => (int)Math.Round(_settings.DebounceFrames),
            () => _settings.DragThreshold);
        var smoother = new EmaSmoother(() => Math.Clamp(1.0 - _settings.Smoothing, 0.05, 1.0));
        var deadZone = new DeadZoneFilter(() =>
        {
            var minSide = Math.Max(1, Math.Min(_frameW, _frameH));
            return _settings.DeadZoneRadius / minSide;
        });
        // 镜像开关作用在采集帧上(预览与识别共用镜像后的帧),关键点已是镜像坐标:
        // 镜像开 → 坐标方向已符合直觉,映射器不得再翻转(否则左右颠倒);
        // 镜像关 → 帧未翻转,由映射器补一次翻转,保证"手向右、光标向右"
        var mapper = new CoordinateMapper(
            () => (GetSystemMetrics(0), GetSystemMetrics(1)),
            () => !_settings.Mirror,
            () => _settings.Sensitivity);

        _pipeline = new GesturePipeline(
            recognizer, stateMachine, smoother, deadZone, mapper,
            () => (int)Math.Round(_settings.ScrollLines),
            () => _settings.PinchThreshold);
    }

    public int ScrollEventCount { get; private set; }

    public int ClickEventCount { get; private set; }

    public string LastTransition => _pipeline.LastTransition;

    public MouseAction? LastAction { get; private set; }

    public float LastMouseDx { get; private set; }

    public float LastMouseDy { get; private set; }

    public void SetFrameSize(int width, int height)
    {
        if (width > 0) _frameW = width;
        if (height > 0) _frameH = height;
    }

    public MouseAction Process(HandLandmarkResult landmarks, int frameWidth, int frameHeight)
    {
        SetFrameSize(frameWidth, frameHeight);

        if (!landmarks.Detected || landmarks.Landmarks.Count < 21)
        {
            // 交给管线复位:拖拽中断需补发 LeftUp,待定单击需补发 LeftClick
            var flushAction = _pipeline.Process(null);
            _hasLastScreen = false;
            LastAction = null;
            if (_settings.MouseSimulationEnabled)
                Apply(flushAction);
            return flushAction;
        }

        var norm = new Vec2[21];
        for (var i = 0; i < 21; i++)
        {
            var p = landmarks.Landmarks[i];
            norm[i] = new Vec2(p.X / frameWidth, p.Y / frameHeight);
        }

        var action = _pipeline.Process(norm);
        LastAction = action;

        if (_settings.MouseSimulationEnabled)
            Apply(action);

        return action;
    }

    private void Apply(MouseAction action)
    {
        switch (action.Kind)
        {
            case MouseActionKind.Move:
                if (_hasLastScreen)
                {
                    LastMouseDx = action.ScreenX - _lastScreenX;
                    LastMouseDy = action.ScreenY - _lastScreenY;
                }
                _mouse.MoveTo(action.ScreenX, action.ScreenY);
                _lastScreenX = action.ScreenX;
                _lastScreenY = action.ScreenY;
                _hasLastScreen = true;
                break;

            case MouseActionKind.LeftDown:
                _mouse.LeftDown();
                break;

            case MouseActionKind.LeftUp:
                _mouse.LeftUp();
                break;

            case MouseActionKind.LeftClick:
                _mouse.LeftClick();
                ClickEventCount++;
                break;

            case MouseActionKind.RightClick:
                _mouse.RightClick();
                ClickEventCount++;
                break;

            case MouseActionKind.Scroll:
                _mouse.Scroll(action.ScrollDelta);
                ScrollEventCount++;
                break;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
