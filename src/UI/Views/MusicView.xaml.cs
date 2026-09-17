using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Mixline.Audio.Sources;
using Mixline.Core;

namespace Mixline.App.Views;

public partial class MusicView : UserControl
{
    private AppSession? _session;
    private bool _suppress;
    private bool _bound;

    public MusicView() => InitializeComponent();

    public void Bind(AppSession session)
    {
        _session = session;
        if (!_bound)
        {
            _bound = true;
            session.Changed += () => Dispatcher.BeginInvoke(Refresh);
        }
        Refresh();
    }

    public void TickPosition()
    {
        if (_session is null) return;
        var p = _session.Engine.Music;
        if (NowCard.Visibility != Visibility.Visible)
            return;
        NowTime.Text = $"{Format(p.Position)} / {Format(p.Duration)}";
    }

    private void Refresh()
    {
        if (_session is null) return;
        _suppress = true;
        QueueList.Items.Clear();
        var n = 1;
        foreach (var t in _session.Engine.Music.Queue)
        {
            QueueList.Items.Add(new QueueRow
            {
                Number = n++,
                Title = t.Title,
                Artist = string.IsNullOrWhiteSpace(t.Artist) ? t.FileName : t.Artist,
                Length = t.Length
            });
        }
        if (_session.Engine.Music.Index >= 0 && _session.Engine.Music.Index < QueueList.Items.Count)
            QueueList.SelectedIndex = _session.Engine.Music.Index;
        QueueCount.Text = QueueList.Items.Count == 1 ? "1 in queue" : $"{QueueList.Items.Count} in queue";
        EmptyQueue.Visibility = QueueList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var cur = _session.Engine.Music.Current;
        if (cur is null)
        {
            NowCard.Visibility = Visibility.Collapsed;
            NowArt.Source = null;
        }
        else
        {
            NowCard.Visibility = Visibility.Visible;
            NowName.Text = cur.Title;
            NowWho.Text = string.IsNullOrWhiteSpace(cur.Artist) ? cur.FileName : cur.Artist;
            SetNowArt(cur.Artwork);
            TickPosition();
        }
        Shuffle.IsChecked = _session.Config.Music.Shuffle;
        Loop.SelectedIndex = (int)_session.Config.Music.Loop;
        if (string.IsNullOrWhiteSpace(GitHubBox.Text) && !string.IsNullOrWhiteSpace(_session.Config.Music.GitHubRepo))
        {
            GitHubBox.Text = _session.Config.Music.GitHubRepo;
            GitHubHint.Visibility = Visibility.Collapsed;
        }
        TickPosition();
        _suppress = false;
    }

