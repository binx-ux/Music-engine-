using System.Windows;
using System.Windows.Controls;
using Mixline.Audio.Devices;

namespace Mixline.App.Views;

public partial class DevicesView : UserControl
{
    private AppSession? _session;
    private bool _suppress;
    private bool _bound;

    public DevicesView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            session.Engine.Devices.DevicesChanged += (_, _) => Dispatcher.BeginInvoke(Reload);
            session.Changed += () => Dispatcher.BeginInvoke(Reload);
        }
        Reload();
    }

    private void Refresh(object sender, RoutedEventArgs e) => Reload();

    private void Reload()
    {
        if (_session is null) return;
        _suppress = true;
        Fill(Inputs, _session.Engine.Devices.CaptureDevices(), _session.Config.Devices.InputId);
        var renders = _session.Engine.Devices.RenderDevices();
        Fill(Outputs, renders, _session.Config.Devices.OutputId);
        Fill(Virtuals, renders.Where(d => d.IsVirtualCandidate).ToList(), _session.Config.Devices.VirtualOutputId);
        Hint.Text = _session.Engine.VirtualStatus().Hint ?? "";
        _suppress = false;
    }

    private static void Fill(ComboBox box, IReadOnlyList<AudioDeviceInfo> items, string? selected)
    {
        box.DisplayMemberPath = nameof(AudioDeviceInfo.Name);
        box.SelectedValuePath = nameof(AudioDeviceInfo.Id);
        box.ItemsSource = items;
        var pick = items.FirstOrDefault(i => i.Id == selected)
            ?? items.FirstOrDefault(i => i.IsDefault)
            ?? items.FirstOrDefault();
        if (pick is null)
        {
            box.SelectedIndex = -1;
            return;
        }
        box.SelectedItem = pick;
        box.SelectedValue = pick.Id;
    }

    private void Apply(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var nextIn = (Inputs.SelectedItem as AudioDeviceInfo)?.Id;
        var nextOut = (Outputs.SelectedItem as AudioDeviceInfo)?.Id;
        var nextVirt = (Virtuals.SelectedItem as AudioDeviceInfo)?.Id;
        var cfg = _session.Config.Devices;
        var changed = nextIn != cfg.InputId || nextOut != cfg.OutputId || nextVirt != cfg.VirtualOutputId;
        cfg.InputId = nextIn;
        cfg.OutputId = nextOut;
        cfg.VirtualOutputId = nextVirt;
        _session.ScheduleSave();
        Hint.Text = _session.Engine.VirtualStatus().Hint ?? "";
        if (changed)
            _session.RestartEngine();
    }

    private void Restart(object sender, RoutedEventArgs e)
    {
        _session?.RestartEngine();
        Reload();
    }
}
