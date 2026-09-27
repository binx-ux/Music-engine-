using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public sealed class YtDlpClient
{
    private const string Release = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string FfmpegZip = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
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
    public string FfmpegPath => Path.Combine(AppPaths.Root, "tools", "ffmpeg.exe");
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

    private bool FfmpegReady()
        => File.Exists(FfmpegPath) && new FileInfo(FfmpegPath).Length > 1_000_000;

    private async Task EnsureFfmpegAsync(CancellationToken ct)
    {
        if (FfmpegReady())
            return;
        var zip = FfmpegPath + ".zip";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FfmpegPath)!);
            var psi = new ProcessStartInfo
            {
                FileName = File.Exists(Curl) ? Curl : "curl.exe",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-sL");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(zip);
            psi.ArgumentList.Add("--max-time");
            psi.ArgumentList.Add("300");
            psi.ArgumentList.Add(FfmpegZip);
            using var proc = Process.Start(psi);
            if (proc is null)
                return;
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0 || !File.Exists(zip) || new FileInfo(zip).Length < 1_000_000)
            {
                TryDelete(zip);
                return;
            }

            using (var archive = ZipFile.OpenRead(zip))
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                    return;
                entry.ExtractToFile(FfmpegPath, true);
            }
        }
        catch (Exception ex)
        {
            _log.Warning("music", "ffmpeg install failed.", ex.Message);
        }
        finally
        {
            TryDelete(zip);
        }
    }

    private void AddQualityArgs(List<string> args)
    {
        if (FfmpegReady())
        {
            args.Add("--ffmpeg-location");
            args.Add(Path.GetDirectoryName(FfmpegPath)!);
            args.Add("-f");
            args.Add("bestaudio/best");
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("flac");
            args.Add("--audio-quality");
            args.Add("0");
            return;
        }

        args.Add("-f");
        args.Add("bestaudio[ext=m4a]/bestaudio[acodec^=mp4a]/140");
    }

    public async Task<Result<List<string>>> DownloadAsync(string urlOrQuery, string destDir, int maxFiles, CancellationToken ct)
    {
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<string>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        Directory.CreateDirectory(destDir);
        CleanupParts(destDir);
        await EnsureFfmpegAsync(ct);
        var before = Directory.GetFiles(destDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var template = Path.Combine(destDir, "%(title).80B.%(ext)s");
        var args = new List<string>
        {
            "--ignore-errors",
            "--no-warnings",
            "--no-progress",
            "--windows-filenames",
            "--print", "after_move:filepath",
            "--playlist-end", Math.Clamp(maxFiles, 1, 20).ToString(),
            "-o", template
        };
        AddQualityArgs(args);
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

    public async Task<Result<List<SongHit>>> SearchSongsAsync(string query, int count, CancellationToken ct)
    {
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<SongHit>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        if (query == null)
            query = "";
        query = query.Trim();
        if (query.Length == 0)
            return Result<List<SongHit>>.Fail("Type a song name.");

        // yt-dlp gets weird past ~20
        if (count < 1)
            count = 1;
        if (count > 20)
            count = 20;

        string q;
        if (LooksLikeSearch(query))
            q = query;
        else
            q = "ytsearch" + count + ":" + query;

        var hits = await SearchAsync(q, count, ct);
        if (hits == null)
            hits = new List<SongHit>();
        if (hits.Count == 0)
            return Result<List<SongHit>>.Fail("Nothing matched. Try a different name.");
        return Result<List<SongHit>>.Ok(hits);
    }

    public Task<string?> DownloadIdAsync(string id, CancellationToken ct)
        => DownloadVideoAsync(id, Path.Combine(MusicDir, "Downloads"), ct);

    public Task<Result<List<string>>> DownloadLinkAsync(string url, CancellationToken ct)
        => DownloadAsync(url, Path.Combine(MusicDir, "Downloads"), 1, ct);

    public async Task<Result<List<string>>> FindCleanRapAsync(CancellationToken ct)
    {
        // tries a few search strings then falls back to whatever is already in CleanRap
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<string>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        var dest = Path.Combine(MusicDir, "CleanRap");
        try
        {
            Directory.CreateDirectory(dest);
        }
        catch
        {
            // rare, but just keep going with whatever path we have
        }
        CleanupParts(dest);

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var want = 8;

        for (var qi = 0; qi < CleanQueries.Length; qi++)
        {
            if (files.Count >= want)
                break;
            if (ct.IsCancellationRequested)
                break;

            var query = CleanQueries[qi];
            var hits = await SearchAsync(query, 10, ct);
            if (hits == null)
                continue;

            _log.Info("music", "Clean rap search returned " + hits.Count + " results.");

            for (var hi = 0; hi < hits.Count; hi++)
            {
                if (files.Count >= want)
                    break;
                if (ct.IsCancellationRequested)
                    break;

                var hit = hits[hi];
                if (hit == null)
                    continue;
                if (!IsCleanSong(hit))
                    continue;
                if (string.IsNullOrWhiteSpace(hit.Id))
                    continue;

                var path = await DownloadVideoAsync(hit.Id, dest, ct);
                if (path == null)
                    continue;
                if (!File.Exists(path))
                    continue;
                if (!seen.Add(path))
                    continue;

                files.Add(path);
                _log.Info("music", "Downloaded clean rap: " + Path.GetFileName(path));
            }
        }

        // if yt-dlp flaked, use leftover files from last time
        if (files.Count == 0 && Directory.Exists(dest))
        {
            var leftovers = Directory.GetFiles(dest);
            var picked = new List<string>();
            for (var i = 0; i < leftovers.Length; i++)
            {
                var p = leftovers[i];
                if (!AudioFileSupport.IsSupportedFile(p))
                    continue;
                try
                {
                    if (new FileInfo(p).Length <= 200_000)
                        continue;
                }
                catch
                {
                    continue;
                }
                var name = Path.GetFileNameWithoutExtension(p);
                if (!IsCleanTitle(name, 180))
                    continue;
                picked.Add(p);
            }

            picked.Sort((a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            var take = want;
            if (picked.Count < take)
                take = picked.Count;
            for (var i = 0; i < take; i++)
                files.Add(picked[i]);
        }

        if (files.Count == 0)
            return Result<List<string>>.Fail("Could not find clean rap right now. Check your internet and try again.");

        return Result<List<string>>.Ok(files);
    }

    private async Task<List<SongHit>> SearchAsync(string query, int count, CancellationToken ct)
    {
        // flat playlist print is way faster than downloading for seach
        var hits = new List<SongHit>();
        if (string.IsNullOrWhiteSpace(query))
            return hits;

        if (count < 1)
            count = 1;
        if (count > 25)
            count = 25;

        var run = await RunAsync(
        [
            "--flat-playlist",
            "--no-warnings",
            "--no-progress",
            "--playlist-end", count.ToString(),
            "--print", "%(id)s\t%(title)s\t%(uploader)s\t%(duration)s",
            query
        ], ct);

        var lines = run.Out.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('\t');
            if (parts.Length < 2)
                continue;

            var id = parts[0].Trim();
            if (id.Length == 0)
                continue;

            var title = parts[1].Trim();
            var channel = "";
            if (parts.Length > 2)
                channel = parts[2].Trim();

            // yt-dlp puts NA when its missing
            if (channel == "NA" || channel == "None" || channel == "null")
                channel = "";

            var duration = 0;
            if (parts.Length > 3)
            {
                var raw = parts[3].Trim();
                int.TryParse(raw, out duration);
            }

            // skip weird blanks
            if (string.IsNullOrWhiteSpace(title))
                title = id;

            hits.Add(new SongHit
            {
                Id = id,
                Title = title,
                Channel = channel,
                Duration = duration
            });
        }

        if (hits.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(run.Err))
                _log.Warning("music", "Search failed.", Trim(run.Err));
        }

        return hits;
    }

    private async Task<string?> DownloadVideoAsync(string id, string destDir, CancellationToken ct)
    {
        var url = "https://www.youtube.com/watch?v=" + id;
        var template = Path.Combine(destDir, "%(title).80B.%(ext)s");
        var before = Directory.GetFiles(destDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await EnsureFfmpegAsync(ct);
        var args = new List<string>
        {
            "--no-playlist",
            "--ignore-errors",
            "--no-warnings",
            "--no-progress",
            "--windows-filenames",
            "--print", "after_move:filepath",
            "-o", template
        };
        AddQualityArgs(args);
        args.Add(url);
        var run = await RunAsync(args, ct);

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

    private static bool IsCleanSong(SongHit hit)
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
}

public sealed class SongHit
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Channel { get; init; } = "";
    public int Duration { get; init; }
    public string Length => Duration < 1 ? "" : $"{Duration / 60}:{Duration % 60:00}";
    public string Subtitle => string.IsNullOrWhiteSpace(Channel) ? Length : Channel + (Length.Length == 0 ? "" : "  ·  " + Length);
}
