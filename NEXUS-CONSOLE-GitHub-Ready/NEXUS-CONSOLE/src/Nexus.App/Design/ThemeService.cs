using System.Windows;
using System.Windows.Media;
using Nexus.Core.Config;

namespace Nexus.Shell.Design;

public static class ThemeService
{
    public static void Apply(AppSettings s)
    {
        var r = Application.Current.Resources;
        var accent = ParseColor(s.Accent);

        if (s.Theme == "light")
        {
            Set(r, "BgBrush", "#F3F5F9"); Set(r, "SurfaceBrush", "#FFFFFF"); Set(r, "SurfaceHoverBrush", "#EEF1F7");
            Set(r, "StrokeBrush", "#D9DEE8"); Set(r, "TextBrush", "#12151C"); Set(r, "TextDimBrush", "#5B6478");
        }
        else
        {
            Set(r, "BgBrush", "#0B0D12"); Set(r, "SurfaceBrush", "#161B28"); Set(r, "SurfaceHoverBrush", "#1E2536");
            Set(r, "StrokeBrush", "#2A3144"); Set(r, "TextBrush", "#F2F4F8"); Set(r, "TextDimBrush", "#8B93A7");
        }
        Set(r, "AccentBrush", accent);
        Set(r, "AccentSoftBrush", Color.FromArgb(0x33, accent.R, accent.G, accent.B));
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0x3B, 0x82, 0xF6); }
    }

    private static void Set(ResourceDictionary r, string key, string hex) => Set(r, key, ParseColor(hex));

    private static void Set(ResourceDictionary r, string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        r[key] = b;
    }
}
