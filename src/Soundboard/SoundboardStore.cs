using System.Text.Json;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Soundboard;

public sealed class SoundboardStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppLog _log;

    public SoundboardStore(AppLog log)
    {
        _log = log;
        Directory.CreateDirectory(AppPaths.Soundboard);
    }

    public SoundboardLayout Load(string name)
    {
        var path = PathFor(name);
        try
        {
            if (!File.Exists(path))
            {
                var fresh = new SoundboardLayout { Name = name };
                Save(fresh);
                return fresh;
            }
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SoundboardLayout>(json, Json) ?? new SoundboardLayout { Name = name };
        }
        catch (Exception ex)
        {
            _log.Error("soundboard", "Could not load soundboard.", ex);
            return new SoundboardLayout { Name = name };
        }
    }

    public void Save(SoundboardLayout layout)
    {
        try
        {
            File.WriteAllText(PathFor(layout.Name), JsonSerializer.Serialize(layout, Json));
        }
        catch (Exception ex)
        {
            _log.Error("soundboard", "Could not save soundboard.", ex);
        }
    }

    public IReadOnlyList<string> List()
    {
        Directory.CreateDirectory(AppPaths.Soundboard);
        return Directory.GetFiles(AppPaths.Soundboard, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .ToArray();
    }

    private static string PathFor(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return Path.Combine(AppPaths.Soundboard, name + ".json");
    }
}
