using System.Net.Http;
using System.Text.Json;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public sealed class GitHubPlaylist
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly string[] PlaylistNames =
    [
        "cuebox.json", "playlist.json", "playlist.m3u", "playlist.m3u8",
        "playlist.txt", "tracks.txt", "songs.txt"
    ];
    private static readonly string[] MusicDirs = ["music", "playlist", "tracks", "songs", "audio"];

    private readonly AppLog _log;
    private readonly YtDlpClient _ytdlp;

    public GitHubPlaylist(AppLog log, YtDlpClient ytdlp)
    {
        _log = log;
        _ytdlp = ytdlp;
    }

    public static bool LooksLike(string text)
    {
        text = (text ?? "").Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        var slash = text.IndexOf('/');
        return slash > 0 && slash == text.LastIndexOf('/') && text.Length < 100 && !text.Contains(' ');
    }

    public async Task<Result<List<TrackInfo>>> LoadAsync(string text, CancellationToken ct)
    {
        if (!TryParse(text, out var owner, out var repo, out var branch, out var path))
            return Result<List<TrackInfo>>.Fail("Paste a GitHub repo like github.com/you/playlist.");

        try
        {
            branch ??= await DefaultBranch(owner, repo, ct);
            if (string.IsNullOrWhiteSpace(branch))
                return Result<List<TrackInfo>>.Fail("That GitHub repo could not be opened.");

            var tracks = new List<TrackInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(path))
            {
                var item = await GetContent(owner, repo, path, branch, ct);
                if (item is not null)
                    await Collect(owner, repo, branch, item, tracks, seen, 0, false, ct);
            }
            else
            {
                var root = await ListDir(owner, repo, "", branch, ct);
                foreach (var item in root)
                    await Collect(owner, repo, branch, item, tracks, seen, 0, true, ct);
            }

            if (tracks.Count == 0)
                return Result<List<TrackInfo>>.Fail("No playlist or audio files found in that repo. Add cuebox.json, playlist.m3u, or mp3/m4a files.");

            return Result<List<TrackInfo>>.Ok(tracks);
        }
        catch (Exception ex)
        {
            _log.Warning("music", "GitHub playlist failed.", ex.Message);
            return Result<List<TrackInfo>>.Fail("Could not load that GitHub playlist.", ex.Message);
        }
    }

    public static string ExportJson(IEnumerable<TrackInfo> queue)
    {
        var tracks = queue.Select(t => new Dictionary<string, string?>
        {
            ["title"] = t.Title,
            ["artist"] = string.IsNullOrWhiteSpace(t.Artist) ? null : t.Artist,
            ["url"] = string.IsNullOrWhiteSpace(t.SourceUrl) ? (t.IsUrl ? t.Path : null) : t.SourceUrl
        }).Where(d => !string.IsNullOrWhiteSpace(d["url"])).ToList();

        return JsonSerializer.Serialize(new { name = "Cuebox playlist", tracks },
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
    }

    private async Task Collect(string owner, string repo, string branch, GhItem item, List<TrackInfo> tracks, HashSet<string> seen, int depth, bool fromRoot, CancellationToken ct)
    {
        if (tracks.Count >= 40)
            return;

        if (item.Type == "dir")
        {
            if (depth >= 2)
                return;
            if (fromRoot && depth == 0 && !MusicDirs.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                return;
            foreach (var child in await ListDir(owner, repo, item.Path, branch, ct))
                await Collect(owner, repo, branch, child, tracks, seen, depth + 1, fromRoot, ct);
            return;
        }

        if (item.Type != "file")
            return;

        if (IsPlaylistFile(item.Name))
        {
            var body = await DownloadText(item.DownloadUrl ?? RawUrl(owner, repo, branch, item.Path), ct);
            if (!string.IsNullOrWhiteSpace(body))
                await ParsePlaylist(owner, repo, branch, body, item.Name, tracks, seen, ct);
            return;
        }

        if (!AudioFileSupport.IsSupportedFile(item.Name))
            return;

        var url = item.DownloadUrl ?? RawUrl(owner, repo, branch, item.Path);
        if (!seen.Add(url))
            return;
        tracks.Add(new TrackInfo
        {
            Path = url,
            Title = Path.GetFileNameWithoutExtension(item.Name),
            FileName = item.Name,
            IsUrl = true,
            SourceUrl = url
        });
    }

    private async Task ParsePlaylist(string owner, string repo, string branch, string body, string name, List<TrackInfo> tracks, HashSet<string> seen, CancellationToken ct)
    {
        var ext = Path.GetExtension(name);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var url in JsonUrls(doc.RootElement, owner, repo, branch))
                await AddEntry(url, tracks, seen, ct);
            return;
        }

        foreach (var line in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#') || t.StartsWith("//"))
                continue;
            if (t.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                continue;
            await AddEntry(ResolvePath(owner, repo, branch, t), tracks, seen, ct);
        }
    }

    private async Task AddEntry(string url, List<TrackInfo> tracks, HashSet<string> seen, CancellationToken ct)
    {
        if (tracks.Count >= 40 || string.IsNullOrWhiteSpace(url) || !seen.Add(url))
            return;

        if (IsYouTube(url) || url.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase))
        {
            var got = await _ytdlp.DownloadLinkAsync(url, ct);
            if (!got.Success || got.Value is null)
                return;
            foreach (var file in got.Value)
            {
                if (!seen.Add(file))
                    continue;
                tracks.Add(AudioFileSupport.ReadMetadata(file) with { SourceUrl = url });
            }
            return;
        }

        var title = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(uri.AbsolutePath))
            : url;
        if (string.IsNullOrWhiteSpace(title))
            title = "Track";
        tracks.Add(new TrackInfo
        {
            Path = url,
            Title = title,
            FileName = title,
            IsUrl = true,
            SourceUrl = url
        });
    }

    private static IEnumerable<string> JsonUrls(JsonElement root, string owner, string repo, string branch)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in root.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                    yield return ResolvePath(owner, repo, branch, el.GetString() ?? "");
                else
                {
                    var u = TrackUrl(el, owner, repo, branch);
                    if (u is not null)
                        yield return u;
                }
            }
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("tracks", out var tracks) || root.TryGetProperty("songs", out tracks) || root.TryGetProperty("urls", out tracks))
        {
            foreach (var u in JsonUrls(tracks, owner, repo, branch))
                yield return u;
        }
    }

    private static string? TrackUrl(JsonElement el, string owner, string repo, string branch)
    {
        foreach (var key in new[] { "url", "src", "link", "path", "file" })
        {
            if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                return ResolvePath(owner, repo, branch, v.GetString() ?? "");
        }
        return null;
    }

    private static string ResolvePath(string owner, string repo, string branch, string value)
    {
        value = (value ?? "").Trim().Trim('"');
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;
        value = value.Replace('\\', '/').TrimStart('/');
        return RawUrl(owner, repo, branch, value);
    }

    private async Task<string> DefaultBranch(string owner, string repo, CancellationToken ct)
    {
        using var doc = await GetJson($"https://api.github.com/repos/{owner}/{repo}", ct);
        if (doc is not null && doc.RootElement.TryGetProperty("default_branch", out var b))
            return b.GetString() ?? "main";
        return "main";
    }

    private async Task<List<GhItem>> ListDir(string owner, string repo, string path, string branch, CancellationToken ct)
    {
        var url = string.IsNullOrWhiteSpace(path)
            ? $"https://api.github.com/repos/{owner}/{repo}/contents?ref={Uri.EscapeDataString(branch)}"
            : $"https://api.github.com/repos/{owner}/{repo}/contents/{Uri.EscapeDataString(path)}?ref={Uri.EscapeDataString(branch)}";
        using var doc = await GetJson(url, ct);
        if (doc is null)
            return [];
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var one = ReadItem(doc.RootElement);
            return one is null ? [] : [one];
        }
        var list = new List<GhItem>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var item = ReadItem(el);
            if (item is not null)
                list.Add(item);
        }
        return list;
    }

    private async Task<GhItem?> GetContent(string owner, string repo, string path, string branch, CancellationToken ct)
    {
        var items = await ListDir(owner, repo, path, branch, ct);
        return items.Count == 1 ? items[0] : new GhItem("dir", Path.GetFileName(path), path, null);
    }

    private static GhItem? ReadItem(JsonElement el)
    {
        var type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var path = el.TryGetProperty("path", out var p) ? p.GetString() ?? name : name;
        var dl = el.TryGetProperty("download_url", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return new GhItem(type, name, path, dl);
    }

    private async Task<JsonDocument?> GetJson(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", "Cuebox");
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        using var res = await Http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            _log.Warning("music", "GitHub API " + (int)res.StatusCode, url);
            return null;
        }
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static async Task<string> DownloadText(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", "Cuebox");
        using var res = await Http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }

    private static HttpClient CreateClient()
        => new() { Timeout = TimeSpan.FromSeconds(25) };

    private static string RawUrl(string owner, string repo, string branch, string path)
        => $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path.TrimStart('/')}";

    private static bool IsPlaylistFile(string name)
        => PlaylistNames.Contains(name, StringComparer.OrdinalIgnoreCase)
           || name.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);

    private static bool IsYouTube(string url)
        => url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
           || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string text, out string owner, out string repo, out string? branch, out string? path)
    {
        owner = "";
        repo = "";
        branch = null;
        path = null;
        text = (text ?? "").Trim().Trim('/');
        if (text.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("git@github.com:", "https://github.com/");
        if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase) && text.Contains('/'))
            text = "https://github.com/" + text.TrimStart('/');

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;

        if (uri.Host.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            var raw = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (raw.Length < 4)
                return false;
            owner = raw[0];
            repo = raw[1];
            branch = raw[2];
            path = string.Join('/', raw.Skip(3));
            return true;
        }

        if (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;
        owner = parts[0];
        repo = parts[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];
        if (parts.Length >= 4 && (parts[2] is "tree" or "blob"))
        {
            branch = parts[3];
            if (parts.Length > 4)
                path = string.Join('/', parts.Skip(4));
        }
        return owner.Length > 0 && repo.Length > 0;
    }

    private sealed record GhItem(string Type, string Name, string Path, string? DownloadUrl);
}
