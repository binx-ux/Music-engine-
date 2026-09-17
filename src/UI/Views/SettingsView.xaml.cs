using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        RedirectBox.Text = SpotifyClient.RedirectHelp();
        FindCmd.Text = SpotifyClientIdFinder.Command;
        SpStatus.Text = _session.Spotify.IsConnected
            ? $"Connected as {_session.Spotify.DisplayName}. Spotify Premium required for play, pause, and skip."
            : "Not connected. You need Spotify Premium for play, pause, and skip.";
        ProfileBox.ItemsSource = _session.Profiles.ToArray();
        ProfileBox.SelectedItem = c.ActiveProfile;
        WinStart.IsChecked = c.Startup.StartWithWindows;
        MinStart.IsChecked = c.Startup.StartMinimized;
        AutoEngine.IsChecked = c.Startup.StartEngineAutomatically;
        CheckUpdates.IsChecked = c.Updates.CheckOnStart;
        UpdateStatus.Text = "Cuebox " + AppInfo.Version;
        Err.Text = _session.LastError ?? "";
        DataPath.Text = AppPaths.Root;
        HexBox.Text = string.IsNullOrWhiteSpace(c.Appearance.AccentHex)
            ? Theme.PresetHex(c.Appearance.Theme)
            : c.Appearance.AccentHex;
        var scale = c.Appearance.UiScale;
        if (scale < 0.9 || scale > 1.2)
            scale = 1;
        UiScale.Value = scale;
        BuildThemes();
        MarkThemes();
        _suppress = false;
    }

    private void BuildThemes()
    {
        if (ThemeRow.Children.Count > 0)
            return;
        foreach (var p in Theme.Presets)
        {
            var fill = (SolidColorBrush)new BrushConverter().ConvertFrom(p.Hex)!;
            fill.Freeze();
            var btn = new Button
            {
                Width = 78,
                Height = 46,
                Margin = new Thickness(0, 0, 8, 8),
                Tag = p.Id,
                ToolTip = p.Hex,
                Cursor = Cursors.Hand,
                Content = p.Label,
                Background = fill,
                Style = (Style)FindResource("ThemeChip")
            };
            btn.Click += (_, _) => PickTheme((string)btn.Tag);
            ThemeRow.Children.Add(btn);
        }
    }

    private void MarkThemes()
    {
        if (_session is null) return;
        var id = _session.Config.Appearance.Theme;
        foreach (Button btn in ThemeRow.Children)
        {
            var on = (string)btn.Tag == id;
            btn.BorderThickness = new Thickness(on ? 2 : 1);
            btn.BorderBrush = on
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(0x33, 0, 0, 0));
        }
    }

    private void ScaleSave(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppress || _session is null) return;
        _session.Config.Appearance.UiScale = Math.Round(UiScale.Value, 2);
        Theme.Apply(_session.Config.Appearance);
        _session.ScheduleSave();
    }

    private void PickTheme(string id)
    {
        if (_session is null) return;
        _session.Config.Appearance.Theme = id;
        _session.Config.Appearance.AccentHex = Theme.PresetHex(id);
        HexBox.Text = _session.Config.Appearance.AccentHex;
        Theme.Apply(_session.Config.Appearance);
        MarkThemes();
        _session.ScheduleSave();
    }

    private void SetHex(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var hex = HexBox.Text.Trim();
        if (!Theme.LooksHex(hex))
        {
            _session.Notify("Use a hex color like #C9A36A.");
            return;
        }
        if (!hex.StartsWith('#'))
            hex = "#" + hex;
        _session.Config.Appearance.Theme = "Custom";
        _session.Config.Appearance.AccentHex = hex;
        Theme.Apply(_session.Config.Appearance);
        MarkThemes();
        _session.ScheduleSave();
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
        c.Updates.CheckOnStart = CheckUpdates.IsChecked == true;
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
            SpStatus.Text = "None found. Create an app at developer.spotify.com/dashboard and add both redirect URIs from Settings.";
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

    private void CopyRedirect(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(SpotifyClient.RedirectHelp());
        _session?.Notify("Redirect URIs copied.");
    }

    private void OpenDashboard(object sender, RoutedEventArgs e)
    {
        SpotifyClient.OpenDashboard();
        _session?.Notify("Add both redirect URIs, Save, then Connect.");
    }

    private async void ConnectSp(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        _session.Config.Spotify.ClientId = ClientId.Text.Trim();
        _session.Config.Spotify.RedirectUri = SpotifyClient.RedirectUri;
        Clipboard.SetText(SpotifyClient.RedirectHelp());
        if (!_session.Spotify.IsConnected)
        {
            var go = MessageBox.Show(
                "You need Spotify Premium for play, pause, and skip.\n\n" +
                "Spotify must have these Redirect URIs on your app (copied):\n\n" +
                SpotifyClient.RedirectHelp() +
                "\n\nOpen the dashboard, paste them, click Save, then sign in.\n\nOpen the dashboard now?",
                "Connect Spotify",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (go == MessageBoxResult.Yes)
                SpotifyClient.OpenDashboard();
        }
        SpStatus.Text = "Waiting for Spotify login...";
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

    private void MoveData(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var dlg = new OpenFolderDialog
        {
            Title = "Cuebox data folder",
            InitialDirectory = AppPaths.Root
        };
        if (dlg.ShowDialog() != true)
            return;
        if (!AppPaths.Relocate(dlg.FolderName, out var error))
        {
            _session.Notify(error);
            return;
        }
        DataPath.Text = AppPaths.Root;
        _session.Notify("Data folder set. Restart Cuebox if logs look empty.");
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

    private async void CheckNow(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow main)
            return;
        UpdateStatus.Text = "Checking GitHub...";
        await main.CheckForUpdate(true);
        UpdateStatus.Text = "Cuebox " + AppInfo.Version;
    }

    private void Details(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        MessageBox.Show(_session.LastErrorDetails ?? "No extra detail.", "Details");
    }
}
