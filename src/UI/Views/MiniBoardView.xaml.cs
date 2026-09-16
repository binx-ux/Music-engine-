using System.Windows;
using System.Windows.Controls;
using Mixline.Soundboard;

namespace Mixline.App.Views;

public partial class MiniBoardView : UserControl
{
    private AppSession? _session;
    private bool _bound;

    public MiniBoardView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        if (_bound)
        {
            Rebuild();
            return;
        }
        _bound = true;
        _session = session;
        Rebuild();
        session.LayoutChanged += () => Dispatcher.BeginInvoke(Rebuild);
        session.PadNamesChanged += () => Dispatcher.BeginInvoke(SyncNames);
    }

    private void Rebuild()
    {
        if (_session is null) return;
        Pads.Children.Clear();
        foreach (var pad in _session.Layout.Pads.OrderBy(p => p.Order).Take(8))
        {
            var btn = new Button
            {
                Content = pad.Name,
                Style = (Style)FindResource("PadBtn"),
                Width = 108,
                Height = 56,
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 8),
                Tag = pad
            };
            btn.Click += async (_, _) => await _session.PlayPad((SoundPad)btn.Tag);
            Pads.Children.Add(btn);
        }
    }

    private void SyncNames()
    {
        foreach (Button btn in Pads.Children)
        {
            if (btn.Tag is SoundPad pad)
                btn.Content = pad.Name;
        }
    }
}
