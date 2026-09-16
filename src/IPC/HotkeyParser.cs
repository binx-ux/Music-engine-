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
            else if (p.Length == 1)
                key = char.ToUpperInvariant(p[0]);
            else if (p.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(p[1..], out var fn) && fn is >= 1 and <= 24)
                key = 0x70 + (fn - 1);
            else if (int.TryParse(p, out var digit) && digit is >= 0 and <= 9)
                key = 0x30 + digit;
            else
                return Result<HotkeyBinding>.Fail($"Unknown key '{p}'.");
        }

        if (key is null)
            return Result<HotkeyBinding>.Fail("A shortcut needs a key, not only modifiers.");
        if (mods == 0)
            return Result<HotkeyBinding>.Fail("Global shortcuts need a modifier such as Ctrl or Alt.");

        return Result<HotkeyBinding>.Ok(new HotkeyBinding
        {
            Id = id,
            Modifiers = mods,
            Key = key.Value,
            Display = text.Trim()
        });
    }
}

public sealed class HotkeyConflict
{
    public required string Id { get; init; }
    public required string Display { get; init; }
    public required string Message { get; init; }
}
