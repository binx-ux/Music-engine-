using System.Windows;
using System.Windows.Controls;
using Mixline.Audio.Mixer;

namespace Mixline.App.Views;

public partial class MixerStrip : UserControl
{
    private AppSession? _session;
    private bool _suppress;

    public MixerStrip() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        Load();
    }

    public void Load()
    {
        if (_session is null) return;
        _suppress = true;
        var m = _session.Config.Mixer;
        MicVol.Value = m.Mic.Volume;
        MusicVol.Value = m.Music.Volume;
        BoardVol.Value = m.Soundboard.Volume;
        MasterVol.Value = m.Master.Volume;
        MicMute.IsChecked = m.Mic.Mute;
        MusicMute.IsChecked = m.Music.Mute;
        BoardMute.IsChecked = m.Soundboard.Mute;
        MasterMute.IsChecked = m.Master.Mute;
        MicSolo.IsChecked = m.Mic.Solo;
        MusicSolo.IsChecked = m.Music.Solo;
        BoardSolo.IsChecked = m.Soundboard.Solo;
        _suppress = false;
    }

    public void UpdateMeters(MeterState meters)
    {
        MicMeter.Level = meters.Mic;
        MicMeter.Hold = meters.MicHold;
        MusicMeter.Level = meters.Music;
        MusicMeter.Hold = meters.MusicHold;
        BoardMeter.Level = meters.Soundboard;
        BoardMeter.Hold = meters.SoundboardHold;
        MasterMeter.Level = meters.Master;
        MasterMeter.Hold = meters.MasterHold;
    }

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => Changed(sender, e);

    private void Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var m = _session.Config.Mixer;
        m.Mic.Volume = (float)MicVol.Value;
        m.Music.Volume = (float)MusicVol.Value;
        m.Soundboard.Volume = (float)BoardVol.Value;
        m.Master.Volume = (float)MasterVol.Value;
        m.Mic.Mute = MicMute.IsChecked == true;
        m.Music.Mute = MusicMute.IsChecked == true;
        m.Soundboard.Mute = BoardMute.IsChecked == true;
        m.Master.Mute = MasterMute.IsChecked == true;
        m.Mic.Solo = MicSolo.IsChecked == true;
        m.Music.Solo = MusicSolo.IsChecked == true;
        m.Soundboard.Solo = BoardSolo.IsChecked == true;
        _session.MixerChanged();
    }
}
