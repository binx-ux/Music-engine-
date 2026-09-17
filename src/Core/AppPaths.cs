namespace Mixline.Core;

public static class AppPaths
{
    private static string? _root;

    public static string Root
    {
        get
        {
            _root ??= Resolve();
            return _root;
        }
    }

    public static string Logs => Path.Combine(Root, "Logs");
    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string Profiles => Path.Combine(Root, "Profiles");
    public static string Soundboard => Path.Combine(Root, "Soundboard");
    public static string Presets => Path.Combine(Root, "Presets");
    public static string Cache => Path.Combine(Root, "Cache");
    public static string Music => Path.Combine(Root, "Music");
    public static string Backups => Path.Combine(Root, "Backups");
    public static string TokenFile => Path.Combine(Root, "spotify.bin");
    public static string MarkerFile => Path.Combine(AppContext.BaseDirectory, "data.path");

    public static void EnsureCreated()
    {
        TryMigrateLegacy();
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Profiles);
        Directory.CreateDirectory(Soundboard);
        Directory.CreateDirectory(Presets);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Music);
        Directory.CreateDirectory(Backups);
    }

    public static bool Relocate(string dest, out string error)
    {
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(dest))
            {
                error = "Pick a folder.";
                return false;
            }

            dest = Path.GetFullPath(dest.Trim());
            var from = Root;
            Directory.CreateDirectory(dest);
            if (!string.Equals(from, dest, StringComparison.OrdinalIgnoreCase) && Directory.Exists(from))
                CopyDir(from, dest);

            File.WriteAllText(MarkerFile, dest);
            _root = dest;
            EnsureCreated();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Resolve()
    {
        var env = Environment.GetEnvironmentVariable("CUEBOX_HOME");
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env);

        if (File.Exists(MarkerFile))
        {
            var line = File.ReadAllText(MarkerFile).Trim();
            if (line.Length > 0)
                return Path.GetFullPath(line);
        }

        var portable = Path.Combine(AppContext.BaseDirectory, "Cuebox.data");
        if (Directory.Exists(portable))
            return portable;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.Name);
    }

    private static void TryMigrateLegacy()
    {
        if (Directory.Exists(Root))
            return;
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mixline");
        if (!Directory.Exists(legacy))
            return;
        try
        {
            CopyDir(legacy, Root);
        }
        catch
        {
        }
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(from))
            CopyDir(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
