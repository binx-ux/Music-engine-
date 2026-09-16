using System.Windows.Controls;
using Mixline.Audio.Mixer;

namespace Mixline.App.Views;

public partial class HomeView : UserControl
{
    private AppSession? _session;
    private bool _bound;

    public HomeView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            _mixerStrip.Bind(session);
            _miniBoard.Bind(session);
            session.Changed += () => Dispatcher.BeginInvoke(Refresh);
        }
        Refresh();
    }

    public void Refresh()
    {
        if (_session is null) return;
        MicName.Text = _session.Engine.InputName ?? "No microphone";
        OutName.Text = _session.Engine.OutputName ?? "No headphones";
        EngineState.Text = _session.Engine.IsRunning ? "Running" : "Stopped";
        Notice.Text = _session.LastError ?? "";
        var virt = _session.Engine.VirtualStatus();
        VirtHint.Text = string.IsNullOrWhiteSpace(virt.Hint)
            ? virt.Message
            : virt.Message + Environment.NewLine + virt.Hint;
        _mixerStrip.Load();
    }

    public void UpdateMeters(MeterState meters)
    {
        _mixerStrip.UpdateMeters(meters);
        MicMeter.Level = meters.Mic;
        MicMeter.Hold = meters.MicHold;
        MusicMeter.Level = meters.Music;
        MusicMeter.Hold = meters.MusicHold;
        BoardMeter.Level = meters.Soundboard;
        BoardMeter.Hold = meters.SoundboardHold;
        MasterMeter.Level = meters.Master;
        MasterMeter.Hold = meters.MasterHold;
    }
}
