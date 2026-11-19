using Microsoft.Win32;
using Nexus.Core.Logging;
using Nexus.Core.Models;

namespace Nexus.Detection.Sources;

/// <summary>GOG : clés de registre des jeux installés. Les jeux GOG se lancent directement par leur exécutable.</summary>
public sealed class GogSource : IGameSource
{
    public string Platform => "gog";

    public IReadOnlyList<DetectedGame> Scan()
    {
        var result = new List<DetectedGame>();
        using var games = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
        if (games is null) return result;

        foreach (var id in games.GetSubKeyNames())
        {
            try
            {
                using var k = games.OpenSubKey(id);
                var name = k?.GetValue("gameName") as string;
                var path = k?.GetValue("path") as string;
                var exe = k?.GetValue("exe") as string;
                if (name is null || path is null || !Directory.Exists(path)) continue;
                if (exe is not null && !File.Exists(exe)) exe = null;
                result.Add(new DetectedGame("gog", id, name, path, exe, null, null));
            }
            catch (Exception ex) { Log.Warn($"Entrée GOG illisible : {id}", ex); }
        }
        return result;
    }
}
