using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Mixline.Soundboard;

namespace Mixline.App.Views;

public partial class SoundboardView : UserControl
{
    private AppSession? _session;
    private SoundPad? _selected;
    private bool _suppress;
    private bool _bound;
    private Point _press;
    private bool _dragArmed;
    private string _folder = "All";

    public SoundboardView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            session.LayoutChanged += () => Dispatcher.BeginInvoke(Rebuild);
            session.PadNamesChanged += () => Dispatcher.BeginInvoke(SyncNames);
        }
        Rebuild();
    }

    private void Rebuild()
    {
        if (_session is null) return;
        _suppress = true;
        Master.Value = _session.Config.Soundboard.MasterVolume;
        var folders = _session.Layout.Pads
            .Select(p => string.IsNullOrWhiteSpace(p.Folder) ? "General" : p.Folder)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        folders.Insert(0, "All");
        FolderBox.Items.Clear();
        foreach (var folder in folders)
            FolderBox.Items.Add(folder);
        if (!folders.Contains(_folder))
            _folder = "All";
        FolderBox.SelectedItem = _folder;
        GridPads.Items.Clear();
        var pads = _session.Layout.Pads.OrderBy(p => p.Order).AsEnumerable();
        if (_folder != "All")
            pads = pads.Where(p => (string.IsNullOrWhiteSpace(p.Folder) ? "General" : p.Folder) == _folder);
        foreach (var pad in pads)
        {
            var btn = new Button
            {
                Content = pad.Name,
                Tag = pad,
                Style = (Style)FindResource("PadBtn"),
                AllowDrop = true
            };
            btn.Click += (_, _) => Select(pad);
            btn.MouseDoubleClick += async (_, _) => await _session.PlayPad(pad);
            btn.PreviewMouseLeftButtonDown += PadPress;
            btn.PreviewMouseMove += PadDrag;
            btn.Drop += PadDrop;
            GridPads.Items.Add(btn);
        }
        _suppress = false;
        if (_selected is not null)
            Select(_selected);
    }

    private void SyncNames()
    {
        foreach (Button btn in GridPads.Items)
        {
            if (btn.Tag is SoundPad pad)
                btn.Content = pad.Name;
        }
    }

    private void Select(SoundPad pad)
    {
        _selected = pad;
        _suppress = true;
        NameBox.Text = pad.Name;
        FileBox.Text = pad.FilePath ?? "";
        Vol.Value = pad.Volume;
        Pitch.Value = pad.Pitch;
        Speed.Value = pad.Speed;
        Loop.IsChecked = pad.Loop;
        FadeIn.Value = pad.FadeIn;
        FadeOut.Value = pad.FadeOut;
        HotkeyBox.Text = pad.Hotkey ?? "";
        _suppress = false;
    }

    private void FolderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || FolderBox.SelectedItem is not string folder)
            return;
        _folder = folder;
        Rebuild();
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var pad = new SoundPad { Name = "New sound", Order = _session.Layout.Pads.Count };
        _session.Layout.Pads.Add(pad);
        _session.ScheduleSave();
        _selected = pad;
        _session.RaiseLayout();
    }

    private async void LoadPresets(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        try
        {
            var n = await InstantPresets.InstallAsync(_session.Layout, CancellationToken.None);
            _session.SoundboardStore.Save(_session.Layout);
            _session.RaiseLayout();
            _session.Notify(n > 0 ? $"Added {n} Myinstants sounds." : "Presets already on the board.");
        }
        catch (Exception ex)
        {
            _session.Notify("Could not load presets. " + ex.Message);
        }
    }

    private void StopAll(object sender, RoutedEventArgs e) => _session?.Engine.StopAllSounds();

    private void MasterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || _session is null) return;
        _session.Config.Soundboard.MasterVolume = (float)Master.Value;
        _session.ScheduleSave();
    }

    private void PadSlider(object sender, RoutedPropertyChangedEventArgs<double> e) => PadChanged(sender, e);
    private void PadText(object sender, TextChangedEventArgs e) => PadChanged(sender, e);

    private void PadChanged(object sender, RoutedEventArgs e)
    {
        if (_suppress || _selected is null || _session is null) return;
        _selected.Name = NameBox.Text;
        _selected.Volume = (float)Vol.Value;
        _selected.Pitch = (float)Pitch.Value;
        _selected.Speed = (float)Speed.Value;
        _selected.Loop = Loop.IsChecked == true;
        _selected.FadeIn = (float)FadeIn.Value;
        _selected.FadeOut = (float)FadeOut.Value;
        _selected.Hotkey = HotkeyBox.Text;
        _session.ScheduleSave();
        if (sender == HotkeyBox)
            _session.RegisterHotkeys();
        if (sender == NameBox)
            _session.RaisePadNames();
        else
            SyncNames();
    }

    private void ChooseFile(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var dlg = new OpenFileDialog
        {
            Filter = "Audio|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.aac;*.wma|All|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _selected.FilePath = dlg.FileName;
            FileBox.Text = dlg.FileName;
            if (_selected.Name is "New sound" or "Sound")
                _selected.Name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            NameBox.Text = _selected.Name;
            _session?.ScheduleSave();
            _session?.RaiseLayout();
        }
    }

    private async void PlaySelected(object sender, RoutedEventArgs e)
    {
        if (_session is null || _selected is null) return;
        await _session.PlayPad(_selected);
    }

    private void PadPress(object sender, MouseButtonEventArgs e)
    {
        _press = e.GetPosition((IInputElement)sender);
        _dragArmed = false;
    }

    private void PadDrag(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragArmed)
            return;
        if (sender is not Button btn)
            return;
        var now = e.GetPosition(btn);
        if (Math.Abs(now.X - _press.X) < 8 && Math.Abs(now.Y - _press.Y) < 8)
            return;
        _dragArmed = true;
        DragDrop.DoDragDrop(btn, btn.Tag, DragDropEffects.Move);
    }

    private void PadDrop(object sender, DragEventArgs e)
    {
        if (_session is null) return;
        if (sender is not Button target || target.Tag is not SoundPad to)
            return;
        if (e.Data.GetData(typeof(SoundPad)) is not SoundPad from)
            return;
        var list = _session.Layout.Pads;
        var a = list.IndexOf(from);
        var b = list.IndexOf(to);
        if (a < 0 || b < 0 || a == b) return;
        list.RemoveAt(a);
        list.Insert(b, from);
        for (var i = 0; i < list.Count; i++)
            list[i].Order = i;
        _session.ScheduleSave();
        _session.RaiseLayout();
    }
}
