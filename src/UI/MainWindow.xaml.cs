using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mixline.App.Views;
using Mixline.Audio.Mixer;
using Mixline.Core;
using Mixline.Soundboard;
using Mixline.Spotify;

namespace Mixline.App;

public partial class MainWindow : Window
{
    private static readonly Geometry PlayGeo = FreezeGeo("M 8,7 L 8,17 L 16,12 Z");
    private static readonly Geometry PauseGeo = FreezeGeo("M 8,8 L 8,16 L 11,16 L 11,8 Z M 13,8 L 13,16 L 16,16 L 16,8 Z");
    private static readonly SolidColorBrush VirtOn = FreezeBrush(125, 206, 160);
    private static readonly SolidColorBrush VirtOff = FreezeBrush(120, 120, 120);

    private readonly AppSession _session = App.Session;
    private readonly DispatcherTimer _meterTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _spotifyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly HomeView _home = new();
    private readonly MixerView _mixer = new();
    private readonly SoundboardView _board = new();
    private readonly MusicView _music = new();
    private readonly VoiceView _voice = new();
    private readonly DevicesView _devices = new();
    private readonly SettingsView _settings = new();
    private byte[]? _artBytes;
    private string? _artUrl;
    private bool _playIconPlaying;
    private bool _virtConnected;
    private string? _nowKey;
    private object? _chromeTrack;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeBackdrop.TryApply(this, true);
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _meterTimer.Stop();
            _spotifyTimer.Stop();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        src?.AddHook(WndProc);
        _session.Hotkeys.Attach(new WindowInteropHelper(this).Handle);
        _session.RegisterHotkeys();
        Host.Content = _home;
        _home.Bind(_session);
        _mixer.Bind(_session);
        _board.Bind(_session);
        _music.Bind(_session);
        _voice.Bind(_session);
        _devices.Bind(_session);
        _settings.Bind(_session);
        _session.Changed += () => Dispatcher.BeginInvoke(RefreshChrome);
        _session.Meters += OnMeters;
        _session.SpotifyUpdated += OnSpotify;
        _meterTimer.Tick += (_, _) => _session.TickMeters();
        _meterTimer.Start();
        _spotifyTimer.Tick += async (_, _) => await _session.RefreshSpotify(CancellationToken.None);
        _spotifyTimer.Start();
        RefreshChrome();
        CopySpotifyFinder();
        _ = LoadInstantPresets();
        if (_session.Config.Startup.StartMinimized)
            WindowState = WindowState.Minimized;
    }

    private void TitleDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
            return;
        var src = e.OriginalSource as DependencyObject;
        while (src is not null && !ReferenceEquals(src, sender))
        {
            if (src is Button)
                return;
            src = VisualTreeHelper.GetParent(src);
        }
        if (e.ClickCount == 2)
        {
            MaxClick(sender, e);
            return;
        }
        DragMove();
    }

    private static void CopySpotifyFinder()
    {
        try
        {
            var dest = Path.Combine(Mixline.Core.AppPaths.Root, "find-spotify-id.ps1");
            var src = Path.Combine(AppContext.BaseDirectory, "scripts", "find-spotify-id.ps1");
            if (File.Exists(src))
                File.Copy(src, dest, true);
        }
        catch
        {
        }
    }

    private async Task LoadInstantPresets()
    {
        try
        {
            if (_session.Layout.Pads.Count > 0)
                return;
            var n = await InstantPresets.InstallAsync(_session.Layout, CancellationToken.None);
            if (n <= 0)
                return;
            _session.SoundboardStore.Save(_session.Layout);
            _session.RaiseLayout();
        }
        catch
        {
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_session.Hotkeys.Handle(msg, wParam))
            handled = true;
        return IntPtr.Zero;
    }

    private void NavChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        if (NavMixer.IsChecked == true) Show(_mixer);
        else if (NavBoard.IsChecked == true) Show(_board);
        else if (NavMusic.IsChecked == true) Show(_music);
        else if (NavVoice.IsChecked == true) Show(_voice);
        else if (NavDevices.IsChecked == true) Show(_devices);
        else if (NavSettings.IsChecked == true) Show(_settings);
        else Show(_home);
    }

    private void Show(UserControl page)
    {
        if (Host.Content == page)
            return;
        Host.Content = page;
        Host.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90)));
    }

    private void RefreshChrome()
    {
        var track = _session.Engine.Music.Current;
        var key = track is null ? "" : track.Title + "\n" + track.Artist + "\n" + track.FileName;
        if (track is not null && key != _nowKey)
        {
            _nowKey = key;
            NowTitle.Text = track.Title;
            NowArtist.Text = string.IsNullOrWhiteSpace(track.Artist) ? track.FileName : track.Artist;
            SetArt(track.Artwork);
        }

        SetPlayIcon(_session.Engine.Music.IsPlaying);

        var virt = _session.Engine.VirtualStatus();
        if (virt.Connected != _virtConnected)
        {
            _virtConnected = virt.Connected;
            VirtDot.Fill = virt.Connected ? VirtOn : VirtOff;
        }
        var virtText = string.IsNullOrEmpty(virt.RenderName)
            ? virt.Message
            : virt.RenderName + "  ·  " + virt.Message;
        if (VirtText.Text != virtText)
            VirtText.Text = virtText;
        MicLive.Text = _session.Engine.MicrophoneActive ? "Mic on" : "";
        MicLivePill.Visibility = _session.Engine.MicrophoneActive ? Visibility.Visible : Visibility.Collapsed;
        var lat = _session.Engine.Latency;
        LatencyText.Text = _session.Engine.IsRunning
            ? $"In {lat.InputMs} ms  ·  DSP {lat.ProcessingMs} ms  ·  Out {lat.OutputMs} ms  ·  ~{lat.TotalMs} ms"
            : "Engine stopped";
    }

    private void OnMeters(MeterState meters)
    {
        var page = Host.Content;
        if (page == _home) _home.UpdateMeters(meters);
        else if (page == _mixer) _mixer.UpdateMeters(meters);
        else if (page == _voice) _voice.UpdateLive(meters);
        else if (page == _music) _music.TickPosition();
        SetPlayIcon(_session.Engine.Music.IsPlaying);
        var track = _session.Engine.Music.Current;
        if (!ReferenceEquals(track, _chromeTrack))
        {
            _chromeTrack = track;
            RefreshChrome();
        }
    }

    private void SetPlayIcon(bool playing)
    {
        if (playing == _playIconPlaying)
            return;
        _playIconPlaying = playing;
        PlayIcon.Data = playing ? PauseGeo : PlayGeo;
    }

    private void OnSpotify(SpotifyNowPlaying? now)
    {
        if (now is null || string.IsNullOrEmpty(now.Title))
            return;
        if (_session.Engine.Music.Current is not null)
            return;
        var key = "sp:" + now.Title + now.Artist + now.IsPlaying;
        if (key != _nowKey)
        {
            _nowKey = key;
            NowTitle.Text = now.Title;
            NowArtist.Text = now.Artist + (now.IsPlaying ? "" : "  (paused on Spotify)");
        }
        if (!string.IsNullOrEmpty(now.ArtworkUrl))
            SetArtUrl(now.ArtworkUrl);
    }

    private void SetArt(byte[]? data)
    {
        if (ReferenceEquals(data, _artBytes) && ArtImage.Source is not null)
            return;
        _artBytes = data;
        _artUrl = null;
        if (data is null || data.Length == 0)
        {
            ArtImage.Source = null;
            return;
        }
        try
        {
            var img = new BitmapImage();
            using var ms = new MemoryStream(data);
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            ArtImage.Source = img;
        }
        catch
        {
            ArtImage.Source = null;
        }
    }

    private void SetArtUrl(string url)
    {
        if (_artUrl == url)
            return;
        _artUrl = url;
        _artBytes = null;
        try
        {
            var img = new BitmapImage(new Uri(url));
            ArtImage.Source = img;
        }
        catch
        {
        }
    }

    private void PlayClick(object sender, RoutedEventArgs e)
    {
        if (_session.Engine.Music.IsPlaying)
            _session.Engine.Music.Pause();
        else
            _session.Engine.Music.Play();
        RefreshChrome();
    }

    private void PrevClick(object sender, RoutedEventArgs e)
    {
        _session.Engine.Music.Previous();
        RefreshChrome();
    }

    private void NextClick(object sender, RoutedEventArgs e)
    {
        _session.Engine.Music.Next();
        RefreshChrome();
    }

    private void MinClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void WinState(object sender, EventArgs e)
        => MaxBtn.Content = WindowState == WindowState.Maximized ? "❐" : "▢";

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private static Geometry FreezeGeo(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    private static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
