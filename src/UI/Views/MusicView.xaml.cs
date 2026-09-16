using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        if (p.Duration.TotalSeconds <= 0)
            return;
        if (!Seek.IsMouseCaptureWithin)
            Seek.Value = p.Position.TotalSeconds / p.Duration.TotalSeconds;
        PosText.Text = $"{Format(p.Position)} / {Format(p.Duration)}";
    }

    private void Refresh()
    {
        if (_session is null) return;
        _suppress = true;
        QueueList.Items.Clear();
        foreach (var t in _session.Engine.Music.Queue)
            QueueList.Items.Add(t);
        if (_session.Engine.Music.Index >= 0 && _session.Engine.Music.Index < QueueList.Items.Count)
            QueueList.SelectedIndex = _session.Engine.Music.Index;
        EmptyQueue.Visibility = QueueList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Shuffle.IsChecked = _session.Config.Music.Shuffle;
        Loop.SelectedIndex = (int)_session.Config.Music.Loop;
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

    private void QueueClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            PlaySelected();
    }

    private void PlaySelected()
    {
        if (_session is null || QueueList.SelectedIndex < 0) return;
        _session.Engine.Music.PlayIndex(QueueList.SelectedIndex);
        Refresh();
    }

    private void Play(object sender, RoutedEventArgs e) => _session?.Engine.Music.Play();
    private void Pause(object sender, RoutedEventArgs e) => _session?.Engine.Music.Pause();
    private void Stop(object sender, RoutedEventArgs e) => _session?.Engine.Music.Stop();

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

    private void Seeked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_session is null) return;
        var dur = _session.Engine.Music.Duration;
        _session.Engine.Music.Seek(TimeSpan.FromSeconds(Seek.Value * dur.TotalSeconds));
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

    private async void CleanRap(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;
        UrlStatus.Text = "Finding clean rap...";
        var result = await _session.FindCleanRap();
        UrlStatus.Text = result.Message;
        if (!result.Ok)
            return;
        var start = _session.Engine.Music.Queue.Count;
        foreach (var t in result.Tracks)
            _session.Engine.Music.Add(t);
        PersistQueue();
        Refresh();
        if (result.Tracks.Count > 0)
            _session.Engine.Music.PlayIndex(start);
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
        _session.ScheduleSave();
    }

    private static string Format(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
}
