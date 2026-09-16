using System.Windows;
using System.Windows.Media;

namespace Mixline.App.Controls;

public sealed class LevelMeter : FrameworkElement
{
    private static readonly SolidColorBrush WellFill = Freeze(Color.FromArgb(90, 0, 0, 0));
    private static readonly Pen WellPen = FreezePen(Color.FromArgb(40, 255, 255, 255));
    private static readonly SolidColorBrush HoldBrush = Freeze(Color.FromRgb(236, 228, 214));
    private static readonly LinearGradientBrush FillH = MakeFill(false);
    private static readonly LinearGradientBrush FillV = MakeFill(true);

    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoldProperty = DependencyProperty.Register(
        nameof(Hold), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalProperty = DependencyProperty.Register(
        nameof(Vertical), typeof(bool), typeof(LevelMeter),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private RectangleGeometry? _clip;
    private double _clipW, _clipH, _clipR;

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set
        {
            if (Math.Abs(Level - value) < 0.0008)
                return;
            SetValue(LevelProperty, value);
        }
    }

    public double Hold
    {
        get => (double)GetValue(HoldProperty);
        set
        {
            if (Math.Abs(Hold - value) < 0.0008)
                return;
            SetValue(HoldProperty, value);
        }
    }

    public bool Vertical
    {
        get => (bool)GetValue(VerticalProperty);
        set => SetValue(VerticalProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        var radius = Vertical ? Math.Min(4, w / 2) : Math.Min(4, h / 2);
        var well = new Rect(0, 0, w, h);
        dc.DrawRoundedRectangle(WellFill, WellPen, well, radius, radius);

        var fill = MeterAmount(Level);
        var hold = MeterAmount(Hold);
        if (fill <= 0.001 && hold <= 0.001)
            return;

        if (_clip is null || _clipW != w || _clipH != h || _clipR != radius)
        {
            _clip = new RectangleGeometry(well, radius, radius);
            _clip.Freeze();
            _clipW = w;
            _clipH = h;
            _clipR = radius;
        }

        dc.PushClip(_clip);
        if (Vertical)
        {
            var fh = h * fill;
            dc.DrawRectangle(FillV, null, new Rect(1, h - fh, w - 2, fh));
            if (hold > fill)
            {
                var y = h - (h * hold);
                dc.DrawRectangle(HoldBrush, null, new Rect(1, y, w - 2, 1.5));
            }
        }
        else
        {
            var fw = w * fill;
            dc.DrawRectangle(FillH, null, new Rect(0, 1, fw, h - 2));
            if (hold > fill)
            {
                var x = w * hold;
                dc.DrawRectangle(HoldBrush, null, new Rect(x, 1, 1.5, h - 2));
            }
        }
        dc.Pop();
    }

    private static double MeterAmount(double linear)
    {
        if (linear <= 0.0005)
            return 0;
        var db = 20.0 * Math.Log10(Math.Clamp(linear, 1e-6, 2));
        return Math.Clamp((db + 60.0) / 60.0, 0, 1);
    }

    private static LinearGradientBrush MakeFill(bool vertical)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = vertical ? new Point(0, 1) : new Point(0, 0),
            EndPoint = vertical ? new Point(0, 0) : new Point(1, 0)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(110, 168, 132), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(201, 163, 106), 0.72));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(196, 92, 86), 1));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Color color)
    {
        var pen = new Pen(Freeze(color), 1);
        pen.Freeze();
        return pen;
    }
}
