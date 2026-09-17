using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Mixline.Audio;
using Mixline.Audio.DSP;
using Mixline.Audio.Mixer;
using Mixline.Audio.Sources;
using Mixline.Core;
using Mixline.IPC;
using Mixline.Logging;
using Mixline.Soundboard;
using Mixline.Spotify;
using Mixline.Storage;

namespace Mixline.App;

public sealed class AppSession : IDisposable
{
    public AppLog Log { get; } = new();
    public ConfigStore ConfigStore { get; }
    public BackupService Backups { get; }
    public AudioEngine Engine { get; }
    public SoundboardStore SoundboardStore { get; }
    public SpotifyClient Spotify { get; }
    public HotkeyService Hotkeys { get; }
    public YtDlpClient Downloader { get; }
    public GitHubPlaylist GitHub { get; }
    public AppConfig Config { get; private set; }
    public SoundboardLayout Layout { get; private set; }
    public ObservableCollection<string> Profiles { get; } = [];
    public ObservableCollection<string> Notifications { get; } = [];

    private readonly SecretStore _secrets = new();
    private readonly Dictionary<Guid, DecodedClip> _clips = [];
    private readonly DispatcherTimer _saveTimer;
    private readonly UrlAudioValidator _urls = new();

    public event Action? Changed;
    public event Action? LayoutChanged;
    public event Action? PadNamesChanged;
    public event Action<MeterState>? Meters;
    public event Action<SpotifyNowPlaying?>? SpotifyUpdated;
    public string? LastError { get; private set; }
    public string? LastErrorDetails { get; private set; }

