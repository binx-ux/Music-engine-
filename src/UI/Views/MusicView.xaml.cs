using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private bool _finding;
    private bool _githubBusy;
    // leftover from when i was debugging search, leave it
    private string _lastSearch = "";
    private int _searchTries;

    public MusicView()
    {
        InitializeComponent();
    }

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
        if (_session is null)
            return;

        var p = _session.Engine.Music;
        if (NowCard.Visibility != Visibility.Visible)
            return;

        // kinda ugly but it works
        var pos = Format(p.Position);
        var dur = Format(p.Duration);
        NowTime.Text = pos + " / " + dur;
    }

    private void Refresh()
    {
        if (_session is null)
            return;

        _suppress = true;
        QueueList.Items.Clear();

        var queue = _session.Engine.Music.Queue;
        var n = 1;
        for (var i = 0; i < queue.Count; i++)
        {
            var t = queue[i];
            var artist = t.Artist;
            if (string.IsNullOrWhiteSpace(artist))
                artist = t.FileName;

            // number starts at 1 for the ui, not zero
            QueueList.Items.Add(new QueueRow
            {
                Number = n,
                Title = t.Title,
                Artist = artist,
                Length = t.Length
            });
            n = n + 1;
        }

        var idx = _session.Engine.Music.Index;
        if (idx >= 0 && idx < QueueList.Items.Count)
        {
            QueueList.SelectedIndex = idx;
        }

        var count = QueueList.Items.Count;
        if (count == 1)
            QueueCount.Text = "1 in queue";
        else
            QueueCount.Text = count + " in queue";

        if (count == 0)
            EmptyQueue.Visibility = Visibility.Visible;
        else
            EmptyQueue.Visibility = Visibility.Collapsed;

        var cur = _session.Engine.Music.Current;
        if (cur == null)
        {
            NowCard.Visibility = Visibility.Collapsed;
            NowArt.Source = null;
        }
        else
        {
            var wasHidden = NowCard.Visibility != Visibility.Visible;
            NowCard.Visibility = Visibility.Visible;
            NowName.Text = cur.Title;

            if (string.IsNullOrWhiteSpace(cur.Artist))
                NowWho.Text = cur.FileName;
            else
                NowWho.Text = cur.Artist;

            SetNowArt(cur.Artwork);
            TickPosition();

            // animate in the first time it shows up
            if (wasHidden)
            {
                try
                {
                    UiMotion.Enter(NowCard, NowSlide);
                }
                catch
                {
                    // whatever, card still shows
                }
            }
        }

        Shuffle.IsChecked = _session.Config.Music.Shuffle;

        var loopIdx = (int)_session.Config.Music.Loop;
        if (loopIdx < 0)
            loopIdx = 0;
        if (loopIdx > 2)
            loopIdx = 2;
        Loop.SelectedIndex = loopIdx;

        // fill github box if they already connected one before
        if (string.IsNullOrWhiteSpace(GitHubBox.Text))
        {
            var repo = _session.Config.Music.GitHubRepo;
            if (!string.IsNullOrWhiteSpace(repo))
            {
                GitHubBox.Text = repo;
                GitHubHint.Visibility = Visibility.Collapsed;
            }
        }

        TickPosition(); // called twice sometimes, fine
        _suppress = false;
    }

    private void AddFiles(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog();
        dlg.Multiselect = true;
        dlg.Filter = "Audio|*.mp3;*.wav;*.flac;*.ogg;*.m4a;*.aac;*.wma";
        if (dlg.ShowDialog() != true)
            return;
        if (_session is null)
            return;

        for (var i = 0; i < dlg.FileNames.Length; i++)
        {
            var f = dlg.FileNames[i];
            if (File.Exists(f))
                _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
        }

        PersistQueue();
        Refresh();
    }

    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() != true || _session is null)
            return;

        string[] files;
        try
        {
            files = Directory.GetFiles(dlg.FolderName, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            return;
        }

        foreach (var f in files)
        {
            if (!AudioFileSupport.IsSupportedFile(f))
                continue;
            _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
        }

        PersistQueue();
        Refresh();
    }

    private void QueuePlay(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_session is null || _suppress)
            return;
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
            return;

        // ignore clicks on the scrollbar
        var src = e.OriginalSource as DependencyObject;
        while (src != null)
        {
            if (src is System.Windows.Controls.Primitives.ScrollBar)
                return;
            if (src is System.Windows.Controls.Primitives.Thumb)
                return;
            if (src is System.Windows.Controls.Primitives.RepeatButton)
                return;
            src = VisualTreeHelper.GetParent(src);
        }

        var item = ItemsControl.ContainerFromElement(QueueList, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item == null)
            return;

        var i = QueueList.ItemContainerGenerator.IndexFromContainer(item);
        if (i < 0)
            return;

        _session.Engine.Music.PlayIndex(i);
        Refresh();
    }

    private void LoopChanged(object sender, SelectionChangedEventArgs e)
    {
        Flags(sender, e);
    }

    private void Flags(object sender, RoutedEventArgs e)
    {
        if (_suppress)
            return;
        if (_session is null)
            return;

        _session.Config.Music.Shuffle = Shuffle.IsChecked == true;
        var loop = Loop.SelectedIndex;
        if (loop < 0)
            loop = 0;
        _session.Config.Music.Loop = (LoopMode)loop;
        _session.Engine.Music.SetShuffle(_session.Config.Music.Shuffle);
        _session.Engine.Music.SetLoop(_session.Config.Music.Loop);
        _session.ScheduleSave();
    }

    private async void FindGo(object sender, RoutedEventArgs e)
    {
        await FindOrLink();
    }

    private async void UrlKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            await FindOrLink();
        }
    }

    private void UrlChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UrlBox.Text))
            UrlHint.Visibility = Visibility.Visible;
        else
            UrlHint.Visibility = Visibility.Collapsed;
    }

    private async Task FindOrLink()
    {
        if (_session is null)
            return;
        if (_finding)
            return;

        var text = UrlBox.Text;
        if (text == null)
            text = "";
        text = text.Trim();

        if (text.Length == 0)
        {
            UrlStatus.Text = "Type a song name or paste a link.";
            return;
        }

        _finding = true;
        _lastSearch = text;
        _searchTries = _searchTries + 1;

        try
        {
            var looksLikeUrl = false;
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                looksLikeUrl = true;
            if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                looksLikeUrl = true;
            if (File.Exists(text))
                looksLikeUrl = true;

            if (looksLikeUrl)
            {
                UrlStatus.Text = "Getting that...";
                var result = await _session.ImportLink(text);
                UrlStatus.Text = result.Message;
                if (result.Ok)
                {
                    Enqueue(result.Tracks);
                }
                return;
            }

            // normal seach path
            UrlStatus.Text = "Searching...";
            Hits.Items.Clear();
            Hits.Visibility = Visibility.Collapsed;

            var found = await _session.SearchSongs(text);
            UrlStatus.Text = found.Message;

            if (!found.Ok)
            {
                Hits.Visibility = Visibility.Collapsed;
                return;
            }

            var hits = found.Hits;
            if (hits == null)
            {
                Hits.Visibility = Visibility.Collapsed;
                return;
            }

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;
                if (string.IsNullOrWhiteSpace(hit.Id))
                    continue;
                Hits.Items.Add(hit);
            }

            if (Hits.Items.Count == 0)
            {
                Hits.Visibility = Visibility.Collapsed;
                UrlStatus.Text = "Nothing matched. Try a different name.";
                return;
            }

            Hits.Visibility = Visibility.Visible;
            try
            {
                UiMotion.Fade(Hits, 0, 1, 180);
            }
            catch
            {
            }
        }
        finally
        {
            _finding = false;
        }
    }

    private async void HitPick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_session is null || _finding)
            return;
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
            return;

        var item = ItemsControl.ContainerFromElement(Hits, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item == null)
            return;

        var hit = item.DataContext as SongHit;
        if (hit == null)
            return;
        if (string.IsNullOrWhiteSpace(hit.Id))
            return;

        _finding = true;
        var title = hit.Title;
        if (string.IsNullOrWhiteSpace(title))
            title = "song";
        UrlStatus.Text = "Downloading " + title + "...";

        try
        {
            var result = await _session.DownloadHit(hit);
            UrlStatus.Text = result.Message;
            if (result.Ok)
                Enqueue(result.Tracks);
        }
        finally
        {
            _finding = false;
        }
    }

    private async void CleanRap(object sender, RoutedEventArgs e)
    {
        if (_session is null || _finding)
            return;

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
        if (_session is null)
            return;

        var text = _session.ShareCurrent();
        if (string.IsNullOrWhiteSpace(text))
        {
            UrlStatus.Text = "Play a song first, then share it.";
            return;
        }

        Clipboard.SetText(text);
        UrlStatus.Text = "Copied. They can paste that in Search.";
    }

    private void GitHubChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GitHubBox.Text))
            GitHubHint.Visibility = Visibility.Visible;
        else
            GitHubHint.Visibility = Visibility.Collapsed;
    }

    private async void GitHubKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            await ConnectGitHub();
        }
    }

    private async void GitHubConnect(object sender, RoutedEventArgs e)
    {
        await ConnectGitHub();
    }

    private async Task ConnectGitHub()
    {
        if (_session is null)
            return;
        if (_githubBusy)
            return;

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
        if (_session is null)
            return;

        var json = GitHubPlaylist.ExportJson(_session.Engine.Music.Queue);
        // empty playlist check, yt-dlp json is weird about spaces
        if (json.Contains("\"tracks\": []") || json.Contains("\"tracks\":[]"))
        {
            GitHubStatus.Text = "Queue songs with links first, then export.";
            return;
        }

        var dlg = new SaveFileDialog();
        dlg.FileName = "cuebox.json";
        dlg.Filter = "Cuebox playlist|*.json";
        if (dlg.ShowDialog() != true)
            return;

        File.WriteAllText(dlg.FileName, json);
        Clipboard.SetText(json);
        GitHubStatus.Text = "Saved cuebox.json and copied it. Put that file in a GitHub repo and share the repo link.";
    }

    private void Enqueue(List<TrackInfo> tracks)
    {
        if (_session is null)
            return;
        if (tracks == null || tracks.Count == 0)
            return;

        var start = _session.Engine.Music.Queue.Count;
        for (var i = 0; i < tracks.Count; i++)
        {
            _session.Engine.Music.Add(tracks[i]);
        }

        PersistQueue();
        Refresh();

        // auto play if nothing is going
        if (!_session.Engine.Music.IsPlaying)
        {
            if (start >= 0 && start < _session.Engine.Music.Queue.Count)
                _session.Engine.Music.PlayIndex(start);
        }
    }

    private async void SpPlay(object sender, RoutedEventArgs e)
    {
        if (_session is null)
            return;
        var r = await _session.Spotify.PlayAsync(CancellationToken.None);
        if (!r.Success)
            _session.Notify(r.Error ?? "Spotify play failed.");
    }

    private async void SpPause(object sender, RoutedEventArgs e)
    {
        if (_session is null)
            return;
        var r = await _session.Spotify.PauseAsync(CancellationToken.None);
        if (!r.Success)
            _session.Notify(r.Error ?? "Spotify pause failed.");
    }

    private void FileOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
    }

    private void FileDrop(object sender, DragEventArgs e)
    {
        if (_session is null)
            return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        foreach (var f in files)
        {
            if (Directory.Exists(f))
            {
                string[] nested;
                try
                {
                    nested = Directory.GetFiles(f, "*.*", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var p in nested)
                {
                    if (AudioFileSupport.IsSupportedFile(p))
                        _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(p));
                }
            }
            else if (AudioFileSupport.IsSupportedFile(f))
            {
                _session.Engine.Music.Add(AudioFileSupport.ReadMetadata(f));
            }
        }

        PersistQueue();
        Refresh();
    }

    private void PersistQueue()
    {
        if (_session is null)
            return;

        var paths = new List<string>();
        foreach (var t in _session.Engine.Music.Queue)
            paths.Add(t.Path);

        _session.Config.Music.Queue = paths;
        _session.Config.Music.QueueIndex = _session.Engine.Music.Index;
        _session.ScheduleSave();
    }

    private static string Format(TimeSpan t)
    {
        var m = (int)t.TotalMinutes;
        var s = t.Seconds;
        if (s < 10)
            return m + ":0" + s;
        return m + ":" + s;
    }

    private void SetNowArt(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
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
            // bad art or weird format, just skip it
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
