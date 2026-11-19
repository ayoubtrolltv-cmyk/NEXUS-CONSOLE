using Nexus.Core.Logging;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Shell.Design;

namespace Nexus.Shell.Services;

public sealed record ImportResult(Game? Game, string Message);

/// <summary>Ajout manuel d'un jeu (avant la détection automatique de la Phase 5).</summary>
public static class GameImport
{
    public static ImportResult PickAndAdd(GameRepository repo)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choisir un jeu ou un raccourci",
            Filter = "Jeux et raccourcis (*.exe;*.lnk)|*.exe;*.lnk|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return new(null, "");

        var path = dlg.FileName;
        var game = new Game
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Platform = "manual",
            ExternalId = path.ToLowerInvariant(),
            ExePath = path,
            InstallPath = Path.GetDirectoryName(path),
            IconPath = GameVisuals.ExtractIcon(path),
            AddedUtc = DateTime.UtcNow
        };

        try
        {
            var added = repo.Add(game);
            return added is null
                ? new(null, "Ce jeu est déjà dans la bibliothèque.")
                : new(added, $"« {added.Name} » a été ajouté.");
        }
        catch (Exception ex)
        {
            Log.Error("Ajout manuel d'un jeu impossible.", ex);
            return new(null, "Impossible d'ajouter ce jeu (détails dans les journaux).");
        }
    }
}
