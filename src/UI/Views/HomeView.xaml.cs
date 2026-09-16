using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        var running = _session.Engine.IsRunning;
        MicName.Text = _session.Engine.InputName ?? "No microphone";
        OutName.Text = _session.Engine.OutputName ?? "No headphones";
        EngineState.Text = running ? "Running" : "Stopped";
        EnginePill.Background = running
            ? (Brush)FindResource("AccentGhostBrush")
            : (Brush)FindResource("MutedBrush");
        EnginePill.Opacity = running ? 1 : 0.25;
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

    private async void CleanRap(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var result = await _session.FindCleanRap();
        if (!result.Ok)
        {
            _session.Notify(result.Message);
            return;
        }
        var start = _session.Engine.Music.Queue.Count;
        foreach (var t in result.Tracks)
            _session.Engine.Music.Add(t);
        _session.Config.Music.Queue = _session.Engine.Music.Queue.Select(t => t.Path).ToList();
        _session.ScheduleSave();
        if (result.Tracks.Count > 0)
            _session.Engine.Music.PlayIndex(start);
        _session.Notify(result.Message);
    }
}
