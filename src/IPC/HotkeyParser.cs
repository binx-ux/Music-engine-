using Mixline.Core;
using Mixline.Logging;

namespace Mixline.IPC;

public sealed class HotkeyBinding
{
    public required string Id { get; init; }
    public required int Modifiers { get; init; }
    public required int Key { get; init; }
    public required string Display { get; init; }
}

public static class HotkeyParser
{
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;
    public const int ModNoRepeat = 0x4000;

    public static Result<HotkeyBinding> Parse(string id, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result<HotkeyBinding>.Fail("No shortcut entered.");

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var mods = 0;
        int? key = null;
        foreach (var raw in parts)
        {
            var p = raw.Trim();
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModControl;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= ModAlt;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModShift;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase) || p.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= ModWin;
            else if (TryKey(p, out var vk))
                key = vk;
            else
                return Result<HotkeyBinding>.Fail($"Unknown key '{p}'.");
        }

        if (key is null)
            return Result<HotkeyBinding>.Fail("A shortcut needs a key, not only modifiers.");

        var fn = key.Value >= 0x70 && key.Value <= 0x87;
        if (mods == 0 && !fn)
            return Result<HotkeyBinding>.Fail("Use F1-F12, or add Ctrl / Alt, like Ctrl+1.");

        return Result<HotkeyBinding>.Ok(new HotkeyBinding
        {
            Id = id,
            Modifiers = mods,
            Key = key.Value,
            Display = Format(mods, key.Value)
        });
    }

    public static bool TryKey(string p, out int vk)
    {
        vk = 0;
        if (p.Length == 1)
        {
            var c = char.ToUpperInvariant(p[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
            return false;
        }
        if (p.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(p[1..], out var fn) && fn is >= 1 and <= 24)
        {
            vk = 0x70 + (fn - 1);
            return true;
        }
        if (int.TryParse(p, out var digit) && digit is >= 0 and <= 9)
        {
            vk = 0x30 + digit;
            return true;
        }
        if (p.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            vk = 0x20;
            return true;
        }
        return false;
    }

    public static string Format(int mods, int vk)
    {
        var parts = new List<string>();
        if ((mods & ModControl) != 0) parts.Add("Ctrl");
        if ((mods & ModAlt) != 0) parts.Add("Alt");
        if ((mods & ModShift) != 0) parts.Add("Shift");
        if ((mods & ModWin) != 0) parts.Add("Win");
        if (vk is >= 0x70 and <= 0x87)
            parts.Add("F" + (vk - 0x6F));
        else if (vk == 0x20)
            parts.Add("Space");
        else
            parts.Add(((char)vk).ToString());
        return string.Join("+", parts);
    }
}

public sealed class HotkeyConflict
{
    public required string Id { get; init; }
    public required string Display { get; init; }
    public required string Message { get; init; }
}
