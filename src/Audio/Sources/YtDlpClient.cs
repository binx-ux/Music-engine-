using System.Diagnostics;
using System.Text;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public sealed class YtDlpClient
{
    private const string Release = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private static readonly string Curl = Path.Combine(Environment.SystemDirectory, "curl.exe");
    private static readonly string[] CleanQueries =
    [
        "ytsearch10:clean rap radio edit official audio",
        "ytsearch8:clean hip hop radio edit official audio",
        "ytsearch8:pg rap radio version official audio"
    ];

    private readonly AppLog _log;

    public YtDlpClient(AppLog log) => _log = log;

    public string ExePath => Path.Combine(AppPaths.Root, "tools", "yt-dlp.exe");
    public string MusicDir => Path.Combine(AppPaths.Root, "Music");

    public async Task<Result> EnsureAsync(CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ExePath)!);
            if (File.Exists(ExePath)
                && new FileInfo(ExePath).Length > 1_000_000
                && File.GetLastWriteTimeUtc(ExePath) > DateTime.UtcNow.AddDays(-14))
                return Result.Ok();

            var tmp = ExePath + ".part";
            var psi = new ProcessStartInfo
            {
                FileName = File.Exists(Curl) ? Curl : "curl.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-sL");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(tmp);
            psi.ArgumentList.Add("--max-time");
            psi.ArgumentList.Add("120");
            psi.ArgumentList.Add(Release);
            using var proc = Process.Start(psi);
            if (proc is null)
                return Result.Fail("Could not start the downloader.");
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0 || !File.Exists(tmp) || new FileInfo(tmp).Length < 1_000_000)
            {
                TryDelete(tmp);
                if (File.Exists(ExePath) && new FileInfo(ExePath).Length > 1_000_000)
                    return Result.Ok();
                return Result.Fail("Could not install yt-dlp. Check your internet connection.");
            }
            File.Move(tmp, ExePath, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Warning("music", "yt-dlp install failed.", ex.Message);
            if (File.Exists(ExePath) && new FileInfo(ExePath).Length > 1_000_000)
                return Result.Ok();
            return Result.Fail("Could not install yt-dlp.", ex.Message);
        }
    }

    public async Task<Result<List<string>>> DownloadAsync(string urlOrQuery, string destDir, int maxFiles, CancellationToken ct)
    {
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<string>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        Directory.CreateDirectory(destDir);
        CleanupParts(destDir);
        var before = Directory.GetFiles(destDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var template = Path.Combine(destDir, "%(title).80B.%(ext)s");
        var args = new List<string>
        {
            "-f", "140/bestaudio[ext=m4a]/bestaudio[ext=mp3]/bestaudio",
            "--ignore-errors",
            "--no-warnings",
            "--no-progress",
            "--windows-filenames",
            "--print", "after_move:filepath",
            "--playlist-end", Math.Clamp(maxFiles, 1, 20).ToString(),
            "-o", template
        };
        if (!LooksLikeSearch(urlOrQuery))
            args.Add("--no-playlist");
        args.Add(urlOrQuery);

        var run = await RunAsync(args, ct);
        var files = ParsePrintedFiles(run.Out)
            .Where(AudioFileSupport.IsSupportedFile)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            files = Directory.GetFiles(destDir)
                .Where(p => !before.Contains(p) && AudioFileSupport.IsSupportedFile(p) && new FileInfo(p).Length > 80_000)
                .ToList();
        }
        if (files.Count == 0)
        {
            _log.Warning("music", "yt-dlp produced no audio.", Trim(run.Err + "\n" + run.Out));
            return Result<List<string>>.Fail("Nothing downloaded from that link.", Trim(run.Err));
        }
        return Result<List<string>>.Ok(files);
    }

    public Task<Result<List<string>>> DownloadLinkAsync(string url, CancellationToken ct)
        => DownloadAsync(url, Path.Combine(MusicDir, "Downloads"), 1, ct);

    public async Task<Result<List<string>>> FindCleanRapAsync(CancellationToken ct)
    {
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<string>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        var dest = Path.Combine(MusicDir, "CleanRap");
        Directory.CreateDirectory(dest);
        CleanupParts(dest);

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in CleanQueries)
        {
            if (files.Count >= 8 || ct.IsCancellationRequested)
                break;

            var hits = await SearchAsync(query, 10, ct);
            _log.Info("music", $"Clean rap search returned {hits.Count} results.");
            foreach (var hit in hits)
            {
                if (files.Count >= 8 || ct.IsCancellationRequested)
                    break;
                if (!IsCleanSong(hit))
                    continue;

                var path = await DownloadVideoAsync(hit.Id, dest, ct);
                if (path is null || !seen.Add(path))
                    continue;
                files.Add(path);
                _log.Info("music", "Downloaded clean rap: " + Path.GetFileName(path));
            }
        }

        if (files.Count == 0)
        {
            files = Directory.GetFiles(dest)
                .Where(AudioFileSupport.IsSupportedFile)
                .Where(p => new FileInfo(p).Length > 200_000)
                .Where(p => IsCleanTitle(Path.GetFileNameWithoutExtension(p), 180))
                .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                .Take(8)
                .ToList();
        }

        if (files.Count == 0)
            return Result<List<string>>.Fail("Could not find clean rap right now. Check your internet and try again.");

        return Result<List<string>>.Ok(files);
    }

    private async Task<List<SearchHit>> SearchAsync(string query, int count, CancellationToken ct)
    {
        var run = await RunAsync(
        [
            "--flat-playlist",
            "--no-warnings",
            "--no-progress",
            "--playlist-end", count.ToString(),
            "--print", "%(id)s\t%(title)s\t%(duration)s",
            query
        ], ct);

        var hits = new List<SearchHit>();
        foreach (var line in run.Out.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                continue;
            int.TryParse(parts.Length > 2 ? parts[2] : "", out var duration);
            hits.Add(new SearchHit(parts[0].Trim(), parts[1].Trim(), duration));
        }
        if (hits.Count == 0 && !string.IsNullOrWhiteSpace(run.Err))
            _log.Warning("music", "Clean rap search failed.", Trim(run.Err));
        return hits;
    }

    private async Task<string?> DownloadVideoAsync(string id, string destDir, CancellationToken ct)
    {
        var url = "https://www.youtube.com/watch?v=" + id;
        var template = Path.Combine(destDir, "%(title).80B.%(ext)s");
        var before = Directory.GetFiles(destDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var run = await RunAsync(
        [
            "-f", "140/bestaudio[ext=m4a]/bestaudio[ext=mp3]/bestaudio",
            "--no-playlist",
            "--ignore-errors",
            "--no-warnings",
            "--no-progress",
            "--windows-filenames",
            "--print", "after_move:filepath",
            "-o", template,
            url
        ], ct);

        var printed = ParsePrintedFiles(run.Out).FirstOrDefault(p => File.Exists(p) && AudioFileSupport.IsSupportedFile(p));
        if (printed is not null && new FileInfo(printed).Length > 80_000)
            return printed;

        return Directory.GetFiles(destDir)
            .Where(p => !before.Contains(p) && AudioFileSupport.IsSupportedFile(p) && new FileInfo(p).Length > 80_000)
            .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
            .FirstOrDefault();
    }

    private async Task<(int Code, string Out, string Err)> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var tools = Path.GetDirectoryName(ExePath);
        if (!string.IsNullOrEmpty(tools))
            psi.Environment["PATH"] = tools + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi);
        if (proc is null)
            return (-1, "", "Could not start yt-dlp.");

        var stdout = proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = proc.StandardError.ReadToEndAsync(ct);
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            throw;
        }

        return (proc.ExitCode, await stdout, await stderr);
    }

    private static bool LooksLikeSearch(string text)
        => text.StartsWith("ytsearch", StringComparison.OrdinalIgnoreCase);

    private static bool IsCleanSong(SearchHit hit)
        => IsCleanTitle(hit.Title, hit.Duration);

    private static bool IsCleanTitle(string title, int duration)
    {
        if (duration is > 0 and < 70 or > 420)
            return false;
        if (ContainsAny(title, "playlist", "hours", "compilation", "livestream", "live stream", "full album", "video mix", "hip hop mix", "rap mix"))
            return false;
        if (ContainsAny(title, "explicit") && !ContainsAny(title, "clean", "radio", "pg"))
            return false;
        return true;
    }

    private static bool ContainsAny(string text, params string[] words)
    {
        foreach (var w in words)
        {
            if (text.Contains(w, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static List<string> ParsePrintedFiles(string output)
    {
        var files = new List<string>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var path = line.Trim().Trim('"');
            if (path.Length >= 3 && path[1] == ':' && (path.Contains('\\') || path.Contains('/')))
                files.Add(path);
        }
        return files;
    }

    private static void CleanupParts(string dir)
    {
        try
        {
            foreach (var part in Directory.GetFiles(dir, "*.part"))
                TryDelete(part);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static string Trim(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        text = text.Trim();
        return text.Length > 400 ? text[..400] : text;
    }

    private readonly record struct SearchHit(string Id, string Title, int Duration);
}
