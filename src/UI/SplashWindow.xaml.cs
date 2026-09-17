using System.Windows;
using Mixline.Core;

namespace Mixline.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opacity = 0;
        Ver.Text = "v" + AppInfo.Version;
        Loaded += (_, _) =>
        {
            UiMotion.Fade(this, 0, 1, 240);
            UiMotion.ScaleTo(MarkScale, 0.82, 1, 340);
        };
    }

    public async Task Step(string line, double amount)
    {
        Line.Text = line;
        amount = Math.Clamp(amount, 0.06, 1);
        UiMotion.ScaleX(FillScale, amount, 340);
        UiMotion.ScaleY(Meter1, Math.Clamp(amount / 0.42, 0.14, 1), 260);
        UiMotion.ScaleY(Meter2, Math.Clamp((amount - 0.12) / 0.55, 0.10, 1), 320);
        UiMotion.ScaleY(Meter3, Math.Clamp((amount - 0.28) / 0.62, 0.08, 1), 380);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        await Task.Delay(90);
    }

    public async Task Finish()
    {
        Line.Text = "Ready";
        UiMotion.ScaleX(FillScale, 1, 200);
        UiMotion.ScaleY(Meter1, 1, 180);
        UiMotion.ScaleY(Meter2, 1, 220);
        UiMotion.ScaleY(Meter3, 1, 260);
        await Task.Delay(180);
        var done = new TaskCompletionSource();
        UiMotion.Fade(this, Opacity, 0, 200, () => done.TrySetResult());
        await Task.WhenAny(done.Task, Task.Delay(420));
        try { Close(); } catch { }
    }
}
