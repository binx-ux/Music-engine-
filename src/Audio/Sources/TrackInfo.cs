using Mixline.Core;

namespace Mixline.Audio.Sources;

public sealed record TrackInfo
{
    public required string Path { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string FileName { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public string Length => Duration.TotalSeconds < 1
        ? ""
        : Duration.TotalHours >= 1
            ? $"{(int)Duration.TotalHours}:{Duration.Minutes:00}:{Duration.Seconds:00}"
            : $"{(int)Duration.TotalMinutes}:{Duration.Seconds:00}";
    public byte[]? Artwork { get; init; }
    public bool IsUrl { get; init; }
    public string? SourceUrl { get; init; }
}

public interface IAudioSource : IDisposable
{
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    TrackInfo? Track { get; }
    int Read(Span<float> stereo, int frames);
    void Seek(TimeSpan position);
}
