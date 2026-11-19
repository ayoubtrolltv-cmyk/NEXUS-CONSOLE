using System.Diagnostics;
using Microsoft.Win32;
using Nexus.Core.Logging;
using Nexus.Core.Models;

namespace Nexus.Launching;

public enum SessionOutcome { Ended, NeverStarted, Cancelled, Untracked }

public sealed record SessionResult(SessionOutcome Outcome, DateTime StartedUtc, TimeSpan Played);

public sealed class LaunchException : Exception
{
    public LaunchException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Lance un jeu puis suit sa session. Le vrai jeu est reconnu par son chemin (un processus sous le dossier d'installation)
/// et, pour Steam, par la valeur RunningAppID. Un délai de grâce évite les retours prématurés (lanceur intermédiaire,
/// installation de prérequis, second exécutable). Un seul suivi à la fois ; aucune relance automatique.
/// </summary>
public sealed class GameLauncher
{
    private static readonly string[] IgnoredPrefixes =
        { "UnityCrashHandler", "CrashReportClient", "vc_redist", "dxsetup", "UE4PrereqSetup", "EasyAntiCheat_EOS_Setup", "unins", "dotnet" };

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan GraceShort = TimeSpan.FromSeconds(20);   // jeu vu moins de 30 s : lanceur / prérequis probables
    private static readonly TimeSpan GraceNormal = TimeSpan.FromSeconds(4);
    private readonly object _gate = new();
    private bool _busy;
    private DateTime _lastEndUtc = DateTime.MinValue;

    public bool IsBusy { get { lock (_gate) return _busy; } }

    /// <param name="onGameDetected">Appelé une fois, depuis un thread d'arrière-plan, quand le vrai jeu est détecté.</param>
    public async Task<SessionResult> RunAsync(Game game, Action onGameDetected, CancellationToken cancel)
    {
        lock (_gate)
        {
            if (_busy) throw new LaunchException("Un lancement est déjà en cours.");
            if (DateTime.UtcNow - _lastEndUtc < TimeSpan.FromSeconds(3)) throw new LaunchException("Patiente un instant avant de relancer.");
            _busy = true;
        }
        try { return await RunCoreAsync(game, onGameDetected, cancel).ConfigureAwait(false); }
        finally { lock (_gate) { _busy = false; _lastEndUtc = DateTime.UtcNow; } }
    }

    private static async Task<SessionResult> RunCoreAsync(Game game, Action onGameDetected, CancellationToken cancel)
    {
        var startedUtc = DateTime.UtcNow;
        Process? launched;
        try { launched = Start(game); }
        catch (LaunchException) { throw; }
        catch (Exception ex)
        {
            Log.Error($"Lancement de « {game.Name} » impossible.", ex);
            throw new LaunchException("Ce jeu n'a pas pu être lancé.", ex);
        }

        await Task.Delay(300).ConfigureAwait(false);   // la suite (énumération des processus) quitte le thread d'interface

        var install = NormalizeDir(game.InstallPath ?? (game.ExePath is null ? null : Path.GetDirectoryName(game.ExePath)));
        var steamId = game.Platform == "steam" && int.TryParse(game.ExternalId, out var id) ? id : (int?)null;

        // Aucun moyen de reconnaître le jeu : on suit seulement le processus lancé, s'il existe.
        if (game.TrackLaunchedProcess || (install is null && steamId is null))
        {
            if (launched is null) return new SessionResult(SessionOutcome.Untracked, startedUtc, TimeSpan.Zero);
            onGameDetected();
            try { await launched.WaitForExitAsync().ConfigureAwait(false); } catch { }
            return new SessionResult(SessionOutcome.Ended, startedUtc, DateTime.UtcNow - startedUtc);
        }

        DateTime? firstSeen = null;
        var lastSeen = DateTime.MinValue;
        while (true)
        {
            var now = DateTime.UtcNow;
            if (IsRunning(install, steamId))
            {
                if (firstSeen is null) { firstSeen = now; Log.Info($"Jeu détecté : {game.Name}"); onGameDetected(); }
                lastSeen = now;
            }
            else if (firstSeen is null)
            {
                if (now - startedUtc > StartTimeout)
                {
                    Log.Warn($"« {game.Name} » n'a pas démarré dans le délai imparti.");
                    return new SessionResult(SessionOutcome.NeverStarted, startedUtc, TimeSpan.Zero);
                }
            }
            else
            {
                var ran = lastSeen - firstSeen.Value;
                var grace = ran < TimeSpan.FromSeconds(30) ? GraceShort : GraceNormal;
                if (now - lastSeen > grace)
                {
                    Log.Info($"Session terminée : {game.Name}, {ran.TotalMinutes:0.0} min");
                    return new SessionResult(SessionOutcome.Ended, startedUtc, ran);
                }
            }

            try { await Task.Delay(firstSeen is null ? 1000 : 2000, firstSeen is null ? cancel : CancellationToken.None).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                Log.Info($"Lancement de « {game.Name} » annulé par l'utilisateur.");
                return new SessionResult(SessionOutcome.Cancelled, startedUtc, TimeSpan.Zero);
            }
        }
    }

    private static Process? Start(Game game)
    {
        ProcessStartInfo psi;
        if (!string.IsNullOrWhiteSpace(game.LaunchUri))
            psi = new ProcessStartInfo(game.LaunchUri) { UseShellExecute = true };
        else if (!string.IsNullOrWhiteSpace(game.ExePath))
        {
            if (!File.Exists(game.ExePath)) throw new LaunchException("Le fichier du jeu est introuvable. Vérifie qu'il n'a pas été déplacé ou désinstallé.");
            psi = new ProcessStartInfo(game.ExePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(game.ExePath) ?? "", Arguments = game.Arguments ?? "" };
        }
        else throw new LaunchException("Aucun moyen de lancer ce jeu n'est connu.");

        Log.Info($"Lancement de « {game.Name} » ({game.Platform}) : {psi.FileName}");
        return Process.Start(psi);
    }

    private static bool IsRunning(string? installDir, int? steamAppId)
    {
        if (steamAppId is { } id &&
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "RunningAppID", 0) is int running && running == id)
            return true;
        if (installDir is null) return false;

        var self = Environment.ProcessId;
        foreach (var (pid, path) in ProcessPaths.Enumerate())
        {
            if (pid == self || !IsUnder(path, installDir)) continue;
            var file = Path.GetFileName(path);
            if (IgnoredPrefixes.Any(p => file.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
            return true;
        }
        return false;
    }

    public static string? NormalizeDir(string? dir) =>
        string.IsNullOrWhiteSpace(dir) ? null : dir.Replace('/', '\\').TrimEnd('\\') + "\\";

    public static bool IsUnder(string path, string normalizedDir) =>
        path.Replace('/', '\\').StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
}
