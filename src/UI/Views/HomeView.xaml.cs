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
            Refresh();
        }
    }

    public void Refresh()
    {
        if (_session is null)
            return;

        var running = _session.Engine.IsRunning;
        var mic = _session.Engine.InputName;
        if (string.IsNullOrWhiteSpace(mic))
            mic = "No microphone";
        MicName.Text = mic;

        var outs = _session.Engine.OutputName;
        if (string.IsNullOrWhiteSpace(outs))
            outs = "No headphones";
        OutName.Text = outs;

        if (running)
            EngineState.Text = "Running";
        else
            EngineState.Text = "Stopped";

        if (running)
        {
            EnginePill.Background = (Brush)FindResource("AccentGhostBrush");
            EnginePill.Opacity = 1;
        }
        else
        {
            EnginePill.Background = (Brush)FindResource("MutedBrush");
            EnginePill.Opacity = 0.25;
        }

        var err = _session.LastError;
        if (err == null)
            err = "";
        Notice.Text = err;

        var virt = _session.Engine.VirtualStatus();
        if (string.IsNullOrWhiteSpace(virt.Hint))
            VirtHint.Text = virt.Message;
        else
            VirtHint.Text = virt.Message + Environment.NewLine + virt.Hint;

        _mixerStrip.Load();
    }

    public void UpdateMeters(MeterState meters)
    {
        // home meters + the strip under them, both need the same snapshot
        if (_mixerStrip != null)
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
