using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mixline.App;

public static class UiMotion
{
    private static IEasingFunction Out() => new QuarticEase { EasingMode = EasingMode.EaseOut };
    private static IEasingFunction Back() => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

    public static void Enter(UIElement el, TranslateTransform slide)
    {
        el.BeginAnimation(UIElement.OpacityProperty, Anim(0, 1, 180));
        slide.BeginAnimation(TranslateTransform.XProperty, Anim(18, 0, 220, Out()));
        slide.BeginAnimation(TranslateTransform.YProperty, Anim(4, 0, 200, Out()));
    }

    public static void Fade(UIElement el, double from, double to, int ms, Action? done = null)
    {
        var a = Anim(from, to, ms);
        if (done is not null)
            a.Completed += (_, _) => done();
        el.BeginAnimation(UIElement.OpacityProperty, a);
    }

    public static void WidthTo(FrameworkElement el, double to, int ms)
    {
        var from = el.Width;
        if (double.IsNaN(from) || from < 0)
            from = el.ActualWidth;
        el.BeginAnimation(FrameworkElement.WidthProperty, Anim(from, to, ms, Out()));
    }

    public static void ScaleX(ScaleTransform sc, double to, int ms)
    {
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(sc.ScaleX, to, ms, Out()));
    }

    public static void ScaleTo(ScaleTransform sc, double from, double to, int ms)
    {
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(from, to, ms, Back()));
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(from, to, ms, Back()));
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
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(1.08, 1, 140, Back()));
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1.08, 1, 140, Back()));
        };
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, up);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(1, 1.08, 70));
    }

    private static DoubleAnimation Anim(double from, double to, int ms, IEasingFunction? ease = null)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = ease ?? Out(),
            FillBehavior = FillBehavior.HoldEnd
        };
    }
}
