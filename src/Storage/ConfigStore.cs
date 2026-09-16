using System.Text.Json;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Storage;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppLog _log;
    private readonly object _gate = new();

    public ConfigStore(AppLog log)
    {
        _log = log;
        AppPaths.EnsureCreated();
    }

    public AppConfig Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigFile))
                {
                    var fresh = new AppConfig();
                    Save(fresh);
                    return fresh;
                }

                var json = File.ReadAllText(AppPaths.ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                return Migrate(config);
            }
            catch (Exception ex)
            {
                _log.Error("config", "Could not load settings. Using defaults.", ex);
                return new AppConfig();
            }
        }
    }

    public void Save(AppConfig config)
    {
        lock (_gate)
        {
            try
            {
                config.Version = AppInfo.ConfigVersion;
                var json = JsonSerializer.Serialize(config, JsonOptions);
                var temp = AppPaths.ConfigFile + ".tmp";
                File.WriteAllText(temp, json);
                File.Copy(temp, AppPaths.ConfigFile, true);
                File.Delete(temp);
            }
            catch (Exception ex)
            {
                _log.Error("config", "Could not save settings.", ex);
            }
        }
    }

    public UserProfile LoadProfile(string name)
    {
        var path = ProfilePath(name);
        try
        {
            if (!File.Exists(path))
                return new UserProfile { Name = name };
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserProfile>(json, JsonOptions) ?? new UserProfile { Name = name };
        }
        catch (Exception ex)
        {
            _log.Error("config", $"Could not load profile '{name}'.", ex);
            return new UserProfile { Name = name };
        }
    }

    public void SaveProfile(UserProfile profile)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Profiles);
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(ProfilePath(profile.Name), json);
        }
        catch (Exception ex)
        {
            _log.Error("config", $"Could not save profile '{profile.Name}'.", ex);
        }
    }

    public IReadOnlyList<string> ListProfiles()
    {
        Directory.CreateDirectory(AppPaths.Profiles);
        return Directory.GetFiles(AppPaths.Profiles, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void ApplyProfile(AppConfig config, UserProfile profile)
    {
        config.ActiveProfile = profile.Name;
        config.Devices = profile.Devices;
        config.Mixer = profile.Mixer;
        config.Voice = profile.Voice;
        config.Soundboard = profile.Soundboard;
        config.Hotkeys = profile.Hotkeys;
    }

    public UserProfile CaptureProfile(string name, AppConfig config)
    {
        return new UserProfile
        {
            Name = name,
            Devices = config.Devices,
            Mixer = config.Mixer,
            Voice = config.Voice,
            Soundboard = config.Soundboard,
            Hotkeys = config.Hotkeys
        };
    }

    private static AppConfig Migrate(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Version))
            config.Version = "1";
        config.Audio ??= new AudioSettings();
        config.Devices ??= new DeviceSettings();
        config.Mixer ??= new MixerSettings();
        config.Voice ??= new VoiceSettings();
        config.Music ??= new MusicSettings();
        config.Soundboard ??= new SoundboardSettings();
        config.Spotify ??= new SpotifySettings();
        config.Hotkeys ??= new HotkeySettings();
        config.Appearance ??= new AppearanceSettings();
        config.Storage ??= new StorageSettings();
        config.Advanced ??= new AdvancedSettings();
        config.Startup ??= new StartupSettings();
        return config;
    }

    private static string ProfilePath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return Path.Combine(AppPaths.Profiles, name + ".json");
    }
}