    private void AddFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Multiselect = true, Filter = "Audio|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.aac;*.wma" };
        if (dlg.ShowDialog() != true || _session is null) return;
        foreach (var f in dlg.FileNames)
            _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
        PersistQueue();
        Refresh();
    }

    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() != true || _session is null) return;
        foreach (var f in Directory.GetFiles(dlg.FolderName, "*.*", SearchOption.AllDirectories).Where(AudioFileSupport.IsSupportedFile))
            _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
        PersistQueue();
        Refresh();
    }

    private void QueuePlay(object sender, System.Windows.Input.MouseButtonEventArgs e) => PlaySelected();

    private void PlaySelected()
    {
        if (_session is null || QueueList.SelectedIndex < 0) return;
        _session.Engine.Music.PlayIndex(QueueList.SelectedIndex);
        Refresh();
    }

    private void LoopChanged(object sender, SelectionChangedEventArgs e) => Flags(sender, e);

    private void Flags(object sender, RoutedEventArgs e)
    {
        if (_suppress || _session is null) return;
        _session.Config.Music.Shuffle = Shuffle.IsChecked == true;
        _session.Config.Music.Loop = (LoopMode)Math.Max(0, Loop.SelectedIndex);
        _session.Engine.Music.SetShuffle(_session.Config.Music.Shuffle);
        _session.Engine.Music.SetLoop(_session.Config.Music.Loop);
        _session.ScheduleSave();
    }

    private async void LoadUrl(object sender, RoutedEventArgs e) => await AddLink();

    private async void UrlKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            await AddLink();
        }
    }

    private void UrlChanged(object sender, TextChangedEventArgs e)
        => UrlHint.Visibility = string.IsNullOrWhiteSpace(UrlBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    private async Task AddLink()
    {
        if (_session is null) return;
        var result = await _session.ImportLink(UrlBox.Text);
        UrlStatus.Text = result.Message;
        if (!result.Ok)
            return;
        foreach (var t in result.Tracks)
            _session.Engine.Music.Add(t);
        PersistQueue();
        Refresh();
        if (result.Tracks.Count > 0)
            _session.Engine.Music.PlayIndex(_session.Engine.Music.Queue.Count - result.Tracks.Count);
    }

    private bool _finding;
    private bool _githubBusy;

    private async void CleanRap(object sender, RoutedEventArgs e)
    {
        if (_session is null || _finding) return;
        _finding = true;
        UrlStatus.Text = "Finding clean rap...";
        try
        {
            var result = await _session.FindCleanRap();
            UrlStatus.Text = result.Message;
            if (!result.Ok)
                return;
            Enqueue(result.Tracks);
        }
        finally
        {
            _finding = false;
        }
    }

    private void Share(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var text = _session.ShareCurrent();
        if (string.IsNullOrWhiteSpace(text))
        {
            UrlStatus.Text = "Play a song first, then share it.";
            return;
        }
        Clipboard.SetText(text);
        UrlStatus.Text = "Copied. Send that to a friend and they can paste it in Add link.";
    }

    private void GitHubChanged(object sender, TextChangedEventArgs e)
        => GitHubHint.Visibility = string.IsNullOrWhiteSpace(GitHubBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    private async void GitHubKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            await ConnectGitHub();
        }
    }

    private async void GitHubConnect(object sender, RoutedEventArgs e) => await ConnectGitHub();

    private async Task ConnectGitHub()
    {
        if (_session is null || _githubBusy) return;
        _githubBusy = true;
        GitHubStatus.Text = "Connecting...";
        try
        {
            var result = await _session.ImportGitHub(GitHubBox.Text);
            GitHubStatus.Text = result.Message;
            if (!result.Ok)
                return;
            Enqueue(result.Tracks);
        }
        finally
        {
            _githubBusy = false;
        }
    }

    private void ExportPlaylist(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var json = GitHubPlaylist.ExportJson(_session.Engine.Music.Queue);
        if (json.Contains("\"tracks\": []") || json.Contains("\"tracks\":[]"))
        {
            GitHubStatus.Text = "Queue songs with links first, then export.";
            return;
        }
        var dlg = new SaveFileDialog
        {
            FileName = "cuebox.json",
            Filter = "Cuebox playlist|*.json"
        };
        if (dlg.ShowDialog() != true)
            return;
        File.WriteAllText(dlg.FileName, json);
        Clipboard.SetText(json);
        GitHubStatus.Text = "Saved cuebox.json and copied it. Put that file in a GitHub repo and share the repo link.";
    }

    private void Enqueue(List<TrackInfo> tracks)
    {
        if (_session is null || tracks.Count == 0) return;
        var start = _session.Engine.Music.Queue.Count;
        foreach (var t in tracks)
            _session.Engine.Music.Add(t);
        PersistQueue();
        Refresh();
        if (!_session.Engine.Music.IsPlaying)
            _session.Engine.Music.PlayIndex(start);
    }

    private async void SpPlay(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var r = await _session.Spotify.PlayAsync(CancellationToken.None);
        if (!r.Success) _session.Notify(r.Error ?? "Spotify play failed.");
    }

    private async void SpPause(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        var r = await _session.Spotify.PauseAsync(CancellationToken.None);
        if (!r.Success) _session.Notify(r.Error ?? "Spotify pause failed.");
    }

    private void FileOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
    }

    private void FileDrop(object sender, DragEventArgs e)
    {
        if (_session is null) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
        {
            if (Directory.Exists(f))
            {
                foreach (var p in Directory.GetFiles(f, "*.*", SearchOption.AllDirectories).Where(AudioFileSupport.IsSupportedFile))
                    _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(p));
            }
            else if (AudioFileSupport.IsSupportedFile(f))
                _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
        }
        PersistQueue();
        Refresh();
    }

    private void PersistQueue()
    {
        if (_session is null) return;
        _session.Config.Music.Queue = _session.Engine.Music.Queue.Select(t => t.Path).ToList();
        _session.Config.Music.QueueIndex = _session.Engine.Music.Index;
        _session.ScheduleSave();
    }

    private static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private void SetNowArt(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            NowArt.Source = null;
            return;
        }

        try
        {
            var img = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            NowArt.Source = img;
        }
        catch
        {
            NowArt.Source = null;
        }
    }
}

internal sealed class QueueRow
{
    public int Number { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Length { get; init; } = "";
}
