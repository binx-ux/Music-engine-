namespace Mixline.Core;

public sealed class AppConfig
{
    public string Version { get; set; } = AppInfo.ConfigVersion;
    public string ActiveProfile { get; set; } = "Default";
    public AudioSettings Audio { get; set; } = new();
    public DeviceSettings Devices { get; set; } = new();
    public MixerSettings Mixer { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public MusicSettings Music { get; set; } = new();
    public SoundboardSettings Soundboard { get; set; } = new();
    public SpotifySettings Spotify { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public StorageSettings Storage { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public StartupSettings Startup { get; set; } = new();
}

public sealed class AudioSettings
{
    public int SampleRate { get; set; } = AudioConstants.DefaultSampleRate;
    public BufferPreset BufferPreset { get; set; } = BufferPreset.Stable;
    public int CustomBufferMs { get; set; } = 48;
    public ShareModeSetting ShareMode { get; set; } = ShareModeSetting.Shared;
    public bool BypassProcessing { get; set; }

    public int BufferMilliseconds()
    {
        if (BufferPreset == BufferPreset.Custom)
            return Math.Clamp(CustomBufferMs, 5, 100);
        return BufferPresetValues.Milliseconds(BufferPreset);
    }
}

public sealed class DeviceSettings
{
    public string? InputId { get; set; }
    public string? OutputId { get; set; }
    public string? VirtualOutputId { get; set; }
    public bool SetWindowsDefaultMic { get; set; } = true;
}

public sealed class MixerSettings
{
    public ChannelSettings Mic { get; set; } = new() { Volume = 1f, MonitorSend = 1f, VirtualSend = 1f };
    public ChannelSettings Music { get; set; } = new() { Volume = 0.7f, MonitorSend = 1f, VirtualSend = 0.7f };
    public ChannelSettings Soundboard { get; set; } = new() { Volume = 0.85f, MonitorSend = 1f, VirtualSend = 1f };
    public ChannelSettings Master { get; set; } = new() { Volume = 0.9f, MonitorSend = 1f, VirtualSend = 1f };
    public float MonitorVolume { get; set; } = 0.85f;
    public bool MicMonitor { get; set; } = true;
    public bool MusicMonitor { get; set; } = true;
    public bool SoundboardMonitor { get; set; } = true;
    public bool MasterMonitor { get; set; } = true;
}

public sealed class ChannelSettings
{
    public float Volume { get; set; } = 1f;
    public float Gain { get; set; } = 1f;
    public float Pan { get; set; }
    public bool Mute { get; set; }
    public bool Solo { get; set; }
    public float MonitorSend { get; set; } = 1f;
    public float VirtualSend { get; set; } = 1f;
}

public sealed class VoiceSettings
{
    public bool VoiceEnhance { get; set; }
    public bool HighPass { get; set; } = true;
    public float HighPassHz { get; set; } = 80f;
    public bool Gate { get; set; }
    public GateSettings GateSettings { get; set; } = new();
    public bool Eq { get; set; }
    public EqSettings EqSettings { get; set; } = new();
    public string EqPreset { get; set; } = "Flat";
    public bool Compressor { get; set; }
    public CompressorSettings CompressorSettings { get; set; } = new();
    public bool DeEsser { get; set; }
    public float DeEsserAmount { get; set; } = 0.35f;
    public bool NoiseReduction { get; set; }
    public float NoiseReductionAmount { get; set; } = 0.25f;
    public bool Saturation { get; set; }
    public float SaturationAmount { get; set; } = 0.12f;
    public bool Limiter { get; set; } = true;
    public AutotuneMode Autotune { get; set; } = AutotuneMode.Off;
    public AutotuneSettings AutotuneSettings { get; set; } = new();
}

public sealed class GateSettings
{
    public float ThresholdDb { get; set; } = -42f;
    public float AttackMs { get; set; } = 5f;
    public float HoldMs { get; set; } = 80f;
    public float ReleaseMs { get; set; } = 120f;
}

public sealed class EqSettings
{
    public EqBand Low { get; set; } = new() { Frequency = 80, GainDb = 0, Q = 0.7f };
    public EqBand LowMid { get; set; } = new() { Frequency = 250, GainDb = 0, Q = 1f };
    public EqBand Mid { get; set; } = new() { Frequency = 1000, GainDb = 0, Q = 1f };
    public EqBand HighMid { get; set; } = new() { Frequency = 3500, GainDb = 0, Q = 1f };
    public EqBand High { get; set; } = new() { Frequency = 10000, GainDb = 0, Q = 0.7f };
}

public sealed class EqBand
{
    public float Frequency { get; set; }
    public float GainDb { get; set; }
    public float Q { get; set; } = 1f;
}

public sealed class CompressorSettings
{
    public float ThresholdDb { get; set; } = -18f;
    public float Ratio { get; set; } = 3f;
    public float AttackMs { get; set; } = 12f;
    public float ReleaseMs { get; set; } = 80f;
    public float MakeupDb { get; set; } = 3f;
}

public sealed class AutotuneSettings
{
    public int Key { get; set; }
    public MusicalScale Scale { get; set; } = MusicalScale.Major;
    public float RetuneSpeed { get; set; } = 0.55f;
    public float Amount { get; set; } = 0.75f;
    public bool FormantPreservation { get; set; } = true;
}

public sealed class MusicSettings
{
    public bool Shuffle { get; set; }
    public LoopMode Loop { get; set; } = LoopMode.Off;
    public List<string> RecentFolders { get; set; } = [];
    public List<string> Queue { get; set; } = [];
    public int QueueIndex { get; set; }
    public string? GitHubRepo { get; set; }
}

public sealed class SoundboardSettings
{
    public float MasterVolume { get; set; } = 1f;
    public string ActiveLayout { get; set; } = "Default";
}

public sealed class SpotifySettings
{
    public string? ClientId { get; set; }
    public string RedirectUri { get; set; } = "http://127.0.0.1:43821/callback";
    public bool Connected { get; set; }
}

public sealed class HotkeySettings
{
    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AppearanceSettings
{
    public bool UseSystemBackdrop { get; set; } = true;
    public double UiScale { get; set; } = 1.0;
    public string Theme { get; set; } = "Night";
    public string AccentHex { get; set; } = "";
}

public sealed class StorageSettings
{
    public bool RememberDevices { get; set; } = true;
}

public sealed class AdvancedSettings
{
    public bool ShowDiagnosticDetails { get; set; }
    public bool TestToneOnStart { get; set; }
}

public sealed class StartupSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool StartEngineAutomatically { get; set; } = true;
}

public sealed class UserProfile
{
    public string Name { get; set; } = "Default";
    public DeviceSettings Devices { get; set; } = new();
    public MixerSettings Mixer { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public SoundboardSettings Soundboard { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
}
