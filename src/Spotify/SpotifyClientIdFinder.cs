using System.Text.RegularExpressions;
using Mixline.Core;

namespace Mixline.Spotify;

public static class SpotifyClientIdFinder
{
    private static readonly Regex BareHex = new(
        @"(?i)client[_-]?id[""']?\s*[:=]\s*[""']([0-9a-f]{32})[""']",
        RegexOptions.Compiled);

    public static string Command
        => "powershell -NoProfile -ExecutionPolicy Bypass -File \"$env:APPDATA\\Cuebox\\find-spotify-id.ps1\"";

    public static string? Find()
    {
        var env = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID")
            ?? Environment.GetEnvironmentVariable("SPOTIFY_ID");
        if (IsId(env))
            return env!.Trim();

        var hits = new List<(string Id, int Score)>();
        foreach (var file in CandidateFiles())
            Scan(file, hits);

        var found = Path.Combine(AppPaths.Root, "found-spotify-id.txt");
        if (File.Exists(found))
        {
            var t = File.ReadAllText(found).Trim();
            if (IsId(t))
                hits.Add((t, 40));
        }

        return hits
            .GroupBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Id: g.Key, Score: g.Sum(x => x.Score)))
            .OrderByDescending(x => x.Score)
            .Select(x => x.Id)
            .FirstOrDefault();
    }

    public static void SaveFound(string id)
    {
        Directory.CreateDirectory(AppPaths.Root);
        File.WriteAllText(Path.Combine(AppPaths.Root, "found-spotify-id.txt"), id);
    }

    public static bool IsId(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^[0-9a-fA-F]{32}$");

    private static IEnumerable<string> CandidateFiles()
    {
        var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folders = new[]
        {
            AppPaths.Root,
            Path.Combine(app, "Mixline"),
            Path.Combine(app, "spicetify"),
            app,
            local,
            home
        };

        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(folder))
                continue;
            foreach (var file in SafeFiles(folder, 0, folder == app || folder == local ? 1 : 2))
                yield return file;
        }
    }

    private static IEnumerable<string> SafeFiles(string dir, int depth, int maxDepth)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (ext is ".json" or ".env" or ".config" or ".ini" or ".txt")
                yield return file;
        }

        if (depth >= maxDepth)
            yield break;

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(dir);
        }
        catch
        {
            yield break;
        }

        foreach (var child in dirs)
        {
            var name = Path.GetFileName(child);
            if (name is "node_modules" or ".git" or "Cache" or "Code Cache" or "GPUCache" or "Temp" or "Packages")
                continue;
            foreach (var file in SafeFiles(child, depth + 1, maxDepth))
                yield return file;
        }
    }

    private static void Scan(string file, List<(string Id, int Score)> hits)
    {
        try
        {
            if (new FileInfo(file).Length > 1_500_000)
                return;
            var text = File.ReadAllText(file);
            var score = file.Contains("spotify", StringComparison.OrdinalIgnoreCase)
                || text.Contains("spotify", StringComparison.OrdinalIgnoreCase)
                ? 12 : 2;
            foreach (Match m in BareHex.Matches(text))
            {
                var id = m.Groups[1].Value;
                if (IsId(id))
                    hits.Add((id, score));
            }
        }
        catch
        {
        }
    }
}
