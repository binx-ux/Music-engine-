using System.IO;
using System.Net.Http;
using System.Text.Json;
using Mixline.Core;

namespace Mixline.App;

public sealed class UpdateOffer
{
    public string Version { get; init; } = "";
    public string Tag { get; init; } = "";
    public string Notes { get; init; } = "";
    public string SetupUrl { get; init; } = "";
    public UpdateKind Kind { get; init; }
    public bool CanSkip => Kind is UpdateKind.Minor or UpdateKind.Fix;
}

public static class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(8) };

    public static async Task<UpdateOffer?> FindAsync(string? skipped, bool ignoreSkip, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, AppInfo.ReleasesApi);
            req.Headers.TryAddWithoutValidation("User-Agent", "Cuebox/" + AppInfo.Version);
            req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
            using var res = await Http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                return null;
            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            if (!AppVersion.TryParse(tag, out var maj, out var min, out var pat))
                return null;
            var version = AppVersion.Canonical(maj, min, pat);
            var kind = AppVersion.Kind(AppInfo.Version, version);
            if (kind == UpdateKind.None)
                return null;
            if (!ignoreSkip && kind != UpdateKind.Major
                && string.Equals(skipped, version, StringComparison.OrdinalIgnoreCase))
                return null;
            var url = SetupUrl(root);
            if (string.IsNullOrEmpty(url))
                return null;
            var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            return new UpdateOffer
            {
                Version = version,
                Tag = tag,
                Notes = Notes(body),
                SetupUrl = url,
                Kind = kind
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<Result> InstallAsync(UpdateOffer offer, IProgress<double>? progress, CancellationToken ct)
    {
        try
        {
            var setup = Path.Combine(Path.GetTempPath(), "CueboxSetup.exe");
            using var req = new HttpRequestMessage(HttpMethod.Get, offer.SetupUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "Cuebox/" + AppInfo.Version);
            using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
                return Result.Fail("Could not download the update.", ((int)res.StatusCode).ToString());
            var total = res.Content.Headers.ContentLength ?? 0;
            await using (var input = await res.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(setup))
            {
                var buf = new byte[64 * 1024];
                long read = 0;
                int n;
                while ((n = await input.ReadAsync(buf, ct)) > 0)
                {
                    await output.WriteAsync(buf.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0)
                        progress?.Report(read / (double)total);
                }
            }
            progress?.Report(1);

            var exe = Environment.ProcessPath ?? "";
            var args = "/c start \"\" /wait \"" + setup + "\" /SILENT /CLOSEAPPLICATIONS";
            if (!string.IsNullOrWhiteSpace(exe))
                args += " & start \"\" \"" + exe + "\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail("Could not install the update.", ex.Message);
        }
    }

    public static string KindLabel(UpdateKind kind) => kind switch
    {
        UpdateKind.Major => "Major update",
        UpdateKind.Minor => "Minor update. You can skip this one.",
        UpdateKind.Fix => "Fix. You can skip this one.",
        _ => "Update"
    };

    private static string? SetupUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return null;
        string? exe = null;
        string? any = null;
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (string.IsNullOrEmpty(url))
                continue;
            if (name.Equals("CueboxSetup.exe", StringComparison.OrdinalIgnoreCase))
                return url;
            if (exe is null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exe = url;
            any ??= url;
        }
        return exe ?? any;
    }

    private static string Notes(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "New Cuebox build.";
        var lines = body.Replace("\r", "").Split('\n')
            .Select(l => l.Trim().TrimStart('#', '*', '-', ' '))
            .Where(l => l.Length > 0)
            .Take(6);
        var text = string.Join("\n", lines);
        return text.Length == 0 ? "New Cuebox build." : text;
    }
}
