using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Soundboard;

public static class InstantPresets
{
    private const string Host = "https://www.myinstants.com";

    private static readonly (string Name, string File)[] Pack =
    [
        ("Vine Boom", "vine-boom.mp3"),
        ("Anime Wow", "anime-wow-sound-effect.mp3"),
        ("Among Us", "among-us-role-reveal-sound.mp3"),
        ("Bone Crack", "bone-crack.mp3"),
        ("SpongeBob Fail", "spongebob-fail.mp3"),
        ("Error", "error_CDOxCYm.mp3"),
        ("Metal Pipe", "metal-pipe-clang.mp3"),
        ("Undertaker Bell", "undertakers-bell_2UwFCIe.mp3"),
        ("Dun Dun Dun", "dun-dun-dun-sound-effect-brass_8nFBccR.mp3"),
        ("Ding", "ding-sound-effect_2.mp3"),
        ("Hub Intro", "hub-intro-sound.mp3"),
        ("Discord Notify", "discord-notification.mp3"),
        ("Taco Bell", "taco-bell-bong-sfx.mp3"),
        ("Punch", "punch-gaming-sound-effect-hd_RzlG1GE.mp3"),
        ("Fortnite Death", "tmp_7901-951678082.mp3"),
        ("Yippee", "yippeeeeeeeeeeeeee.mp3"),
        ("MLG Horn", "mlg-airhorn.mp3"),
        ("Censor Beep", "censor-beep-1.mp3"),
        ("Rizz", "rizz-sound-effect.mp3"),
        ("A Few Moments Later", "a-few-moments-later-sponge-bob-sfx-fun.mp3")
    ];

    public static async Task<int> InstallAsync(SoundboardLayout layout, CancellationToken ct)
    {
        var dir = Path.Combine(AppPaths.Presets, "myinstants");
        Directory.CreateDirectory(dir);
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(25);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Cuebox/1.0");

        var added = 0;
        var order = layout.Pads.Count == 0 ? 0 : layout.Pads.Max(p => p.Order) + 1;
        foreach (var (name, file) in Pack)
        {
            ct.ThrowIfCancellationRequested();
            if (layout.Pads.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var dest = Path.Combine(dir, file);
            if (!File.Exists(dest) || new FileInfo(dest).Length < 200)
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync($"{Host}/media/sounds/{file}", ct);
                    if (bytes.Length < 200)
                        continue;
                    await File.WriteAllBytesAsync(dest, bytes, ct);
                }
                catch
                {
                    continue;
                }
            }

            layout.Pads.Add(new SoundPad
            {
                Name = name,
                FilePath = dest,
                Folder = "Myinstants",
                Order = order++
            });
            added++;
        }

        if (added > 0 && !layout.Folders.Contains("Myinstants"))
            layout.Folders.Add("Myinstants");
        return added;
    }
}
