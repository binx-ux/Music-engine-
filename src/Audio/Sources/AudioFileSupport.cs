using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
            using WaveStream stream = Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                ? new VorbisWaveReader(path)
                : new AudioFileReader(path);

            if (stream.TotalTime.TotalSeconds > maxSeconds)
                return Result<DecodedClip>.Fail("Sound is too long to load into the soundboard.");

            ISampleProvider sample = stream.ToSampleProvider();
            if (sample.WaveFormat.Channels == 1)
                sample = new MonoToStereoSampleProvider(sample);
            if (sample.WaveFormat.SampleRate != engineRate)
                sample = new HqResampleProvider(sample, engineRate);

            var frames = Math.Max(1, (int)Math.Ceiling(Math.Max(stream.TotalTime.TotalSeconds, 0.05) * engineRate) + 256);
            var data = new float[frames * 2];
            var written = 0;
            var temp = new float[4096];
            while (written < frames)
            {
                var n = sample.Read(temp, 0, Math.Min(temp.Length, (frames - written) * 2));
                if (n <= 0)
                    break;
                temp.AsSpan(0, n).CopyTo(data.AsSpan(written * 2));
                written += n / 2;
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
