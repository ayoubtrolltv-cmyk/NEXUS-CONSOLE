using System.Text.Json;
using Nexus.Core.Logging;
using Nexus.Core.Models;

namespace Nexus.Detection.Sources;

/// <summary>Epic Games : manifestes .item (JSON) dans ProgramData. Lancement par l'URI du lanceur Epic.</summary>
public sealed class EpicSource : IGameSource
{
    private readonly string _manifestsDir;

    public EpicSource(string? manifestsOverride = null) =>
        _manifestsDir = manifestsOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public string Platform => "epic";

    public IReadOnlyList<DetectedGame> Scan()
    {
        var result = new List<DetectedGame>();
        if (!Directory.Exists(_manifestsDir)) return result;

        foreach (var file in Directory.EnumerateFiles(_manifestsDir, "*.item"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var r = doc.RootElement;
                string? S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                var appName = S("AppName"); var name = S("DisplayName");
                var install = S("InstallLocation"); var exe = S("LaunchExecutable");
                if (appName is null || name is null || install is null || string.IsNullOrWhiteSpace(exe)) continue;   // extensions, outils…
                if (r.TryGetProperty("bIsIncompleteInstall", out var inc) && inc.ValueKind == JsonValueKind.True) continue;
                if (r.TryGetProperty("AppCategories", out var cats) && cats.ValueKind == JsonValueKind.Array &&
                    !cats.EnumerateArray().Any(c => string.Equals(c.GetString(), "games", StringComparison.OrdinalIgnoreCase))) continue;
                if (!Directory.Exists(install)) continue;

                result.Add(new DetectedGame("epic", appName, name, install, Path.Combine(install, exe),
                    $"com.epicgames.launcher://apps/{Uri.EscapeDataString(appName)}?action=launch&silent=true", null));
            }
            catch (Exception ex) { Log.Warn($"Manifeste Epic illisible : {file}", ex); }
        }
        return result;
    }
}
