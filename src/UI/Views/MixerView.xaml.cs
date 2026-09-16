using System.Windows;
using System.Windows.Controls;
using Mixline.Audio.Mixer;

namespace Mixline.App.Views;

public partial class MixerView : UserControl
{
    private AppSession? _session;
    private bool _suppress;
    private bool _bound;

    public MixerView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            Strip.Bind(session);
            session.Changed += () => Dispatcher.BeginInvoke(Load);
        }
        Load();
    }

    public void UpdateMeters(MeterState meters) => Strip.UpdateMeters(meters);

    private void Load()
    {
        if (_session is null) return;
        _suppress = true;
        var m = _session.Config.Mixer;
        MicMon.IsChecked = m.MicMonitor;
        MusicMon.IsChecked = m.MusicMonitor;
        BoardMon.IsChecked = m.SoundboardMonitor;
        MasterMon.IsChecked = m.MasterMonitor;
        MonVol.Value = m.MonitorVolume;
        MicVirt.Value = m.Mic.VirtualSend;
        MusicVirt.Value = m.Music.VirtualSend;
        BoardVirt.Value = m.Soundboard.VirtualSend;
        Strip.Load();
        _suppress = false;
    }

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => SendsChanged(sender, e);

    private void SendsChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var m = _session.Config.Mixer;
        m.MicMonitor = MicMon.IsChecked == true;
        m.MusicMonitor = MusicMon.IsChecked == true;
        m.SoundboardMonitor = BoardMon.IsChecked == true;
        m.MasterMonitor = MasterMon.IsChecked == true;
        m.MonitorVolume = (float)MonVol.Value;
        m.Mic.VirtualSend = (float)MicVirt.Value;
        m.Music.VirtualSend = (float)MusicVirt.Value;
        m.Soundboard.VirtualSend = (float)BoardVirt.Value;
        _session.MixerChanged();
    }
}
