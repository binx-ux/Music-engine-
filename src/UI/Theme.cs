using System.Windows;
using System.Windows.Media;
using Mixline.Core;

namespace Mixline.App;

public static class Theme
{
    public static readonly (string Id, string Label, string Hex)[] Presets =
    [
        ("Night", "Night", "#C9A36A"),
        ("Acid", "Acid", "#7ED957"),
        ("Ice", "Ice", "#6EB4D4"),
        ("Blood", "Blood", "#D25A54"),
        ("Graphite", "Graphite", "#B4B8C0")
    ];

    public static event Action? Changed;

    public static Color Accent { get; private set; } = Color.FromRgb(0xC9, 0xA3, 0x6A);

    public static void Apply(AppearanceSettings settings)
    {
        var hex = settings.Theme == "Custom" && LooksHex(settings.AccentHex)
            ? settings.AccentHex
            : PresetHex(settings.Theme);
        var accent = Parse(hex);
        Accent = accent;
        var hover = Mix(accent, Colors.White, 0.22);
        var press = Mix(accent, Colors.Black, 0.18);
        var soft = Mix(accent, Colors.White, 0.45);
        var dark = Mix(accent, Colors.Black, 0.78);
        var ghost = Color.FromArgb(0x28, accent.R, accent.G, accent.B);
        var select = Color.FromArgb(0x90, accent.R, accent.G, accent.B);

        SetBrush("AccentBrush", accent);
        SetBrush("AccentHoverBrush", hover);
        SetBrush("AccentPressBrush", press);
        SetBrush("AccentSoftBrush", soft);
        SetBrush("AccentDarkBrush", dark);
        SetBrush("AccentGhostBrush", ghost);
        SetBrush("SelectionBrush", select);

        Application.Current.Resources["GlassEdge"] = Edge(accent);
        Application.Current.Resources["WindowEdge"] = WindowEdge(accent);
        Application.Current.Resources["WindowSheen"] = Sheen(accent);
        Changed?.Invoke();
    }

    public static string PresetHex(string? id)
    {
        foreach (var p in Presets)
        {
            if (p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                return p.Hex;
        }
        return Presets[0].Hex;
    }

    public static bool LooksHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim().TrimStart('#');
        return text.Length is 6 or 8 && text.All(Uri.IsHexDigit);
    }

    public static int DwmColor(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static Color Parse(string hex)
    {
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 8)
            hex = hex[2..];
        return Color.FromRgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private static Color Mix(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    private static LinearGradientBrush Edge(Color accent)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x50, 255, 255, 255), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x22, 255, 255, 255), 0.45));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, accent.R, accent.G, accent.B), 1));
        b.Freeze();
        return b;
    }

    private static LinearGradientBrush WindowEdge(Color accent)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 255, 255, 255), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x28, 255, 255, 255), 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x50, accent.R, accent.G, accent.B), 1));
        b.Freeze();
        return b;
    }

    private static LinearGradientBrush Sheen(Color accent)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x14, 255, 255, 255), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x05, 255, 255, 255), 0.4));
        b.GradientStops.Add(new GradientStop(Colors.Transparent, 0.62));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, accent.R, accent.G, accent.B), 1));
        b.Freeze();
        return b;
    }
}
