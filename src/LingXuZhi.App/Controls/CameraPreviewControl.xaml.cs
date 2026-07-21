using Autofac;
using LingXuZhi.App.Services;
using LingXuZhi.App.ViewModels;
using LingXuZhi.Core.Gestures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Text;
using Windows.Foundation;
using Windows.UI;

namespace LingXuZhi.App.Controls;

public sealed partial class CameraPreviewControl : UserControl
{
    /// <summary>MediaPipe 21 关键点拓扑(手指 + 手掌轮廓)。</summary>
    private static readonly (int A, int B)[] Bones =
    {
        (0, 1), (1, 2), (2, 3), (3, 4),
        (0, 5), (5, 6), (6, 7), (7, 8),
        (5, 9), (9, 10), (10, 11), (11, 12),
        (9, 13), (13, 14), (14, 15), (15, 16),
        (13, 17), (17, 18), (18, 19), (19, 20),
        (0, 17),
    };

    private static readonly HashSet<int> FingertipIds = new() { 4, 8, 12, 16, 20 };

    /// <summary>整手包围框相对关键点范围的外边距比例。</summary>
    private const float HandBoxPadding = 0.12f;

    // 边界框时间平滑(EMA):位置响应稍快,尺寸/角度更稳,抑制逐帧检测噪声导致的抖动
    private const double BoxCenterAlpha = 0.35;
    private const double BoxSizeAlpha = 0.18;
    private const double BoxAngleAlpha = 0.25;

    private bool _boxSmoothingValid;
    private double _smBoxCx;
    private double _smBoxCy;
    private double _smBoxHalfU;
    private double _smBoxHalfV;
    private double _smBoxAngle;

    private readonly Line[] _boneLines = new Line[Bones.Length];
    private readonly Ellipse[] _landmarkDots = new Ellipse[21];
    private readonly Ellipse[] _palmDots = new Ellipse[7];
    private readonly Polygon _boundingBox;
    private readonly TextBlock _confidenceText;
    private readonly Ellipse _deadZoneCircle;
    private readonly Line _crossH;
    private readonly Line _crossV;
    private readonly TextBlock _gestureLabel;

    public MainViewModel ViewModel { get; }

    public Visibility LoadingVisibility(bool isPreviewReady)
        => isPreviewReady ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ReadyVisibility(bool isPreviewReady)
        => isPreviewReady ? Visibility.Visible : Visibility.Collapsed;

    public bool NotReady(bool isPreviewReady) => !isPreviewReady;

