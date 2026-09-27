using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mixline.App;

public static class UiMotion
{
    private static IEasingFunction Out() => new QuarticEase { EasingMode = EasingMode.EaseOut };
    private static IEasingFunction Back() => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

    public static void Enter(UIElement el, TranslateTransform slide)
    {
        el.BeginAnimation(UIElement.OpacityProperty, null);
        slide.BeginAnimation(TranslateTransform.XProperty, null);
        slide.BeginAnimation(TranslateTransform.YProperty, null);
        el.Opacity = 1;
        slide.X = 0;
        slide.Y = 0;
    }

    public static void Fade(UIElement el, double from, double to, int ms, Action? done = null)
    {
        var a = Anim(from, to, ms);
        a.Completed += (_, _) =>
        {
            el.BeginAnimation(UIElement.OpacityProperty, null);
            el.Opacity = to;
            done?.Invoke();
        };
        el.Opacity = from;
        el.BeginAnimation(UIElement.OpacityProperty, a);
    }

    public static void SetSlider(Slider slider, double value)
    {
        if (slider.IsMouseCaptureWithin)
            return;
        if (!double.IsNaN(slider.Value) && Math.Abs(slider.Value - value) < 0.0008)
            return;
        slider.Value = value;
    }

    public static void WidthTo(FrameworkElement el, double to, int ms)
    {
        var from = el.Width;
        if (double.IsNaN(from) || from < 0)
            from = el.ActualWidth;
        var a = Anim(from, to, ms, Out());
        a.Completed += (_, _) =>
        {
            el.BeginAnimation(FrameworkElement.WidthProperty, null);
            el.Width = to;
        };
        el.BeginAnimation(FrameworkElement.WidthProperty, a);
    }

    public static void ScaleX(ScaleTransform sc, double to, int ms)
    {
        var a = Anim(sc.ScaleX, to, ms, Out());
        a.Completed += (_, _) =>
        {
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            sc.ScaleX = to;
        };
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
    }

    public static void ScaleY(ScaleTransform sc, double to, int ms)
    {
        var a = Anim(sc.ScaleY, to, ms, Out());
        a.Completed += (_, _) =>
        {
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            sc.ScaleY = to;
        };
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    public static void ScaleTo(ScaleTransform sc, double from, double to, int ms)
    {
        var x = Anim(from, to, ms, Back());
        var y = Anim(from, to, ms, Back());
        x.Completed += (_, _) =>
        {
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            sc.ScaleX = to;
        };
        y.Completed += (_, _) =>
        {
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            sc.ScaleY = to;
        };
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, x);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, y);
    }

    public static void Punch(UIElement el)
    {
        if (el.RenderTransform is not ScaleTransform sc)
        {
            sc = new ScaleTransform(1, 1);
            el.RenderTransform = sc;
            el.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        var up = Anim(1, 1.08, 70);
        up.Completed += (_, _) =>
        {
            var downX = Anim(1.08, 1, 120, Back());
            var downY = Anim(1.08, 1, 120, Back());
            downX.Completed += (_, _) =>
            {
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                sc.ScaleX = 1;
            };
            downY.Completed += (_, _) =>
            {
                sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                sc.ScaleY = 1;
            };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, downX);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, downY);
        };
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, up);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1, 1.08, 70));
    }

    private static DoubleAnimation Anim(double from, double to, int ms, IEasingFunction? ease = null)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = ease ?? Out(),
            FillBehavior = FillBehavior.Stop
        };
    }
}
