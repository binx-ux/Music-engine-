using Microsoft.Win32;
using Mixline.Core;

namespace Mixline.IPC;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
            return;
        if (enabled)
            key.SetValue(AppInfo.Name, $"\"{exePath}\" --minimized");
        else
            key.DeleteValue(AppInfo.Name, false);
    }
}
