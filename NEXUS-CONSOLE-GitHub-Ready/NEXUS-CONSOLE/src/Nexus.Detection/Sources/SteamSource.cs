using System.Globalization;
using Microsoft.Win32;
using Nexus.Core.Logging;
using Nexus.Core.Models;

namespace Nexus.Detection.Sources;

/// <summary>
/// Steam : registre (chemin d'installation) → libraryfolders.vdf (bibliothèques) → appmanifest_*.acf (jeux installés).
/// Temps de jeu et dernière utilisation : localconfig.vdf de chaque compte, si présents (format non officiel, donc « au mieux »).
/// </summary>
public sealed class SteamSource : IGameSource
{
    private static readonly string[] IgnoredNames =
        { "Steamworks Common Redistributables", "Steam Linux Runtime", "Proton", "Steam Controller Configs" };

    private readonly string? _rootOverride;

    public SteamSource(string? rootOverride = null) => _rootOverride = rootOverride;

    public string Platform => "steam";

    public IReadOnlyList<DetectedGame> Scan()
    {
        var root = _rootOverride ?? FindRoot();
        var result = new List<DetectedGame>();
        if (root is null || !Directory.Exists(root)) return result;

        var stats = LoadPlaytime(root);
        foreach (var library in Libraries(root))
        {
            var steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            foreach (var acf in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                try
                {
                    var state = VdfParser.Parse(File.ReadAllText(acf)).GetValueOrDefault("AppState") as Dictionary<string, object>;
                    if (state is null) continue;
                    var appId = VdfParser.GetString(state, "appid");
                    var name = VdfParser.GetString(state, "name");
                    var dir = VdfParser.GetString(state, "installdir");
                    if (appId is null || name is null || dir is null) continue;
                    if (IgnoredNames.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) continue;

                    var flags = int.TryParse(VdfParser.GetString(state, "StateFlags"), out var f) ? f : 0;
                    if ((flags & 4) == 0) continue;                       // 4 = complètement installé

                    var install = Path.Combine(steamapps, "common", dir);
                    if (!Directory.Exists(install)) continue;

                    stats.TryGetValue(appId, out var st);
                    result.Add(new DetectedGame("steam", appId, name, install, null, $"steam://rungameid/{appId}",
                        FindCover(root, appId), st.Seconds, st.LastPlayed));
                }
                catch (Exception ex) { Log.Warn($"Manifeste Steam illisible : {acf}", ex); }
            }
        }
        return result;
    }

    private static string? FindRoot()
    {
        var p = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (!string.IsNullOrWhiteSpace(p)) return Path.GetFullPath(p);
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static IEnumerable<string> Libraries(string root)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        var file = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(file))
        {
            try
            {
                var doc = VdfParser.Parse(File.ReadAllText(file));
                if (doc.GetValueOrDefault("libraryfolders") is Dictionary<string, object> folders)
                    foreach (var entry in folders.Values)
                    {
                        // nouveau format : bloc avec "path" ; ancien format : chaîne directe
                        var path = entry switch { Dictionary<string, object> d => VdfParser.GetString(d, "path"), string s => s, _ => null };
                        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) set.Add(Path.GetFullPath(path));
                    }
            }
            catch (Exception ex) { Log.Warn("libraryfolders.vdf illisible.", ex); }
        }
        return set;
    }

    private static string? FindCover(string root, string appId)
    {
        var cache = Path.Combine(root, "appcache", "librarycache");
        var flat = Path.Combine(cache, $"{appId}_library_600x900.jpg");
        if (File.Exists(flat)) return flat;
        var dir = Path.Combine(cache, appId);
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir, "library_600x900*.jpg", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static Dictionary<string, (long Seconds, DateTime? LastPlayed)> LoadPlaytime(string root)
    {
        var map = new Dictionary<string, (long, DateTime?)>();
        var userdata = Path.Combine(root, "userdata");
        if (!Directory.Exists(userdata)) return map;
        foreach (var cfg in Directory.EnumerateFiles(userdata, "localconfig.vdf", SearchOption.AllDirectories))
        {
            try
            {
                var doc = VdfParser.Parse(File.ReadAllText(cfg));
                if (VdfParser.Dig(doc, "UserLocalConfigStore", "Software", "Valve", "Steam", "apps") is not Dictionary<string, object> apps) continue;
                foreach (var (id, v) in apps)
                {
                    if (v is not Dictionary<string, object> app) continue;
                    long minutes = long.TryParse(VdfParser.GetString(app, "Playtime"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
                    DateTime? last = long.TryParse(VdfParser.GetString(app, "LastPlayed"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var u) && u > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(u).UtcDateTime : null;
                    map.TryGetValue(id, out var old);
                    map[id] = (Math.Max(old.Item1, minutes * 60), last > old.Item2 || old.Item2 is null ? last : old.Item2);
                }
            }
            catch (Exception ex) { Log.Warn($"localconfig.vdf illisible : {cfg}", ex); }
        }
        return map;
    }
}
