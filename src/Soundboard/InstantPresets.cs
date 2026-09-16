using System.Diagnostics;
using Mixline.Core;

namespace Mixline.Soundboard;

public static class InstantPresets
{
    private const string Host = "https://www.myinstants.com";
    private const int MaxPack = 1800;
    private static readonly string Curl = Path.Combine(Environment.SystemDirectory, "curl.exe");

    public static async Task<int> InstallAsync(SoundboardLayout layout, CancellationToken ct)
    {
        var dir = Path.Combine(AppPaths.Presets, "myinstants");
        Directory.CreateDirectory(dir);

        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pad in layout.Pads)
        {
            have.Add(pad.Name);
            if (!string.IsNullOrWhiteSpace(pad.FilePath))
                have.Add(Path.GetFileName(pad.FilePath));
        }

        var wanted = new List<(string Name, string File)>();
        var room = MaxPack - layout.Pads.Count;
        if (room <= 0)
            return 0;
        foreach (var (name, file) in ReadSeed())
        {
            if (wanted.Count >= room)
                break;
            if (have.Contains(file) || have.Contains(name))
                continue;
            wanted.Add((name, file));
        }

        using var gate = new SemaphoreSlim(6);
        var order = layout.Pads.Count == 0 ? 0 : layout.Pads.Max(p => p.Order) + 1;
        var tasks = wanted.Select(item => Pull(gate, item.Name, item.File,
            Path.Combine(dir, item.File), ct)).ToArray();

        var added = 0;
        foreach (var item in await Task.WhenAll(tasks))
        {
            if (item is null)
                continue;
            layout.Pads.Add(new SoundPad
            {
                Name = item.Value.Name,
                FilePath = item.Value.Dest,
                Folder = "Myinstants",
                Order = order++
            });
            added++;
        }

        if (added > 0 && !layout.Folders.Contains("Myinstants"))
            layout.Folders.Add("Myinstants");
        return added;
    }

    private static IEnumerable<(string Name, string File)> ReadSeed()
    {
        var asm = typeof(InstantPresets).Assembly;
        using var stream = asm.GetManifestResourceStream("Mixline.Soundboard.instants.txt");
        if (stream is null)
            yield break;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim().Trim('\uFEFF');
            var tab = line.IndexOf('\t');
            if (tab <= 0 || tab >= line.Length - 1)
                continue;
            yield return (line[..tab], line[(tab + 1)..]);
        }
    }

    private static async Task<(string Name, string Dest)?> Pull(
        SemaphoreSlim gate, string name, string file, string dest, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(dest) || new FileInfo(dest).Length < 200 || IsHtml(dest))
            {
                var tmp = dest + ".part";
                var psi = new ProcessStartInfo
                {
                    FileName = File.Exists(Curl) ? Curl : "curl.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add("-sL");
                psi.ArgumentList.Add("-A");
                psi.ArgumentList.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
                psi.ArgumentList.Add("-o");
                psi.ArgumentList.Add(tmp);
                psi.ArgumentList.Add("--max-time");
                psi.ArgumentList.Add("20");
                psi.ArgumentList.Add(Host + "/media/sounds/" + file);
                using var proc = Process.Start(psi);
                if (proc is null)
                    return null;
                await proc.WaitForExitAsync(ct);
                if (proc.ExitCode != 0 || !File.Exists(tmp))
                {
                    TryDelete(tmp);
                    return null;
                }
                var info = new FileInfo(tmp);
                if (info.Length < 200 || info.Length > 4_000_000 || IsHtml(tmp))
                {
                    TryDelete(tmp);
                    return null;
                }
                File.Move(tmp, dest, true);
            }
            return (name, dest);
        }
        catch
        {
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsHtml(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> buf = stackalloc byte[16];
            var n = fs.Read(buf);
            for (var i = 0; i < n; i++)
            {
                if (buf[i] is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t')
                    continue;
                return buf[i] == (byte)'<';
            }
        }
        catch
        {
        }
        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
