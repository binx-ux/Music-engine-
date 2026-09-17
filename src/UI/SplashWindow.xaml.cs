using System.Windows;
using Mixline.Core;

namespace Mixline.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opacity = 0;
        Ver.Text = AppInfo.Version;
        Loaded += (_, _) =>
        {
            UiMotion.Fade(this, 0, 1, 200);
            UiMotion.ScaleTo(MarkScale, 0.88, 1, 280);
        };
    }

    public async Task Step(string line, double amount)
    {
        Line.Text = line;
        UiMotion.ScaleX(FillScale, Math.Clamp(amount, 0.08, 1), 320);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        await Task.Delay(70);
    }

    public async Task Finish()
    {
        Line.Text = "Ready";
        UiMotion.ScaleX(FillScale, 1, 180);
        await Task.Delay(140);
        var done = new TaskCompletionSource();
        UiMotion.Fade(this, Opacity, 0, 180, () => done.TrySetResult());
        await Task.WhenAny(done.Task, Task.Delay(400));
        try { Close(); } catch { }
    }
}
