using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mixline.Audio.Devices;
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

    private void GoMusic(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
            main.OpenMusic();
    }

    private void UseForGames(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var pick = VirtualDeviceCatalog.PreferredVirtualRender(_session.Engine.Devices.RenderDevices());
        if (pick is null)
        {
            _session.Notify("Install VB-Audio Cable first, then restart Cuebox.");
            VirtualDeviceCatalog.OpenCableDownload();
            return;
        }
        _session.Config.Devices.VirtualOutputId = pick.Id;
        _session.Config.Devices.SetWindowsDefaultMic = true;
        _session.ScheduleSave();
        _session.RestartEngine();
        Refresh();
    }

    private void GetCable(object sender, RoutedEventArgs e)
        => VirtualDeviceCatalog.OpenCableDownload();
}
