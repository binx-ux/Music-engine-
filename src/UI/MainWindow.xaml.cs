using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private static readonly Geometry PlayGeo = FreezeGeo("M 8,4 L 20,12 L 8,20 Z");
    private static readonly Geometry PauseGeo = FreezeGeo("M 6,4 L 10,4 L 10,20 L 6,20 Z M 14,4 L 18,4 L 18,20 L 14,20 Z");
    private static readonly SolidColorBrush VirtOn = FreezeBrush(125, 206, 160);
    private static readonly SolidColorBrush VirtOff = FreezeBrush(120, 120, 120);

    private readonly AppSession _session = App.Session;
    private readonly DispatcherTimer _meterTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _spotifyTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly HomeView _home = new();
    private MixerView? _mixer;
    private SoundboardView? _board;
    private MusicView? _music;
    private VoiceView? _voice;
    private DevicesView? _devices;
    private SettingsView? _settings;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private byte[]? _artBytes;
    private string? _artUrl;
    private bool _playIconPlaying;
    private bool _virtConnected;
    private string? _nowKey;
    private int _toastGen;
    private object? _chromeTrack;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NativeBackdrop.TryApply(this, true);
        Theme.Changed += OnTheme;
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            Theme.Changed -= OnTheme;
            _meterTimer.Stop();
            _spotifyTimer.Stop();
            _toastTimer.Stop();
        };
    }

    private void OnTheme()
    {
        NativeBackdrop.TryApply(this, true);
        ApplyScale();
    }

    private void ApplyScale()
    {
        var s = _session.Config.Appearance.UiScale;
        if (s < 0.9 || s > 1.2)
            s = 1;
        LayoutTransform = Math.Abs(s - 1) < 0.01
            ? Transform.Identity
            : new ScaleTransform(s, s);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        src?.AddHook(WndProc);
        _session.Hotkeys.Attach(new WindowInteropHelper(this).Handle);
        _session.RegisterHotkeys();
        _home.Bind(_session);
        Host.Content = _home;
        _session.Changed += () => Dispatcher.BeginInvoke(RefreshChrome);
        _session.Meters += OnMeters;
        _session.SpotifyUpdated += OnSpotify;
        _meterTimer.Tick += (_, _) => _session.TickMeters();
        _meterTimer.Start();
        _spotifyTimer.Tick += async (_, _) => await _session.RefreshSpotify(CancellationToken.None);
        _spotifyTimer.Start();
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            var gen = _toastGen;
            UiMotion.Fade(ToastText, ToastText.Opacity, 0, 160, () =>
            {
                if (gen == _toastGen)
                    ToastText.Text = "";
            });
        };
        _session.Notifications.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(ShowToast);
        ApplyScale();
        RefreshChrome();
        CopySpotifyFinder();
        _ = LoadInstantPresets();
        UiMotion.Fade(this, 0, 1, 260);
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
            if (src is Button or Slider or Thumb)
                return;
            if (src is FrameworkElement fe && (fe.Name is "ChromeSeekWell" or "ChromeSeekFill"))
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
            var n = await InstantPresets.InstallAsync(_session.Layout, CancellationToken.None);
            if (n <= 0)
                return;
            _session.SoundboardStore.Save(_session.Layout);
            _session.RaiseLayout();
            _session.RegisterHotkeys();
            _session.Notify($"Added {n} soundboard presets.");
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
        if (NavMixer.IsChecked == true) Show(_mixer ??= new MixerView());
        else if (NavBoard.IsChecked == true) Show(_board ??= new SoundboardView());
        else if (NavMusic.IsChecked == true) Show(_music ??= new MusicView());
        else if (NavVoice.IsChecked == true) Show(_voice ??= new VoiceView());
        else if (NavDevices.IsChecked == true) Show(_devices ??= new DevicesView());
        else if (NavSettings.IsChecked == true) Show(_settings ??= new SettingsView());
        else Show(_home);
    }

    private void Show(UserControl page)
    {
        if (page is HomeView home) home.Bind(_session);
        else if (page is MixerView mixer) mixer.Bind(_session);
        else if (page is SoundboardView board) board.Bind(_session);
        else if (page is MusicView music) music.Bind(_session);
        else if (page is VoiceView voice) voice.Bind(_session);
        else if (page is DevicesView devices) devices.Bind(_session);
        else if (page is SettingsView settings) settings.Bind(_session);

        if (Host.Content == page)
            return;
        Host.Content = page;
        UiMotion.Enter(Host, HostSlide);
    }

    private void RefreshChrome()
    {
        var track = _session.Engine.Music.Current;
        if (track is null)
        {
            if (_nowKey != "")
            {
                _nowKey = "";
                NowTitle.Text = "Nothing playing";
                NowArtist.Text = "Queue a file or paste a link";
                SetArt(null);
            }
        }
        else
        {
            var key = track.Title + "\n" + track.Artist + "\n" + track.FileName;
            if (key != _nowKey)
            {
                _nowKey = key;
                NowTitle.Text = track.Title;
                NowArtist.Text = string.IsNullOrWhiteSpace(track.Artist) ? track.FileName : track.Artist;
                SetArt(track.Artwork);
            }
        }

        SetPlayIcon(_session.Engine.Music.IsPlaying);
        TickChromeSeek();
        ChromeShuffle.Opacity = _session.Config.Music.Shuffle ? 1 : 0.5;
        ChromeLoop.Content = _session.Config.Music.Loop == LoopMode.One ? "Loop 1" : "Loop";
        ChromeLoop.Opacity = _session.Config.Music.Loop == LoopMode.Off ? 0.5 : 1;

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
        else if (_mixer is not null && page == _mixer) _mixer.UpdateMeters(meters);
        else if (_voice is not null && page == _voice) _voice.UpdateLive(meters);
        else if (_music is not null && page == _music) _music.TickPosition();
        SetPlayIcon(_session.Engine.Music.IsPlaying);
        TickChromeSeek();
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
        PlayBtn.ToolTip = playing ? "Pause" : "Play";
    }

    private void TickChromeSeek()
    {
        var music = _session.Engine.Music;
        var dur = music.Duration.TotalSeconds;
        var frac = dur <= 0 ? 0 : Math.Clamp(music.Position.TotalSeconds / dur, 0, 1);
        ChromeSeekFill.BeginAnimation(FrameworkElement.WidthProperty, null);
        var target = ChromeSeekWell.ActualWidth * frac;
        if (Math.Abs(target - ChromeSeekFill.Width) > 24)
            UiMotion.WidthTo(ChromeSeekFill, target, 160);
        else
            ChromeSeekFill.Width = target;
        ChromePos.Text = FormatTime(music.Position);
        ChromeDur.Text = dur <= 0 ? "0:00" : FormatTime(music.Duration);
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private void ChromeSeekClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dur = _session.Engine.Music.Duration.TotalSeconds;
        if (dur <= 0)
            return;
        var x = e.GetPosition(ChromeSeekWell).X / Math.Max(1, ChromeSeekWell.ActualWidth);
        _session.Engine.Music.Seek(TimeSpan.FromSeconds(Math.Clamp(x, 0, 1) * dur));
        TickChromeSeek();
        e.Handled = true;
    }

    private void ShowToast()
    {
        if (_session.Notifications.Count == 0)
            return;
        ToastText.BeginAnimation(OpacityProperty, null);
        ToastText.Text = _session.Notifications[0];
        _toastGen++;
        UiMotion.Fade(ToastText, 0, 1, 140);
        _toastTimer.Stop();
        _toastTimer.Start();
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
            UiMotion.Fade(ArtImage, 0, 1, 220);
            ArtScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
            ArtScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
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
        UiMotion.Punch(PlayBtn);
        RefreshChrome();
    }

    private void PrevClick(object sender, RoutedEventArgs e)
    {
        _session.Engine.Music.Previous();
        if (sender is UIElement el)
            UiMotion.Punch(el);
        RefreshChrome();
    }

    private void NextClick(object sender, RoutedEventArgs e)
    {
        _session.Engine.Music.Next();
        if (sender is UIElement el)
            UiMotion.Punch(el);
        RefreshChrome();
    }

    private void ShuffleClick(object sender, RoutedEventArgs e)
    {
        _session.Config.Music.Shuffle = !_session.Config.Music.Shuffle;
        _session.Engine.Music.SetShuffle(_session.Config.Music.Shuffle);
        _session.ScheduleSave();
        _session.Push();
        RefreshChrome();
    }

    private void LoopClick(object sender, RoutedEventArgs e)
    {
        var next = _session.Config.Music.Loop switch
        {
            LoopMode.Off => LoopMode.All,
            LoopMode.All => LoopMode.One,
            _ => LoopMode.Off
        };
        _session.Config.Music.Loop = next;
        _session.Engine.Music.SetLoop(next);
        _session.ScheduleSave();
        _session.Push();
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
