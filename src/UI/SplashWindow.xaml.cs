using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mixline.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        try
        {
            Bar.Background = (Brush)FindResource("AccentBrush");
        }
        catch
        {
        }
    }

    public void Tick(string line, double amount)
    {
        Line.Text = line;
        var max = ActualWidth > 80 ? ActualWidth - 44 : 296;
        Bar.Width = Math.Clamp(amount, 0.08, 1) * max;
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }
}
