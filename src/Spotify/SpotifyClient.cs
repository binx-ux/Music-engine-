using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Spotify;

public sealed class SpotifyTokens
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public string DisplayName { get; set; } = "";
}

public sealed class SpotifyNowPlaying
{
    public bool IsPlaying { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string? ArtworkUrl { get; init; }
    public int ProgressMs { get; init; }
    public int DurationMs { get; init; }
    public string? PreviewUrl { get; init; }
    public string DeviceName { get; init; } = "";
}

public sealed class SpotifyClient
{
    public const string RedirectUri = "http://127.0.0.1:43821/callback";
    public const string RedirectUriAllowAnyPort = "http://127.0.0.1/callback";
    public const string DashboardUrl = "https://developer.spotify.com/dashboard";
    private const int PreferredPort = 43821;
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiRoot = "https://api.spotify.com/v1";
    private const string Scopes = "user-read-currently-playing user-read-playback-state user-modify-playback-state user-read-private";

    private readonly AppLog _log;
    private readonly HttpClient _http = new();
    private SpotifyTokens? _tokens;
    private string _clientId = "";

    public bool IsConnected => _tokens is not null && !string.IsNullOrEmpty(_tokens.RefreshToken);
    public string? DisplayName => _tokens?.DisplayName;
    public SpotifyTokens? Tokens => _tokens;

    public SpotifyClient(AppLog log)
    {
        _log = log;
    }

    public void LoadTokens(SpotifyTokens? tokens) => _tokens = tokens;

    public async Task<Result> ConnectAsync(string clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return Result.Fail("Add your Spotify Client ID in Settings first.");
        _clientId = clientId.Trim();

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));

        HttpListener? listener = null;
        string redirectUri;
        try
        {
            (listener, redirectUri) = BindCallback();
        }
        catch (Exception ex)
        {
            listener?.Close();
            return Result.Fail("Could not open the local login callback.", ex.Message);
        }

