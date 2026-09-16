using Mixline.Core;

namespace Mixline.Audio.Sources;

public sealed record TrackInfo
{
    public required string Path { get; init; }
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string FileName { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public byte[]? Artwork { get; init; }
    public bool IsUrl { get; init; }
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
