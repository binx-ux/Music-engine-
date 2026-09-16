using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mixline.App;

internal static class NativeBackdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtAcrylic = 3;
    private const int DwmWcpRound = 2;
    private const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    public static bool TryApply(Window window, bool acrylic)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var dark = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var corner = DwmWcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        var backdrop = acrylic ? DwmsbtAcrylic : 2;
        var hr = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        var border = Theme.DwmColor(Theme.Accent);
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
        var none = DwmwaColorNone;
        DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref none, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref none, sizeof(int));
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (hr == 0)
        {
            window.Background = Brushes.Transparent;
            return true;
        }

        window.Background = new SolidColorBrush(Color.FromArgb(160, 12, 13, 16));
        return false;
    }
}
