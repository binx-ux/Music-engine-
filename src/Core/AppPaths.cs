namespace Mixline.Core;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.Name);

    public static string Logs => Path.Combine(Root, "Logs");
    public static string ConfigFile => Path.Combine(Root, "config.json");
    public static string Profiles => Path.Combine(Root, "Profiles");
    public static string Soundboard => Path.Combine(Root, "Soundboard");
    public static string Presets => Path.Combine(Root, "Presets");
    public static string Cache => Path.Combine(Root, "Cache");
    public static string Music => Path.Combine(Root, "Music");
    public static string Backups => Path.Combine(Root, "Backups");
    public static string TokenFile => Path.Combine(Root, "spotify.bin");

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