    public AppSession()
    {
        ConfigStore = new ConfigStore(Log);
        Backups = new BackupService(Log);
        Engine = new AudioEngine(Log);
        SoundboardStore = new SoundboardStore(Log);
        Spotify = new SpotifyClient(Log);
        Hotkeys = new HotkeyService(Log);
        Downloader = new YtDlpClient(Log);
        GitHub = new GitHubPlaylist(Log, Downloader);
        Config = ConfigStore.Load();
        Layout = SoundboardStore.Load(Config.Soundboard.ActiveLayout);
        LuaPads.Load(Path.Combine(AppContext.BaseDirectory, "scripts", "pads.lua"));
        LuaPads.Load(Path.Combine(AppPaths.Root, "pads.lua"));
        EnsureDefaultProfiles();
        RefreshProfiles();
        LoadSpotifyTokens();

        Engine.ErrorRaised += (_, msg) => SetError(msg, null);
        Engine.StatusChanged += (_, msg) => Log.Info("ui", msg);
        Hotkeys.Triggered += OnHotkey;
        Hotkeys.Conflict += c => Notify(c.Message);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Persist();
        };
    }

    public void StartEngineIfNeeded()
    {
        Engine.UpdateMixer(Config.Mixer, Config.Audio.BypassProcessing, Config.Advanced.TestToneOnStart);
        Engine.UpdateVoice(Config.Voice);
        Engine.Music.SetShuffle(Config.Music.Shuffle);
        Engine.Music.SetLoop(Config.Music.Loop);
        var tracks = Config.Music.Queue
            .Where(File.Exists)
            .Select(AudioFileSupport.ReadMetadata)
            .ToList();
        if (tracks.Count > 0)
            Engine.Music.ReplaceQueue(tracks, Math.Clamp(Config.Music.QueueIndex, 0, tracks.Count - 1));
        if (!Config.Startup.StartEngineAutomatically)
            return;
        var result = Engine.Start(Config);
        if (!result.Success)
            SetError(result.Error ?? "Audio failed.", result.Details);
        ScheduleSave();
    }

    public Result RestartEngine()
    {
        Engine.UpdateMixer(Config.Mixer, Config.Audio.BypassProcessing, false);
        Engine.UpdateVoice(Config.Voice);
        var result = Engine.Start(Config);
        if (!result.Success)
            SetError(result.Error ?? "Audio failed.", result.Details);
        else
            ClearError();
        ScheduleSave();
        Push();
        return result;
    }

    public void StopEngine()
    {
        Engine.Stop();
        Push();
    }

    public void MixerChanged()
    {
        Engine.UpdateMixer(Config.Mixer, Config.Audio.BypassProcessing, Config.Advanced.TestToneOnStart);
        ScheduleSave();
    }

    public void VoiceChanged()
    {
        Engine.UpdateVoice(Config.Voice);
        ScheduleSave();
    }

    public void RaiseLayout() => LayoutChanged?.Invoke();
    public void RaisePadNames() => PadNamesChanged?.Invoke();

    public void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Persist()
    {
        ConfigStore.Save(Config);
        SoundboardStore.Save(Layout);
        SaveSpotifyTokens();
        StartupRegistration.Apply(Config.Startup.StartWithWindows, Environment.ProcessPath ?? "");
    }

    public void ApplyProfile(string name)
    {
        var profile = ConfigStore.LoadProfile(name);
        ConfigStore.ApplyProfile(Config, profile);
        Layout = SoundboardStore.Load(Config.Soundboard.ActiveLayout);
        MixerChanged();
        VoiceChanged();
        RestartEngine();
        Persist();
        RaiseLayout();
        Notify($"Switched to {name}");
    }

    public void SaveCurrentProfile(string name)
    {
        var profile = ConfigStore.CaptureProfile(name, Config);
        ConfigStore.SaveProfile(profile);
        Config.ActiveProfile = name;
        RefreshProfiles();
        Persist();
        Notify($"Saved profile {name}");
    }

    public async Task PlayPad(SoundPad pad)
    {
        if (string.IsNullOrWhiteSpace(pad.FilePath) || !File.Exists(pad.FilePath))
        {
            SetError("This sound has no audio file.", pad.FilePath);
            return;
        }

        DecodedClip? clip;
        lock (_clips)
            _clips.TryGetValue(pad.Id, out clip);
        if (clip is null)
        {
            var loaded = await Task.Run(() => ClipLoader.Load(pad.FilePath, Engine.SampleRate, Log));
            if (!loaded.Success || loaded.Value is null)
            {
                SetError(loaded.Error ?? "Could not load sound.", loaded.Details);
                return;
            }
            clip = loaded.Value;
            lock (_clips)
                _clips[pad.Id] = clip;
        }

        Engine.PlaySound(new VoicePlayback
        {
            Id = Guid.NewGuid(),
            Samples = clip.Samples,
            Volume = LuaPads.Volume(pad) * Config.Soundboard.MasterVolume,
            Pitch = pad.Pitch,
            Speed = pad.Speed,
            Loop = pad.Loop,
            FadeIn = pad.FadeIn,
            FadeOut = pad.FadeOut
        });
    }

    public void RegisterHotkeys()
    {
        Hotkeys.Clear();
        foreach (var pad in Layout.Pads)
        {
            if (string.IsNullOrWhiteSpace(pad.Hotkey))
                continue;
            var parsed = HotkeyParser.Parse(pad.Id.ToString(), pad.Hotkey);
            if (!parsed.Success || parsed.Value is null)
            {
                Notify(parsed.Error ?? "Invalid shortcut.");
                continue;
            }
            Hotkeys.Register(parsed.Value);
        }
    }

    public void PrefetchPad(SoundPad pad)
    {
        if (string.IsNullOrWhiteSpace(pad.FilePath) || !File.Exists(pad.FilePath))
            return;
        lock (_clips)
        {
            if (_clips.ContainsKey(pad.Id))
                return;
        }
        var id = pad.Id;
        var path = pad.FilePath;
        var rate = Engine.SampleRate;
        _ = Task.Run(() =>
        {
            var loaded = ClipLoader.Load(path, rate, Log);
            if (!loaded.Success || loaded.Value is null)
                return;
            lock (_clips)
                _clips[id] = loaded.Value;
        });
    }

    public async Task<(bool Ok, string Message, List<TrackInfo> Tracks)> ImportLink(string text)
    {
        text = (text ?? "").Trim();
        var tracks = new List<TrackInfo>();
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Paste a YouTube, SoundCloud, or audio link.", tracks);
        if (File.Exists(text) && AudioFileSupport.IsSupportedFile(text))
        {
            tracks.Add(AudioFileSupport.ReadMetadata(text));
            return (true, "Added file.", tracks);
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (false, "Paste a link or a file path.", tracks);

        if (GitHubPlaylist.LooksLike(text) && (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
            return await ImportGitHub(text);

        var check = await _urls.ValidateAsync(text, CancellationToken.None);
        if (check.Ok)
        {
            tracks.Add(new TrackInfo
            {
                Path = text,
                Title = check.Title ?? uri.Host,
                FileName = check.Title ?? uri.Host,
                IsUrl = true,
                SourceUrl = text
            });
            return (true, "Ready to play.", tracks);
        }

        Notify("Downloading...");
        var got = await Downloader.DownloadLinkAsync(text, CancellationToken.None);
        if (!got.Success || got.Value is null || got.Value.Count == 0)
            return (false, got.Error ?? "Download failed.", tracks);
        foreach (var file in got.Value)
            tracks.Add(AudioFileSupport.ReadMetadata(file) with { SourceUrl = text });
        return (true, $"Downloaded {tracks.Count} track(s).", tracks);
    }

    public async Task<(bool Ok, string Message, List<TrackInfo> Tracks)> FindCleanRap()
    {
        Notify("Finding clean rap...");
        var got = await Downloader.FindCleanRapAsync(CancellationToken.None);
        var tracks = new List<TrackInfo>();
        if (!got.Success || got.Value is null || got.Value.Count == 0)
            return (false, got.Error ?? "Could not find clean rap.", tracks);

        var existing = new HashSet<string>(Engine.Music.Queue.Select(t => t.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var file in got.Value)
        {
            if (!existing.Add(file))
                continue;
            tracks.Add(AudioFileSupport.ReadMetadata(file));
        }
        if (tracks.Count == 0)
            return (true, "Clean rap is already in your queue.", tracks);
        return (true, $"Added {tracks.Count} clean rap tracks.", tracks);
    }

    public async Task<(bool Ok, string Message, List<TrackInfo> Tracks)> ImportGitHub(string text)
    {
        text = (text ?? "").Trim();
        var tracks = new List<TrackInfo>();
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Paste a GitHub repo URL.", tracks);

        Notify("Connecting to GitHub...");
        var got = await GitHub.LoadAsync(text, CancellationToken.None);
        if (!got.Success || got.Value is null || got.Value.Count == 0)
            return (false, got.Error ?? "Could not load that GitHub playlist.", tracks);

        var existing = new HashSet<string>(Engine.Music.Queue.Select(t => t.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var t in got.Value)
        {
            if (!existing.Add(t.Path))
                continue;
            tracks.Add(t);
        }

        Config.Music.GitHubRepo = text;
        ScheduleSave();
        if (tracks.Count == 0)
            return (true, "That GitHub playlist is already in your queue.", tracks);
        return (true, $"Added {tracks.Count} tracks from GitHub.", tracks);
    }

    public string ShareCurrent()
    {
        var t = Engine.Music.Current;
        if (t is null)
            return "";
        if (!string.IsNullOrWhiteSpace(t.SourceUrl))
            return t.Title + Environment.NewLine + t.SourceUrl;
        return t.Title + Environment.NewLine + t.Path;
    }

    public async Task<Result> ConnectSpotify()
        => await Spotify.ConnectAsync(Config.Spotify.ClientId ?? "", CancellationToken.None);

    public async Task RefreshSpotify(CancellationToken ct)
    {
        if (!Spotify.IsConnected)
        {
            SpotifyUpdated?.Invoke(null);
            return;
        }
        if (!string.IsNullOrEmpty(Config.Spotify.ClientId))
            await Spotify.RefreshAsync(Config.Spotify.ClientId, ct);
        var now = await Spotify.GetNowPlayingAsync(ct);
        SpotifyUpdated?.Invoke(now.Success ? now.Value : null);
    }

    public async Task<(bool Ok, string Message, TrackInfo? Track)> TryUrl(string url)
    {
        var result = await ImportLink(url);
        return (result.Ok, result.Message, result.Tracks.FirstOrDefault());
    }

    public void TickMeters()
    {
        Meters?.Invoke(Engine.ReadMeters());
        if (Engine.Music.ConsumeEnded())
        {
            Engine.Music.Next();
            Config.Music.QueueIndex = Engine.Music.Index;
            ScheduleSave();
            Push();
        }
    }

    public string Diagnostics() => Log.CopyDiagnostics();

    public void Notify(string message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Notifications.Insert(0, message);
            while (Notifications.Count > 8)
                Notifications.RemoveAt(Notifications.Count - 1);
        });
        Changed?.Invoke();
    }

    public void SetError(string message, string? details)
    {
        LastError = message;
        LastErrorDetails = details;
        Notify(message);
    }

    public void ClearError()
    {
        LastError = null;
        LastErrorDetails = null;
        Push();
    }

    private void OnHotkey(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return;
        var pad = Layout.Pads.FirstOrDefault(p => p.Id == guid);
        if (pad is not null)
            _ = PlayPad(pad);
    }

    private void EnsureDefaultProfiles()
    {
        var names = new[] { "Default", "Gaming", "Streaming", "Music", "Discord", "Roblox" };
        foreach (var name in names)
        {
            var path = Path.Combine(AppPaths.Profiles, name + ".json");
            if (!File.Exists(path))
                ConfigStore.SaveProfile(new UserProfile { Name = name });
        }
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in ConfigStore.ListProfiles())
            Profiles.Add(p);
    }

    private void LoadSpotifyTokens()
    {
        try
        {
            var data = _secrets.Load(AppPaths.TokenFile);
            if (data is null)
                return;
            var tokens = JsonSerializer.Deserialize<SpotifyTokens>(data);
            Spotify.LoadTokens(tokens);
            Config.Spotify.Connected = Spotify.IsConnected;
        }
        catch (Exception ex)
        {
            Log.Warning("spotify", "Could not restore Spotify session.", ex.Message);
        }
    }

    private void SaveSpotifyTokens()
    {
        try
        {
            if (Spotify.Tokens is null)
            {
                _secrets.Delete(AppPaths.TokenFile);
                Config.Spotify.Connected = false;
                return;
            }
            var json = JsonSerializer.SerializeToUtf8Bytes(Spotify.Tokens);
            _secrets.Save(AppPaths.TokenFile, json);
            Config.Spotify.Connected = Spotify.IsConnected;
        }
        catch (Exception ex)
        {
            Log.Warning("spotify", "Could not store Spotify session.", ex.Message);
        }
    }

    public void Push() => Changed?.Invoke();

    public void Dispose()
    {
        Persist();
        Hotkeys.Dispose();
        Engine.Dispose();
        Log.Dispose();
    }
}
