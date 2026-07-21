using Autofac;
using LingXuZhi.App.Services;
using LingXuZhi.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

    private readonly Line[] _boneLines = new Line[Bones.Length];
    private readonly Ellipse[] _landmarkDots = new Ellipse[21];
    private readonly Ellipse[] _palmDots = new Ellipse[7];
    private readonly Polygon _boundingBox;
    private readonly TextBlock _confidenceText;

    public MainViewModel ViewModel { get; }

    public CameraPreviewControl()
    {
        ViewModel = App.Container.Resolve<MainViewModel>();
        InitializeComponent();

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

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.OverlayResult))
                UpdateOverlay();
        };
        ViewModel.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LingXuZhi.Core.Configuration.AppSettings.DrawSkeleton)
                or nameof(LingXuZhi.Core.Configuration.AppSettings.DrawBoundingBox))
                UpdateOverlay();
        };
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => UpdateOverlay();

    private void UpdateOverlay()
    {
        var result = ViewModel.OverlayResult;
        var settings = ViewModel.Settings;
        var showSkeleton = result is { Detected: true } && settings.DrawSkeleton;
        var showBox = result?.Palm is not null && settings.DrawBoundingBox;

        if (result is null || (!showSkeleton && !showBox))
        {
            HideAll();
            return;
        }

        // Stretch=Uniform 的显示映射:等比缩放 + 居中偏移
        double cw = OverlayCanvas.ActualWidth, ch = OverlayCanvas.ActualHeight;
        if (cw <= 0 || ch <= 0 || result.FrameWidth <= 0)
        {
            HideAll();
            return;
        }
        var scale = Math.Min(cw / result.FrameWidth, ch / result.FrameHeight);
        var ox = (cw - result.FrameWidth * scale) / 2;
        var oy = (ch - result.FrameHeight * scale) / 2;
        Point Map(float x, float y) => new(x * scale + ox, y * scale + oy);

        var palm = result.Palm;
        if (showBox && palm is not null)
        {
            // 有 21 点时用整手包围框(含手指);否则回退到手掌检测框
            if (result.Detected && result.Landmarks.Landmarks.Count >= 21)
            {
                DrawLandmarkHandBox(result.Landmarks.Landmarks, palm.RotationDegrees, Map);
                _confidenceText.Text = $"hand {result.Landmarks.Confidence:F2}";
                var (ax, ay) = LandmarkTopLeft(result.Landmarks.Landmarks);
                var anchor = Map(ax, ay);
                Canvas.SetLeft(_confidenceText, anchor.X);
                Canvas.SetTop(_confidenceText, anchor.Y - 24);
            }
            else
            {
                DrawRotatedRect(palm.X1, palm.Y1, palm.X2, palm.Y2, palm.RotationDegrees, Map);
                _confidenceText.Text = $"palm {palm.Score:F2}";
                var anchor = Map(Math.Min(palm.X1, palm.X2), Math.Min(palm.Y1, palm.Y2));
                Canvas.SetLeft(_confidenceText, anchor.X);
                Canvas.SetTop(_confidenceText, anchor.Y - 24);
            }

            _boundingBox.Visibility = Visibility.Visible;
            _confidenceText.Visibility = Visibility.Visible;

            for (var i = 0; i < 7 && i < palm.Landmarks.Count; i++)
                PlaceDot(_palmDots[i], Map(palm.Landmarks[i].X, palm.Landmarks[i].Y));
        }
        else
        {
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

    /// <summary>
    /// 将 21 关键点投影到手掌旋转坐标系,求带边距的 AABB,再逆变换回图像坐标绘制整手框。
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
        var rad = -rotationDegrees * Math.PI / 180.0;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);

        double minU = double.MaxValue, minV = double.MaxValue;
        double maxU = double.MinValue, maxV = double.MinValue;
        foreach (var p in landmarks)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var u = dx * cos + dy * sin;
            var v = -dx * sin + dy * cos;
            minU = Math.Min(minU, u);
            minV = Math.Min(minV, v);
            maxU = Math.Max(maxU, u);
            maxV = Math.Max(maxV, v);
        }

        var padU = Math.Max((maxU - minU) * HandBoxPadding, 8);
        var padV = Math.Max((maxV - minV) * HandBoxPadding, 8);
        minU -= padU;
        maxU += padU;
        minV -= padV;
        maxV += padV;

        // 局部四角逆旋转回图像坐标
        _boundingBox.Points.Clear();
        foreach (var (u, v) in new[] { (minU, minV), (maxU, minV), (maxU, maxV), (minU, maxV) })
        {
            var x = (float)(cx + u * cos - v * sin);
            var y = (float)(cy + u * sin + v * cos);
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

    private static (float X, float Y) LandmarkTopLeft(IReadOnlyList<LingXuZhi.Vision.Abstractions.Point2D> landmarks)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (var p in landmarks)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
        }
        return (minX, minY);
    }

    private static void PlaceDot(Ellipse dot, Point center)
    {
        Canvas.SetLeft(dot, center.X - dot.Width / 2);
        Canvas.SetTop(dot, center.Y - dot.Height / 2);
        dot.Visibility = Visibility.Visible;
    }

    private void HideAll()
    {
        _boundingBox.Visibility = Visibility.Collapsed;
        _confidenceText.Visibility = Visibility.Collapsed;
        foreach (var line in _boneLines)
            line.Visibility = Visibility.Collapsed;
        foreach (var dot in _palmDots)
            dot.Visibility = Visibility.Collapsed;
        foreach (var dot in _landmarkDots)
            dot.Visibility = Visibility.Collapsed;
    }
}
