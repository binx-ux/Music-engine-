using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Mixline.Core;
using Mixline.Spotify;

namespace Mixline.App.Views;

public partial class SettingsView : UserControl
{
    private AppSession? _session;
    private bool _suppress;
    private bool _bound;

    public SettingsView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            session.Changed += () => Dispatcher.BeginInvoke(Load);
        }
        Load();
    }

    private void Load()
    {
        if (_session is null) return;
        _suppress = true;
        var c = _session.Config;
        Buffer.SelectedIndex = (int)c.Audio.BufferPreset;
        CustomMs.Value = c.Audio.CustomBufferMs;
        Exclusive.IsChecked = c.Audio.ShareMode == ShareModeSetting.Exclusive;
        Bypass.IsChecked = c.Audio.BypassProcessing;
        Tone.IsChecked = c.Advanced.TestToneOnStart;
        ClientId.Text = c.Spotify.ClientId ?? "";
        FindCmd.Text = SpotifyClientIdFinder.Command;
        SpStatus.Text = _session.Spotify.IsConnected
            ? $"Connected as {_session.Spotify.DisplayName}"
            : "Not connected. The app works without Spotify.";
        ProfileBox.ItemsSource = _session.Profiles.ToArray();
        ProfileBox.SelectedItem = c.ActiveProfile;
        WinStart.IsChecked = c.Startup.StartWithWindows;
        MinStart.IsChecked = c.Startup.StartMinimized;
        AutoEngine.IsChecked = c.Startup.StartEngineAutomatically;
        Err.Text = _session.LastError ?? "";
        _suppress = false;
    }

    private void ComboSave(object sender, SelectionChangedEventArgs e) => Save(sender, e);
    private void SliderSave(object sender, RoutedPropertyChangedEventArgs<double> e) => Save(sender, e);

    private void Save(object sender, RoutedEventArgs e)
    {
        if (_suppress || _session is null) return;
        var c = _session.Config;
        c.Audio.BufferPreset = (BufferPreset)Math.Max(0, Buffer.SelectedIndex);
        c.Audio.CustomBufferMs = (int)CustomMs.Value;
        c.Audio.ShareMode = Exclusive.IsChecked == true ? ShareModeSetting.Exclusive : ShareModeSetting.Shared;
        c.Audio.BypassProcessing = Bypass.IsChecked == true;
        c.Advanced.TestToneOnStart = Tone.IsChecked == true;
        c.Spotify.ClientId = ClientId.Text.Trim();
        c.Startup.StartWithWindows = WinStart.IsChecked == true;
        c.Startup.StartMinimized = MinStart.IsChecked == true;
        c.Startup.StartEngineAutomatically = AutoEngine.IsChecked == true;
        _session.MixerChanged();
        _session.ScheduleSave();
    }

    private void CopyFindCmd(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(SpotifyClientIdFinder.Command);
        _session?.Notify("Finder cmd copied.");
    }

    private void FindClientId(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var ok = MessageBox.Show(
            "Cuebox will search this PC for a Spotify Client ID in app data and env vars, then paste it here if it finds one. Continue?",
            "Find Spotify Client ID",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (ok != MessageBoxResult.Yes)
            return;

        var id = SpotifyClientIdFinder.Find();
        if (string.IsNullOrEmpty(id))
        {
            SpStatus.Text = "None found. Create an app at developer.spotify.com/dashboard. Redirect: http://127.0.0.1:43821/callback";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.spotify.com/dashboard") { UseShellExecute = true }); } catch { }
            return;
        }

        SpotifyClientIdFinder.SaveFound(id);
        ClientId.Text = id;
        _session.Config.Spotify.ClientId = id;
        _session.ScheduleSave();
        SpStatus.Text = "Pasted Client ID from this PC.";
        _session.Notify("Spotify Client ID filled in.");
    }

    private void Restart(object sender, RoutedEventArgs e) => _session?.RestartEngine();

    private async void ConnectSp(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        _session.Config.Spotify.ClientId = ClientId.Text.Trim();
        var r = await _session.ConnectSpotify();
        if (!r.Success)
            _session.SetError(r.Error ?? "Spotify login failed.", r.Details);
        else
            _session.Notify("Spotify connected.");
        _session.Persist();
        Load();
    }

    private void DisconnectSp(object sender, RoutedEventArgs e)
    {
        _session?.Spotify.Disconnect();
        _session?.Persist();
        Load();
    }

    private void SwitchProfile(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is string name)
            _session?.ApplyProfile(name);
    }

    private void SaveProfile(object sender, RoutedEventArgs e)
    {
        var name = ProfileBox.SelectedItem as string ?? _session?.Config.ActiveProfile ?? "Default";
        _session?.SaveCurrentProfile(name);
    }

    private void Export(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Cuebox backup|*.cuebox.zip", FileName = "cuebox-backup.cuebox.zip" };
        if (dlg.ShowDialog() == true)
        {
            var r = _session!.Backups.Export(dlg.FileName);
            _session.Notify(r.Success ? "Backup exported." : r.Error ?? "Export failed.");
        }
    }

    private void Import(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Cuebox backup|*.cuebox.zip;*.mixline.zip;*.zip" };
        if (dlg.ShowDialog() == true)
        {
            var r = _session!.Backups.Import(dlg.FileName);
            _session.Notify(r.Success ? "Backup imported. Restart Cuebox to apply everything." : r.Error ?? "Import failed.");
        }
    }

    private void CopyDiag(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_session?.Diagnostics() ?? "");
        _session?.Notify("Diagnostic info copied. Secrets are stripped.");
    }

    private void Details(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        MessageBox.Show(_session.LastErrorDetails ?? "No extra detail.", "Details");
    }
}
