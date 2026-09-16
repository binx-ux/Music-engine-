using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public sealed class YtDlpClient
{
    private const string Release = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private static readonly string Curl = Path.Combine(Environment.SystemDirectory, "curl.exe");

    private readonly AppLog _log;

    public YtDlpClient(AppLog log) => _log = log;

    public string ExePath => Path.Combine(AppPaths.Root, "tools", "yt-dlp.exe");
    public string MusicDir => Path.Combine(AppPaths.Root, "Music");

    public async Task<Result> EnsureAsync(CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ExePath)!);
            if (File.Exists(ExePath) && new FileInfo(ExePath).Length > 1_000_000)
                return Result.Ok();

            var tmp = ExePath + ".part";
            var psi = new System.Diagnostics.ProcessStartInfo
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
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return Result.Fail("Could not start the downloader.");
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0 || !File.Exists(tmp) || new FileInfo(tmp).Length < 1_000_000)
            {
                TryDelete(tmp);
                return Result.Fail("Could not install yt-dlp. Check your internet connection.");
            }
            File.Move(tmp, ExePath, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Warning("music", "yt-dlp install failed.", ex.Message);
            return Result.Fail("Could not install yt-dlp.", ex.Message);
        }
    }

    public async Task<Result<List<string>>> DownloadAsync(string urlOrQuery, string destDir, int maxFiles, CancellationToken ct)
    {
        var ready = await EnsureAsync(ct);
        if (!ready.Success)
            return Result<List<string>>.Fail(ready.Error ?? "yt-dlp is missing.", ready.Details);

        Directory.CreateDirectory(destDir);
        var before = Directory.GetFiles(destDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var template = Path.Combine(destDir, "%(title).80B.%(ext)s");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ExePath,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("bestaudio[ext=m4a]/bestaudio[ext=mp3]/bestaudio[ext=wav]/bestaudio/best");
        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add("--newline");
        psi.ArgumentList.Add("--max-downloads");
        psi.ArgumentList.Add(Math.Clamp(maxFiles, 1, 20).ToString());
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(template);
        psi.ArgumentList.Add(urlOrQuery);

        try
        {
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return Result<List<string>>.Fail("Could not start yt-dlp.");
            var err = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var files = Directory.GetFiles(destDir)
                .Where(p => !before.Contains(p) && AudioFileSupport.IsSupportedFile(p))
                .ToList();
            if (files.Count == 0)
            {
                files = Directory.GetFiles(destDir)
                    .Where(AudioFileSupport.IsSupportedFile)
                    .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                    .Take(maxFiles)
                    .ToList();
            }
            if (files.Count == 0)
            {
                _log.Warning("music", "yt-dlp produced no audio.", err);
                return Result<List<string>>.Fail("Nothing downloaded from that link.", Trim(err));
            }
            return Result<List<string>>.Ok(files);
        }
        catch (Exception ex)
        {
            _log.Warning("music", "yt-dlp failed.", ex.Message);
            return Result<List<string>>.Fail("Download failed.", ex.Message);
        }
    }

    public Task<Result<List<string>>> DownloadLinkAsync(string url, CancellationToken ct)
        => DownloadAsync(url, Path.Combine(MusicDir, "Downloads"), 1, ct);

    public Task<Result<List<string>>> FindCleanRapAsync(CancellationToken ct)
        => DownloadAsync(
            "ytsearch8:clean rap radio edit hip hop no explicit",
            Path.Combine(MusicDir, "CleanRap"),
            8,
            ct);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static string Trim(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        text = text.Trim();
        return text.Length > 240 ? text[..240] : text;
    }
}
