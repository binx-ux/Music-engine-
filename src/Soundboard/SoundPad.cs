namespace Mixline.Soundboard;

public sealed class SoundPad
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Sound";
    public string? FilePath { get; set; }
    public string? Icon { get; set; }
    public string Accent { get; set; } = "#D4B483";
    public float Volume { get; set; } = 1f;
    public float Pitch { get; set; } = 1f;
    public float Speed { get; set; } = 1f;
    public bool Loop { get; set; }
    public float FadeIn { get; set; }
    public float FadeOut { get; set; }
    public string? Hotkey { get; set; }
    public string Folder { get; set; } = "General";
    public int Order { get; set; }
}

public sealed class SoundboardLayout
{
    public string Name { get; set; } = "Default";
    public List<string> Folders { get; set; } = ["General"];
    public List<SoundPad> Pads { get; set; } = [];
}
