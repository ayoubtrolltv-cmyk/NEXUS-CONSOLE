using Nexus.Core.Logging;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Detection.Sources;

namespace Nexus.Detection;

public sealed record ScanSummary(int Added, int Updated, int Missing, IReadOnlyList<string> Platforms, IReadOnlyList<string> Failed)
{
    public string ToText() =>
        $"{Added} ajouté(s), {Updated} mis à jour, {Missing} non installé(s)" +
        (Failed.Count > 0 ? $" · échec : {string.Join(", ", Failed)}" : "");
}

/// <summary>
/// Détection en arrière-plan : interroge chaque source, rapproche avec la base (sans doublons, sans écraser favoris/masquage),
/// marque « non installé » les jeux disparus. Une plateforme dont l'analyse échoue n'efface rien.
/// </summary>
public sealed class GameScanner
{
    private readonly GameRepository _repo;
    private readonly IReadOnlyList<IGameSource> _sources;
    private int _running;

    public event Action<ScanSummary>? Completed;

    /// <summary>Fournie par l'application : extrait l'icône d'un exécutable (dépend de Windows / WPF).</summary>
    public Func<string, string?>? IconProvider { get; set; }

    public GameScanner(GameRepository repo, IEnumerable<IGameSource>? sources = null)
    {
        _repo = repo;
        _sources = (sources ?? new IGameSource[] { new SteamSource(), new EpicSource(), new GogSource(), new UbisoftSource() }).ToList();
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Lance l'analyse hors du thread d'interface. Retourne null si une analyse est déjà en cours.</summary>
    public Task<ScanSummary?> ScanAsync() =>
        Interlocked.Exchange(ref _running, 1) == 1
            ? Task.FromResult<ScanSummary?>(null)
            : Task.Run(() =>
            {
                try
                {
                    var summary = Scan();
                    Completed?.Invoke(summary);
                    return (ScanSummary?)summary;
                }
                catch (Exception ex)
                {
                    Log.Error("Analyse des jeux interrompue.", ex);
                    return null;
                }
                finally { Interlocked.Exchange(ref _running, 0); }
            });

    public ScanSummary Scan()
    {
        var detected = new List<DetectedGame>();
        var scanned = new HashSet<string>();
        var failed = new List<string>();
        foreach (var source in _sources)
        {
            try { detected.AddRange(source.Scan()); scanned.Add(source.Platform); }
            catch (Exception ex) { Log.Error($"Analyse {source.Platform} en échec.", ex); failed.Add(source.Platform); }
        }

        var existing = _repo.List(includeHidden: true);
        var manualExes = existing.Where(g => g.Platform == "manual" && g.ExePath is not null)
                                 .Select(g => Norm(g.ExePath!)).ToHashSet();
        int added = 0, updated = 0;
        var seen = new HashSet<(string, string)>();

        foreach (var d in detected)
        {
            if (!seen.Add((d.Platform, d.ExternalId))) continue;                        // doublon dans les sources
            if (d.ExePath is not null && manualExes.Contains(Norm(d.ExePath))) continue; // déjà ajouté à la main

            var game = new Game
            {
                Name = d.Name, Platform = d.Platform, ExternalId = d.ExternalId, ExePath = d.ExePath,
                InstallPath = d.InstallPath, LaunchUri = d.LaunchUri, CoverPath = d.CoverPath,
                IconPath = d.ExePath is not null ? IconProvider?.Invoke(d.ExePath) : null,
                PlaytimeSeconds = d.PlaytimeSeconds ?? 0, LastPlayedUtc = d.LastPlayedUtc, Installed = true
            };
            var current = _repo.Find(d.Platform, d.ExternalId);
            if (current is null) { if (_repo.Add(game) is not null) added++; }
            else { _repo.UpdateDetected(current.Id, game); updated++; }
        }

        var missing = 0;
        foreach (var g in existing)
        {
            if (g.Platform == "manual" || !scanned.Contains(g.Platform) || !g.Installed) continue;
            if (seen.Contains((g.Platform, g.ExternalId))) continue;
            _repo.SetInstalled(g.Id, false);
            missing++;
        }

        var summary = new ScanSummary(added, updated, missing, scanned.ToList(), failed);
        Log.Info("Analyse des jeux terminée : " + summary.ToText());
        return summary;
    }

    private static string Norm(string path) => path.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
}