    public CameraPreviewControl()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 主题色:骨架用 Accent 蓝加亮;整手框用 Warning 琥珀,与骨架区分
        var accent = Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6);
        var accentBright = Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA);
        var tipFill = Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9);
        var jointFill = Color.FromArgb(0xFF, 0x93, 0xC5, 0xFD);
        var warning = Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B);
        var primary = (Brush)Application.Current.Resources["TextPrimaryBrush"];

        var boneBrush = new SolidColorBrush(accentBright);
        var tipBrush = new SolidColorBrush(tipFill);
        var tipStroke = new SolidColorBrush(accent);
        var jointBrush = new SolidColorBrush(jointFill);
        var jointStroke = new SolidColorBrush(accent);
        var warningBrush = new SolidColorBrush(warning);

        _boundingBox = new Polygon
        {
            Stroke = warningBrush,
            StrokeThickness = 2.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_boundingBox);

        for (var i = 0; i < Bones.Length; i++)
        {
            _boneLines[i] = new Line
            {
                Stroke = boneBrush,
                StrokeThickness = 3.5,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed,
            };
            OverlayCanvas.Children.Add(_boneLines[i]);
        }

        for (var i = 0; i < 7; i++)
        {
            _palmDots[i] = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = warningBrush,
                Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
            };
            OverlayCanvas.Children.Add(_palmDots[i]);
        }

        for (var i = 0; i < 21; i++)
        {
            var isTip = FingertipIds.Contains(i);
            _landmarkDots[i] = new Ellipse
            {
                Width = isTip ? 14 : 10,
                Height = isTip ? 14 : 10,
                Fill = isTip ? tipBrush : jointBrush,
                Stroke = isTip ? tipStroke : jointStroke,
                StrokeThickness = isTip ? 2.5 : 2,
                Visibility = Visibility.Collapsed,
            };
            OverlayCanvas.Children.Add(_landmarkDots[i]);
        }

        _confidenceText = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["MonoFont"],
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = primary,
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_confidenceText);

        var deadZoneBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B));
        _deadZoneCircle = new Ellipse
        {
            Stroke = warningBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = deadZoneBrush,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Insert(0, _deadZoneCircle);

        var crossBrush = new SolidColorBrush(Color.FromArgb(0xE6, 0x22, 0xC5, 0x5E));
        _crossH = new Line { Stroke = crossBrush, StrokeThickness = 2, Visibility = Visibility.Collapsed };
        _crossV = new Line { Stroke = crossBrush, StrokeThickness = 2, Visibility = Visibility.Collapsed };
        OverlayCanvas.Children.Add(_crossH);
        OverlayCanvas.Children.Add(_crossV);

        _gestureLabel = new TextBlock
        {
            FontFamily = (FontFamily)Application.Current.Resources["MonoFont"],
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = primary,
            Visibility = Visibility.Collapsed,
        };
        OverlayCanvas.Children.Add(_gestureLabel);

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.OverlayResult)
                or nameof(MainViewModel.LastMouseAction)
                or nameof(MainViewModel.GestureState))
                UpdateOverlay();
            else if (e.PropertyName is nameof(MainViewModel.IsPreviewReady))
                SyncLoadingAnimation();
        };
        ViewModel.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LingXuZhi.Core.Configuration.AppSettings.DrawSkeleton)
                or nameof(LingXuZhi.Core.Configuration.AppSettings.DrawBoundingBox)
                or nameof(LingXuZhi.Core.Configuration.AppSettings.DeadZoneRadius))
                UpdateOverlay();
        };
    }

    private Storyboard? _loadingPulse;

    private void OnLoaded(object sender, RoutedEventArgs e) => SyncLoadingAnimation();

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopLoadingPulse();

    private void SyncLoadingAnimation()
    {
        if (ViewModel.IsPreviewReady)
            StopLoadingPulse();
        else
            StartLoadingPulse();
    }

    private void StartLoadingPulse()
    {
        if (_loadingPulse is not null)
            return;

        _loadingPulse = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var fade = new DoubleAnimation
        {
            From = 0.45,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(900),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(fade, LoadingLabel);
        Storyboard.SetTargetProperty(fade, "Opacity");
        _loadingPulse.Children.Add(fade);
        _loadingPulse.Begin();
    }

    private void StopLoadingPulse()
    {
        if (_loadingPulse is null)
            return;
        _loadingPulse.Stop();
        _loadingPulse = null;
        LoadingLabel.Opacity = 1;
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => UpdateOverlay();

    private void UpdateOverlay()
    {
        var result = ViewModel.OverlayResult;
        var settings = ViewModel.Settings;
        var showSkeleton = result is { Detected: true } && settings.DrawSkeleton;
        var showBox = result?.Palm is not null && settings.DrawBoundingBox;

        double cw = OverlayCanvas.ActualWidth, ch = OverlayCanvas.ActualHeight;
        var frameW = result?.FrameWidth ?? PreviewBitmapWidth();
        var frameH = result?.FrameHeight ?? PreviewBitmapHeight();
        if (cw <= 0 || ch <= 0 || frameW <= 0)
        {
            HideAll();
            return;
        }

        var scale = Math.Min(cw / frameW, ch / frameH);
        var ox = (cw - frameW * scale) / 2;
        var oy = (ch - frameH * scale) / 2;
        Point Map(float x, float y) => new(x * scale + ox, y * scale + oy);

        // 中央死区圆
        var radius = (float)settings.DeadZoneRadius;
        var center = Map(frameW * 0.5f, frameH * 0.5f);
        var rScreen = radius * scale;
        _deadZoneCircle.Width = rScreen * 2;
        _deadZoneCircle.Height = rScreen * 2;
        Canvas.SetLeft(_deadZoneCircle, center.X - rScreen);
        Canvas.SetTop(_deadZoneCircle, center.Y - rScreen);
        _deadZoneCircle.Visibility = Visibility.Visible;

        // 平滑指针十字 + 手势文本
        var action = ViewModel.LastMouseAction;
        if (action is { ObservedGesture: not GestureKind.Idle })
        {
            var px = action.SmoothedNormalized.X * frameW;
            var py = action.SmoothedNormalized.Y * frameH;
            var tip = Map(px, py);
            const double arm = 12;
            _crossH.X1 = tip.X - arm;
            _crossH.Y1 = tip.Y;
            _crossH.X2 = tip.X + arm;
            _crossH.Y2 = tip.Y;
            _crossV.X1 = tip.X;
            _crossV.Y1 = tip.Y - arm;
            _crossV.X2 = tip.X;
            _crossV.Y2 = tip.Y + arm;
            _crossH.Visibility = Visibility.Visible;
            _crossV.Visibility = Visibility.Visible;

            _gestureLabel.Text = ViewModel.GestureState;
            Canvas.SetLeft(_gestureLabel, tip.X + 14);
            Canvas.SetTop(_gestureLabel, tip.Y - 10);
            _gestureLabel.Visibility = Visibility.Visible;
        }
        else
        {
            _crossH.Visibility = Visibility.Collapsed;
            _crossV.Visibility = Visibility.Collapsed;
            _gestureLabel.Visibility = Visibility.Collapsed;
        }

        if (result is null)
        {
            HideHandOverlay();
            return;
        }

        var palm = result.Palm;
        if (showBox && palm is not null)
        {
            if (result.Detected && result.Landmarks.Landmarks.Count >= 21)
            {
                DrawLandmarkHandBox(result.Landmarks.Landmarks, palm.RotationDegrees, Map);
                _confidenceText.Text = $"hand {result.Landmarks.Confidence:F2}";
            }
            else
            {
                _boxSmoothingValid = false;
                DrawRotatedRect(palm.X1, palm.Y1, palm.X2, palm.Y2, palm.RotationDegrees, Map);
                _confidenceText.Text = $"palm {palm.Score:F2}";
            }

            double anchorX = double.MaxValue, anchorY = double.MaxValue;
            foreach (var pt in _boundingBox.Points)
            {
                anchorX = Math.Min(anchorX, pt.X);
                anchorY = Math.Min(anchorY, pt.Y);
            }
            Canvas.SetLeft(_confidenceText, anchorX);
            Canvas.SetTop(_confidenceText, anchorY - 24);

            _boundingBox.Visibility = Visibility.Visible;
            _confidenceText.Visibility = Visibility.Visible;

            for (var i = 0; i < 7 && i < palm.Landmarks.Count; i++)
                PlaceDot(_palmDots[i], Map(palm.Landmarks[i].X, palm.Landmarks[i].Y));
        }
        else
        {
            _boxSmoothingValid = false;
            _boundingBox.Visibility = Visibility.Collapsed;
            _confidenceText.Visibility = Visibility.Collapsed;
            foreach (var dot in _palmDots)
                dot.Visibility = Visibility.Collapsed;
        }

        if (showSkeleton)
        {
            var landmarks = result.Landmarks.Landmarks;
            for (var i = 0; i < Bones.Length; i++)
            {
                var (a, b) = Bones[i];
                var pa = Map(landmarks[a].X, landmarks[a].Y);
                var pb = Map(landmarks[b].X, landmarks[b].Y);
                var line = _boneLines[i];
                line.X1 = pa.X;
                line.Y1 = pa.Y;
                line.X2 = pb.X;
                line.Y2 = pb.Y;
                line.Visibility = Visibility.Visible;
            }
            for (var i = 0; i < 21; i++)
                PlaceDot(_landmarkDots[i], Map(landmarks[i].X, landmarks[i].Y));
        }
        else
        {
            foreach (var line in _boneLines)
                line.Visibility = Visibility.Collapsed;
            foreach (var dot in _landmarkDots)
                dot.Visibility = Visibility.Collapsed;
        }
    }

    private int PreviewBitmapWidth() => ViewModel.PreviewBitmap?.PixelWidth ?? 0;

    private int PreviewBitmapHeight() => ViewModel.PreviewBitmap?.PixelHeight ?? 0;

    /// <summary>
    /// 将 21 关键点投影到手掌旋转坐标系,求带边距的 AABB,经 EMA 时间平滑后逆变换回图像坐标绘制整手框。
    /// </summary>
    private void DrawLandmarkHandBox(
        IReadOnlyList<LingXuZhi.Vision.Abstractions.Point2D> landmarks,
        float rotationDegrees,
        Func<float, float, Point> map)
    {
        double cx = 0, cy = 0;
        foreach (var p in landmarks)
        {
            cx += p.X;
            cy += p.Y;
        }
        cx /= landmarks.Count;
        cy /= landmarks.Count;

        // 与手掌竖直方向对齐的局部坐标(u,v)
        var rawRad = -rotationDegrees * Math.PI / 180.0;
        double rawCos = Math.Cos(rawRad), rawSin = Math.Sin(rawRad);

        double minU = double.MaxValue, minV = double.MaxValue;
        double maxU = double.MinValue, maxV = double.MinValue;
        foreach (var p in landmarks)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * rawCos + dy * rawSin;
            var v = -dx * rawSin + dy * rawCos;
            minU = Math.Min(minU, u);
            minV = Math.Min(minV, v);
            maxU = Math.Max(maxU, u);
            maxV = Math.Max(maxV, v);
        }

        var halfU = (maxU - minU) / 2;
        var halfV = (maxV - minV) / 2;
        halfU += Math.Max(halfU * 2 * HandBoxPadding, 8);
        halfV += Math.Max(halfV * 2 * HandBoxPadding, 8);

        // AABB 在局部坐标系可能不以 (cx,cy) 为中心,把框中心换算回图像坐标后再平滑
        var midU = (minU + maxU) / 2;
        var midV = (minV + maxV) / 2;
        var boxCx = cx + midU * rawCos - midV * rawSin;
        var boxCy = cy + midU * rawSin + midV * rawCos;

        if (!_boxSmoothingValid)
        {
            _smBoxCx = boxCx;
            _smBoxCy = boxCy;
            _smBoxHalfU = halfU;
            _smBoxHalfV = halfV;
            _smBoxAngle = rawRad;
            _boxSmoothingValid = true;
        }
        else
        {
            _smBoxCx += (boxCx - _smBoxCx) * BoxCenterAlpha;
            _smBoxCy += (boxCy - _smBoxCy) * BoxCenterAlpha;
            _smBoxHalfU += (halfU - _smBoxHalfU) * BoxSizeAlpha;
            _smBoxHalfV += (halfV - _smBoxHalfV) * BoxSizeAlpha;

            // 角度取最短差值平滑,避免 ±180° 跳变
            var delta = Math.IEEERemainder(rawRad - _smBoxAngle, 2 * Math.PI);
            _smBoxAngle += delta * BoxAngleAlpha;
        }

        double cos = Math.Cos(_smBoxAngle), sin = Math.Sin(_smBoxAngle);
        _boundingBox.Points.Clear();
        foreach (var (u, v) in new[]
                 {
                     (-_smBoxHalfU, -_smBoxHalfV), (_smBoxHalfU, -_smBoxHalfV),
                     (_smBoxHalfU, _smBoxHalfV), (-_smBoxHalfU, _smBoxHalfV),
                 })
        {
            var x = (float)(_smBoxCx + u * cos - v * sin);
            var y = (float)(_smBoxCy + u * sin + v * cos);
            _boundingBox.Points.Add(map(x, y));
        }
    }

    private void DrawRotatedRect(float x1, float y1, float x2, float y2, float rotationDegrees, Func<float, float, Point> map)
    {
        var cx = (x1 + x2) / 2f;
        var cy = (y1 + y2) / 2f;
        var rad = -rotationDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);

        _boundingBox.Points.Clear();
        foreach (var (dx, dy) in new[]
                 {
                     (x1 - cx, y1 - cy), (x2 - cx, y1 - cy),
                     (x2 - cx, y2 - cy), (x1 - cx, y2 - cy),
                 })
        {
            var rx = (float)(dx * cos - dy * sin) + cx;
            var ry = (float)(dx * sin + dy * cos) + cy;
            _boundingBox.Points.Add(map(rx, ry));
        }
    }

    private static void PlaceDot(Ellipse dot, Point center)
    {
        Canvas.SetLeft(dot, center.X - dot.Width / 2);
        Canvas.SetTop(dot, center.Y - dot.Height / 2);
        dot.Visibility = Visibility.Visible;
    }

    private void HideHandOverlay()
    {
        _boxSmoothingValid = false;
        _boundingBox.Visibility = Visibility.Collapsed;
        _confidenceText.Visibility = Visibility.Collapsed;
        foreach (var line in _boneLines)
            line.Visibility = Visibility.Collapsed;
        foreach (var dot in _palmDots)
            dot.Visibility = Visibility.Collapsed;
        foreach (var dot in _landmarkDots)
            dot.Visibility = Visibility.Collapsed;
    }

    private void HideAll()
    {
        HideHandOverlay();
        _deadZoneCircle.Visibility = Visibility.Collapsed;
        _crossH.Visibility = Visibility.Collapsed;
        _crossV.Visibility = Visibility.Collapsed;
        _gestureLabel.Visibility = Visibility.Collapsed;
    }
}
