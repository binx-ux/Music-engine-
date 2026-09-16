namespace Mixline.Audio.Sources;

public sealed class UrlAudioValidator
{
    private static readonly string[] AllowedSchemes = ["http", "https"];
    private static readonly string[] AudioTypes =
    [
        "audio/", "application/ogg", "application/octet-stream", "binary/octet-stream"
    ];

    public async Task<(bool Ok, string Message, string? Title)> ValidateAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return (false, "That is not a valid URL.", null);
        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            return (false, "Only http and https audio URLs are supported.", null);
        if (uri.Host.Contains("spotify.com", StringComparison.OrdinalIgnoreCase))
            return (false, "Spotify stream URLs cannot be mixed in. Use Connect on the Spotify page, or play a local file.", null);
        if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return (false, "YouTube pages are not direct audio URLs.", null);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                using var get = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getRes = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!getRes.IsSuccessStatusCode)
                    return (false, "The URL could not be loaded.", $"{(int)getRes.StatusCode} {getRes.ReasonPhrase}");
                return CheckType(getRes, uri);
            }
            return CheckType(res, uri);
        }
        catch (Exception ex)
        {
            return (false, "The URL could not be reached.", ex.Message);
        }
    }

    private static (bool Ok, string Message, string? Title) CheckType(HttpResponseMessage res, Uri uri)
    {
        var type = res.Content.Headers.ContentType?.MediaType ?? "";
        if (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            return (false, "That URL is a web page, not an audio file.", type);
        if (type.Length > 0 && !AudioTypes.Any(t => type.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
            return (false, "This URL does not look like audio.", type);
        var name = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(name))
            name = uri.Host;
        return (true, "Ready to play.", Uri.UnescapeDataString(name));
    }
}

public sealed class ToneGenerator
{
    private readonly int _sampleRate;
    private double _phase;

    public float Frequency { get; set; } = 1000f;
    public float Amplitude { get; set; } = 0.125f;
    public bool Enabled { get; set; }

    public ToneGenerator(int sampleRate) => _sampleRate = sampleRate;

    public void Read(Span<float> stereo, int frames)
    {
        if (!Enabled)
        {
            stereo[..(frames * 2)].Clear();
            return;
        }

        var step = Frequency / _sampleRate;
        for (var i = 0; i < frames; i++)
        {
            var s = Amplitude * MathF.Sin((float)(_phase * 2 * MathF.PI));
            stereo[i * 2] = s;
            stereo[i * 2 + 1] = s;
            _phase += step;
            if (_phase >= 1)
                _phase -= 1;
        }
    }
}
