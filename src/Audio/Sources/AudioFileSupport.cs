using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Audio.Sources;

public static class AudioFileSupport
{
    public static readonly string[] Extensions =
    [
        ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma"
    ];

    public static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path);
        return Extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static TrackInfo ReadMetadata(string path)
    {
        var info = new TrackInfo
        {
            Path = path,
            FileName = Path.GetFileName(path),
            Title = Path.GetFileNameWithoutExtension(path),
            Artist = ""
        };

        try
        {
            using var file = TagLib.File.Create(path);
            if (!string.IsNullOrWhiteSpace(file.Tag.Title))
                info = info with { Title = file.Tag.Title };
            if (file.Tag.Performers is { Length: > 0 })
                info = info with { Artist = string.Join(", ", file.Tag.Performers) };
            info = info with { Duration = file.Properties.Duration };
            if (file.Tag.Pictures is { Length: > 0 })
                info = info with { Artwork = file.Tag.Pictures[0].Data.Data };
        }
        catch
        {
        }

        return info;
    }
}

public sealed class DecodedClip
{
    public required float[] Samples { get; init; }
    public required int SampleRate { get; init; }
    public required TrackInfo Track { get; init; }

    public int Frames => Samples.Length / 2;
}

public static class ClipLoader
{
    public static Result<DecodedClip> Load(string path, int engineRate, AppLog log, int maxSeconds = 120)
    {
        try
        {
            using var source = new StreamedSource(log, engineRate);
            var opened = source.Open(path, false, false);
            if (!opened.Success)
                return Result<DecodedClip>.Fail(opened.Error ?? "Could not load sound.", opened.Details);

            var duration = source.Duration;
            if (duration.TotalSeconds > maxSeconds)
                return Result<DecodedClip>.Fail("Sound is too long to load into the soundboard.");

            var frames = Math.Max(1, (int)Math.Ceiling(Math.Max(duration.TotalSeconds, 0.2) * engineRate) + engineRate);
            var data = new float[frames * 2];
            var written = 0;
            var temp = new float[2048];
            var spins = 0;
            while (written < frames && spins < 2000)
            {
                var n = source.Read(temp, temp.Length / 2);
                if (n <= 0)
                {
                    if (source.Ended)
                        break;
                    Thread.Sleep(5);
                    spins++;
                    continue;
                }
                var copy = Math.Min(n, frames - written);
                temp.AsSpan(0, copy * 2).CopyTo(data.AsSpan(written * 2));
                written += copy;
            }

            Array.Resize(ref data, Math.Max(2, written * 2));
            var track = AudioFileSupport.ReadMetadata(path);
            return Result<DecodedClip>.Ok(new DecodedClip
            {
                Samples = data,
                SampleRate = engineRate,
                Track = track
            });
        }
        catch (Exception ex)
        {
            log.Warning("soundboard", "Could not decode sound.", ex.Message);
            return Result<DecodedClip>.Fail("Could not load sound.", ex.Message);
        }
    }
}
