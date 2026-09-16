using Mixline.Core;

namespace Mixline.Audio.Effects;

public static class VoiceEffectPresets
{
    public static VoiceSettings Streaming()
    {
        return new VoiceSettings
        {
            VoiceEnhance = true,
            HighPass = true,
            HighPassHz = 80,
            Gate = true,
            Eq = true,
            EqPreset = "Streamer",
            EqSettings = DSP.EqPresets.Create("Streamer"),
            Compressor = true,
            Limiter = true
        };
    }
}
