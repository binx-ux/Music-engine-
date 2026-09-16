namespace Mixline.Core;

public static class AudioConstants
{
    public const int DefaultSampleRate = 48000;
    public const int InternalChannels = 2;
    public const int MaxSampleRate = 96000;
    public const int MinSampleRate = 44100;
    public const float PeakClip = 0.999f;
    public const float DefaultLimiterCeiling = 0.95f;
    public const float MeterFloorDb = -60f;
    public const int MeterUiHz = 30;
    public const float MaxGain = 2f;
    public const float MinGain = 0f;
}

public enum BufferPreset
{
    LowLatency = 0,
    Balanced = 1,
    Stable = 2,
    Custom = 3
}

public enum MixChannelId
{
    Mic = 0,
    Music = 1,
    Soundboard = 2,
    Master = 3
}

public enum LoopMode
{
    Off = 0,
    One = 1,
    All = 2
}

public enum AutotuneMode
{
    Off = 0,
    Light = 1,
    Medium = 2,
    Strong = 3
}

public enum MusicalScale
{
    Chromatic = 0,
    Major = 1,
    Minor = 2
}

public enum ShareModeSetting
{
    Shared = 0,
    Exclusive = 1
}

public static class BufferPresetValues
{
    public static int Milliseconds(BufferPreset preset) => preset switch
    {
        BufferPreset.LowLatency => 16,
        BufferPreset.Balanced => 32,
        BufferPreset.Stable => 64,
        _ => 20
    };
}
