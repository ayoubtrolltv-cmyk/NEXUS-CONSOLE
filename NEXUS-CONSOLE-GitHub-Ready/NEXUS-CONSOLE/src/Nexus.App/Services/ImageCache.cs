using System.Windows.Media.Imaging;
using Nexus.Core.Logging;

namespace Nexus.Shell.Services;

/// <summary>Chargement paresseux d'images, décodées à la taille d'affichage, avec un cache borné.</summary>
public static class ImageCache
{
    /// <summary>Nombre maximal d'images décodées conservées (réduit en mode économie de mémoire).</summary>
    public static int MaxEntries { get; set; } = 96;
    private static readonly Dictionary<string, BitmapSource> Map = new();
    private static readonly Queue<string> Order = new();

    public static BitmapSource? Load(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        var key = $"{path}|{decodeWidth}";
        if (Map.TryGetValue(key, out var cached)) return cached;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;   // libère le fichier immédiatement
            bi.DecodePixelWidth = decodeWidth;
            bi.UriSource = new Uri(path);
            bi.EndInit();
            bi.Freeze();

            Map[key] = bi;
            Order.Enqueue(key);
            while (Order.Count > MaxEntries) Map.Remove(Order.Dequeue());
            return bi;
        }
        catch (Exception ex)
        {
            Log.Warn($"Image illisible : {path}", ex);
            return null;
        }
    }

    public static void Clear() { Map.Clear(); Order.Clear(); }
}
