using System.IO.Compression;
using System.Security.Cryptography;
using Mixline.Core;
using Mixline.Logging;

namespace Mixline.Storage;

public sealed class BackupService
{
    private readonly AppLog _log;

    public BackupService(AppLog log)
    {
        _log = log;
    }

    public Result Export(string destinationPath)
    {
        try
        {
            AppPaths.EnsureCreated();
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            ZipFile.CreateFromDirectory(AppPaths.Root, destinationPath, CompressionLevel.Fastest, false);
            using var archive = ZipFile.Open(destinationPath, ZipArchiveMode.Update);
            var token = archive.GetEntry("spotify.bin");
            token?.Delete();
            _log.Info("backup", $"Exported configuration to {destinationPath}");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Error("backup", "Export failed.", ex);
            return Result.Fail("Could not export settings.", ex.Message);
        }
    }

    public Result Import(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath))
                return Result.Fail("Backup file was not found.");

            AppPaths.EnsureCreated();
            using var archive = ZipFile.OpenRead(sourcePath);
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.Name, "spotify.bin", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var dest = Path.GetFullPath(Path.Combine(AppPaths.Root, entry.FullName));
                if (!dest.StartsWith(Path.GetFullPath(AppPaths.Root), StringComparison.OrdinalIgnoreCase))
                    return Result.Fail("Backup file contained an invalid path.");

                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                entry.ExtractToFile(dest, true);
            }

            _log.Info("backup", $"Imported configuration from {sourcePath}");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _log.Error("backup", "Import failed.", ex);
            return Result.Fail("Could not import settings.", ex.Message);
        }
    }
}

public sealed class SecretStore
{
    public void Save(string path, byte[] data)
    {
        var protectedBytes = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    public byte[]? Load(string path)
    {
        if (!File.Exists(path))
            return null;
        var protectedBytes = File.ReadAllBytes(path);
        return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
