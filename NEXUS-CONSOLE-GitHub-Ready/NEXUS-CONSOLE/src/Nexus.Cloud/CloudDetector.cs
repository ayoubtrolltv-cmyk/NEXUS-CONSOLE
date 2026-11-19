using Microsoft.Win32;
using Nexus.Core.Config;
using Nexus.Core.Models;

namespace Nexus.Cloud;

public enum CloudKind { NativeApp, Web }

public sealed record CloudService(string Id, string Name, CloudKind Kind, bool Installed, string? ExePath, string? Url, string Description);

/// <summary>
/// Détecte les services de cloud gaming. GeForce NOW : application installée (chemins connus et raccourcis du menu Démarrer).
/// Xbox Cloud Gaming et Amazon Luna n'ont pas d'application dédiée ici : ils s'ouvrent en version web.
/// NEXUS n'a accès à aucune information interne de ces services.
/// </summary>
public static class CloudDetector
{
    public static IReadOnlyList<CloudService> Detect(Func<string, bool>? fileExists = null, IEnumerable<string>? gfnCandidates = null)
    {
        fileExists ??= File.Exists;
        var gfn = (gfnCandidates ?? GfnCandidates()).FirstOrDefault(fileExists);
        return new[]
        {
            new CloudService("gfn", "NVIDIA GeForce NOW", CloudKind.NativeApp, gfn is not null, gfn, null, "Application NVIDIA installée sur ce PC"),
            new CloudService("gfn-web", "NVIDIA GeForce NOW (web)", CloudKind.Web, true, null, "https://play.geforcenow.com", "Version web, en attendant l'application"),
            new CloudService("xbox", "Xbox Cloud Gaming", CloudKind.Web, true, null, "https://www.xbox.com/play", "Service web Microsoft (compte requis)"),
            new CloudService("luna", "Amazon Luna", CloudKind.Web, true, null, "https://luna.amazon.com", "Service web Amazon (compte requis, selon disponibilité dans ton pays)"),
        };
    }

    private static IEnumerable<string> GfnCandidates()
    {
        var list = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA Corporation", "GeForceNOW", "CEF", "GeForceNOW.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "GeForceNOW", "CEF", "GeForceNOW.exe"),
        };
        foreach (var menu in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs"),
        })
        {
            try { if (Directory.Exists(menu)) list.AddRange(Directory.EnumerateFiles(menu, "GeForce NOW*.lnk", SearchOption.AllDirectories)); }
            catch { /* dossier inaccessible : on ignore */ }
        }
        return list;
    }

    /// <summary>Chemin de Microsoft Edge (installé avec Windows 10/11), ou null.</summary>
    public static string? FindEdge()
    {
        var fromRegistry = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", "", null) as string;
        if (!string.IsNullOrWhiteSpace(fromRegistry) && File.Exists(fromRegistry)) return fromRegistry;
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");
        return File.Exists(fallback) ? fallback : null;
    }
}

public static class CloudLauncher
{
    /// <summary>
    /// Construit une entrée de lancement temporaire (jamais enregistrée en base).
    /// Web : fenêtre Edge « application » avec un profil dédié (connexion conservée, processus à part donc suivable).
    /// </summary>
    public static Game ToGame(CloudService s, string? edgePath, bool trackFolder)
    {
        var game = new Game { Name = s.Name, Platform = "cloud", ExternalId = s.Id, Installed = true };
        if (s.Kind == CloudKind.NativeApp && s.ExePath is not null)
        {
            game.ExePath = s.ExePath;
            if (trackFolder && !s.ExePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) game.InstallPath = Path.GetDirectoryName(s.ExePath);
            else game.TrackLaunchedProcess = true;
        }
        else if (edgePath is not null && s.Url is not null)
        {
            game.ExePath = edgePath;
            game.Arguments = $"--app={s.Url} --user-data-dir=\"{Path.Combine(AppPaths.Root, "edge-profile")}\" --start-maximized";
            game.TrackLaunchedProcess = true;
        }
        else
        {
            game.LaunchUri = s.Url;   // navigateur par défaut : non suivi
        }
        return game;
    }
}
