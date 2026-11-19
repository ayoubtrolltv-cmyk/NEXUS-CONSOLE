using Microsoft.Win32;
using Nexus.Core.Logging;
using Nexus.Core.Models;

namespace Nexus.Detection.Sources;

/// <summary>
/// Ubisoft Connect : registre des installations. Le registre ne contient pas le nom du jeu : il est déduit du nom du dossier
/// d'installation (limite connue). Lancement par l'URI uplay://.
/// </summary>
public sealed class UbisoftSource : IGameSource
{
    public string Platform => "ubisoft";

    public IReadOnlyList<DetectedGame> Scan()
    {
        var result = new List<DetectedGame>();
        using var installs = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
        if (installs is null) return result;

        foreach (var id in installs.GetSubKeyNames())
        {
            try
            {
                using var k = installs.OpenSubKey(id);
                var dir = (k?.GetValue("InstallDir") as string)?.Replace('/', '\\').TrimEnd('\\');
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
                result.Add(new DetectedGame("ubisoft", id, Path.GetFileName(dir), dir, null, $"uplay://launch/{id}/0", null));
            }
            catch (Exception ex) { Log.Warn($"Entrée Ubisoft illisible : {id}", ex); }
        }
        return result;
    }
}