        var url =
            $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}" +
            $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(Scopes)}" +
            $"&code_challenge_method=S256&code_challenge={challenge}&state={state}";

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            var ctxTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(ctxTask, Task.Delay(TimeSpan.FromMinutes(3), ct));
            if (completed != ctxTask)
                return Result.Fail("Spotify login timed out.");

            var ctx = await ctxTask;
            var query = ctx.Request.QueryString;
            var html = "<html><body style='font-family:Segoe UI;background:#111;color:#eee;padding:40px'>You can close this window and return to Cuebox.</body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html";
            ctx.Response.OutputStream.Write(bytes);
            ctx.Response.OutputStream.Close();

            if (query["error"] is string err)
                return Result.Fail("Spotify login was cancelled or denied.", err);
            if (query["state"] != state)
                return Result.Fail("Spotify login state did not match.");
            var code = query["code"];
            if (string.IsNullOrEmpty(code))
                return Result.Fail("Spotify did not return an authorization code.");

            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier
            };
            using var res = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return Result.Fail("Spotify token exchange failed.", Redact(body));

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            _tokens = new SpotifyTokens
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                RefreshToken = root.GetProperty("refresh_token").GetString() ?? "",
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 30)
            };
            await FillProfileAsync(ct);
            _log.Info("spotify", "Connected to Spotify.");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Error("spotify", "Spotify login failed.", ex);
            return Result.Fail("Spotify login failed.", ex.Message);
        }
        finally
        {
            listener?.Stop();
            listener?.Close();
        }
    }

    private static (HttpListener Listener, string RedirectUri) BindCallback()
    {
        var ports = new List<int> { PreferredPort };
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        ports.Add(((System.Net.IPEndPoint)probe.LocalEndpoint).Port);
        probe.Stop();

        Exception? last = null;
        foreach (var port in ports)
        {
            var listener = new HttpListener();
            try
            {
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                return (listener, $"http://127.0.0.1:{port}/callback");
            }
            catch (Exception ex)
            {
                last = ex;
                listener.Close();
            }
        }

        throw last ?? new InvalidOperationException("No free loopback port.");
    }

    public static void OpenDashboard()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DashboardUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public static string RedirectHelp()
        => RedirectUri + Environment.NewLine + RedirectUriAllowAnyPort;

    public void Disconnect()
    {
        _tokens = null;
        _log.Info("spotify", "Disconnected from Spotify.");
    }

    public async Task<Result<SpotifyNowPlaying?>> GetNowPlayingAsync(CancellationToken ct)
    {
        var token = await EnsureTokenAsync(ct);
        if (token is null)
            return Result<SpotifyNowPlaying?>.Fail("Connect Spotify first.");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/me/player/currently-playing");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var res = await _http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NoContent)
            return Result<SpotifyNowPlaying?>.Ok(null);
        if (!res.IsSuccessStatusCode)
            return Result<SpotifyNowPlaying?>.Fail("Could not read Spotify playback.", $"{(int)res.StatusCode}");

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var item = root.TryGetProperty("item", out var it) ? it : default;
        string title = "";
        string artist = "";
        string album = "";
        string? art = null;
        string? preview = null;
        var duration = 0;
        if (item.ValueKind == JsonValueKind.Object)
        {
            title = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            duration = item.TryGetProperty("duration_ms", out var d) ? d.GetInt32() : 0;
            preview = item.TryGetProperty("preview_url", out var p) ? p.GetString() : null;
            if (item.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
                artist = string.Join(", ", artists.EnumerateArray().Select(a => a.GetProperty("name").GetString()));
            if (item.TryGetProperty("album", out var al))
            {
                album = al.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                if (al.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                    art = images[0].GetProperty("url").GetString();
            }
        }

        return Result<SpotifyNowPlaying?>.Ok(new SpotifyNowPlaying
        {
            IsPlaying = root.TryGetProperty("is_playing", out var ip) && ip.GetBoolean(),
            Title = title,
            Artist = artist,
            Album = album,
            ArtworkUrl = art,
            ProgressMs = root.TryGetProperty("progress_ms", out var pr) ? pr.GetInt32() : 0,
            DurationMs = duration,
            PreviewUrl = preview
        });
    }

    public Task<Result> PlayAsync(CancellationToken ct) => SendPlayerAsync("/me/player/play", HttpMethod.Put, ct);
    public Task<Result> PauseAsync(CancellationToken ct) => SendPlayerAsync("/me/player/pause", HttpMethod.Put, ct);
    public Task<Result> NextAsync(CancellationToken ct) => SendPlayerAsync("/me/player/next", HttpMethod.Post, ct);
    public Task<Result> PreviousAsync(CancellationToken ct) => SendPlayerAsync("/me/player/previous", HttpMethod.Post, ct);

    private async Task<Result> SendPlayerAsync(string path, HttpMethod method, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(ct);
        if (token is null)
            return Result.Fail("Connect Spotify first.");
        using var req = new HttpRequestMessage(method, ApiRoot + path);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (method == HttpMethod.Put)
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode || res.StatusCode == HttpStatusCode.NoContent)
            return Result.Ok();
        if ((int)res.StatusCode == 403)
            return Result.Fail("You need Spotify Premium, and Spotify has to be open on a device.");
        if ((int)res.StatusCode == 404)
            return Result.Fail("No active Spotify device. Open Spotify on your PC or phone first.");
        return Result.Fail("Spotify did not accept that command.", $"{(int)res.StatusCode}");
    }

    private async Task FillProfileAsync(CancellationToken ct)
    {
        var token = _tokens?.AccessToken;
        if (string.IsNullOrEmpty(token))
            return;
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/me");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            return;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _tokens!.DisplayName = doc.RootElement.TryGetProperty("display_name", out var n) ? n.GetString() ?? "" : "";
    }

    private async Task<string?> EnsureTokenAsync(CancellationToken ct)
    {
        if (_tokens is null)
            return null;
        if (_tokens.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(10))
            return _tokens.AccessToken;
        if (string.IsNullOrEmpty(_clientId))
            return _tokens.AccessToken;
        var refreshed = await RefreshAsync(_clientId, ct);
        return refreshed.Success ? _tokens.AccessToken : null;
    }

    public async Task<Result> RefreshAsync(string clientId, CancellationToken ct)
    {
        if (_tokens is null)
            return Result.Fail("Not connected.");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _tokens.RefreshToken,
            ["client_id"] = clientId
        };
        using var res = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return Result.Fail("Spotify session expired. Connect again.", Redact(body));
        using var doc = JsonDocument.Parse(body);
        _tokens.AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        _tokens.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 30);
        if (doc.RootElement.TryGetProperty("refresh_token", out var rt))
            _tokens.RefreshToken = rt.GetString() ?? _tokens.RefreshToken;
        return Result.Ok();
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Redact(string value)
        => value.Length > 180 ? value[..180] : value;
}
