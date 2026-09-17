namespace Mixline.Core;

public enum UpdateKind
{
    None,
    Fix,
    Minor,
    Major
}

public static class AppVersion
{
    public static bool TryParse(string? text, out int major, out int minor, out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        var cut = text.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0)
            text = text[..cut];
        var parts = text.Split('.');
        if (parts.Length < 2)
            return false;
        if (!int.TryParse(parts[0], out major) || !int.TryParse(parts[1], out minor))
            return false;
        if (parts.Length >= 3 && !int.TryParse(parts[2], out patch))
            patch = 0;
        return major >= 0 && minor >= 0 && patch >= 0;
    }

    public static string Canonical(int major, int minor, int patch) => $"{major}.{minor}.{patch}";

    public static int Compare(string? a, string? b)
    {
        if (!TryParse(a, out var am, out var an, out var ap))
            return -1;
        if (!TryParse(b, out var bm, out var bn, out var bp))
            return 1;
        var c = am.CompareTo(bm);
        if (c != 0) return c;
        c = an.CompareTo(bn);
        if (c != 0) return c;
        return ap.CompareTo(bp);
    }

    public static UpdateKind Kind(string current, string latest)
    {
        if (!TryParse(current, out var cm, out var cn, out var cp))
            return UpdateKind.None;
        if (!TryParse(latest, out var lm, out var ln, out var lp))
            return UpdateKind.None;
        if (lm < cm || (lm == cm && ln < cn) || (lm == cm && ln == cn && lp <= cp))
            return UpdateKind.None;
        if (lm > cm)
            return UpdateKind.Major;
        if (ln > cn)
            return UpdateKind.Minor;
        return UpdateKind.Fix;
    }
}
