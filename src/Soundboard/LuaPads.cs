using Mixline.Core;

namespace Mixline.Soundboard;

public static class LuaPads
{
    public static float Master = 1f;

    public static float Volume(SoundPad pad)
    {
        var m = Master <= 0 ? 1f : Master;
        return pad.Volume * m;
    }

    public static void Load(string path)
    {
        if (!File.Exists(path))
            return;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("master", StringComparison.OrdinalIgnoreCase))
            {
                var eq = line.IndexOf('=');
                if (eq > 0 && float.TryParse(line[(eq + 1)..].Trim(), out var v))
                    Master = Math.Clamp(v, 0f, AudioConstants.MaxGain);
            }
        }
    }
}
