using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nexus.Core.Config;
using Nexus.Core.Logging;

namespace Nexus.Shell.Design;

public static class GameVisuals
{
    public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Couleur stable dérivée du nom : sert d'ambiance tant qu'aucune pochette n'est disponible.</summary>
    public static Color ColorFor(string name)
    {
        uint h = 2166136261;
        foreach (var ch in name.ToLowerInvariant()) { h ^= ch; h *= 16777619; }
        return FromHsl(h % 360, 0.55, 0.34);
    }

    public static Color FromHsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = h < 60 ? (c, x, 0.0) : h < 120 ? (x, c, 0.0) : h < 180 ? (0.0, c, x)
                      : h < 240 ? (0.0, x, c) : h < 300 ? (x, 0.0, c) : (c, 0.0, x);
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
    }

    public static string PlatformLabel(string platform) => platform switch
    {
        "manual" => "Ajouté manuellement", "steam" => "Steam", "epic" => "Epic Games", "gog" => "GOG",
        "ubisoft" => "Ubisoft Connect", _ => platform
    };

    public static string FormatPlaytime(long seconds)
    {
        if (seconds <= 0) return "Aucun temps de jeu enregistré";
        var h = seconds / 3600; var m = seconds % 3600 / 60;
        return h > 0 ? $"{h} h {m:00} min" : $"{Math.Max(m, 1)} min";
    }

    /// <summary>Extrait l'icône Windows d'un exécutable et l'enregistre en PNG dans le cache. Null si impossible.</summary>
    public static string? ExtractIcon(string path)
    {
        try
        {
            var dir = Path.Combine(AppPaths.Cache, "icons");
            Directory.CreateDirectory(dir);
            var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16] + ".png";
            var file = Path.Combine(dir, name);
            if (File.Exists(file)) return file;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(src));
            using var fs = File.Create(file);
            enc.Save(fs);
            return file;
        }
        catch (Exception ex)
        {
            Log.Warn($"Icône non extraite pour {path}", ex);
            return null;
        }
    }
}
