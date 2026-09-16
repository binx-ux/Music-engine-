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
        box.ItemsSource = items;
        box.SelectedItem = items.FirstOrDefault(i => i.Id == selected) ?? items.FirstOrDefault(i => i.IsDefault);
    }

    private void Apply(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _session is null) return;
        _session.Config.Devices.InputId = (Inputs.SelectedItem as AudioDeviceInfo)?.Id;
        _session.Config.Devices.OutputId = (Outputs.SelectedItem as AudioDeviceInfo)?.Id;
        _session.Config.Devices.VirtualOutputId = (Virtuals.SelectedItem as AudioDeviceInfo)?.Id;
        _session.ScheduleSave();
        Hint.Text = _session.Engine.VirtualStatus().Hint ?? "";
    }

    private void Restart(object sender, RoutedEventArgs e)
    {
        _session?.RestartEngine();
        Reload();
    }
}
