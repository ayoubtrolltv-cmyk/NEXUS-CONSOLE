using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Input;
using Nexus.Core.Models;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>Fiche d'un jeu : lancement, favori, visibilité, retrait de la bibliothèque.</summary>
public sealed class GameDetailPage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly Game _game;

    public string Title => _game.Name;

    public GameDetailPage(IShell shell, Game game)
    {
        _shell = shell;
        _game = game;
        var repo = shell.Games;

        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var icon = ImageCache.Load(game.CoverPath ?? game.IconPath, 160);
        if (icon is not null)
            sp.Children.Add(new Image { Source = icon, Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) });

        sp.Children.Add(Ui.Text(game.Name, 44, weight: FontWeights.Bold));
        var details = Ui.Text(
            $"{GameVisuals.PlatformLabel(game.Platform)}\n" +
            $"Fichier : {game.ExePath ?? "non renseigné"}\n" +
            $"Ajouté le {game.AddedUtc.ToLocalTime().ToString("d MMMM yyyy", GameVisuals.Fr)}\n" +
            (game.LastPlayedUtc is { } d ? $"Dernière utilisation : {d.ToLocalTime().ToString("d MMMM yyyy", GameVisuals.Fr)}\n" : "Pas encore lancé depuis NEXUS\n") +
            GameVisuals.FormatPlaytime(game.PlaytimeSeconds), 15, dim: true);
        details.Margin = new Thickness(0, 8, 0, 24);
        sp.Children.Add(details);

        if (game.Installed)
            sp.Children.Add(Ui.Card("Lancer", "Lance le jeu ; NEXUS revient à sa fermeture", () => _ = shell.LaunchAsync(game), out _));
        else
            sp.Children.Add(Ui.Text("Ce jeu n'est plus installé, ou sa plateforme est introuvable.", 15, dim: true));

        if (repo is not null)
        {
            var fav = Ui.Card("Favori", game.Favorite ? "Oui" : "Non", null, out var favSub);
            fav.Click += (_, _) => { game.Favorite = !game.Favorite; repo.SetFavorite(game.Id, game.Favorite); favSub.Text = game.Favorite ? "Oui" : "Non"; };
            sp.Children.Add(fav);

            var vis = Ui.Card("Visible dans la bibliothèque", game.Hidden ? "Non (masqué)" : "Oui", null, out var visSub);
            vis.Click += (_, _) => { game.Hidden = !game.Hidden; repo.SetHidden(game.Id, game.Hidden); visSub.Text = game.Hidden ? "Non (masqué)" : "Oui"; };
            sp.Children.Add(vis);

            sp.Children.Add(Ui.Card("Retirer de la bibliothèque", "Retire le jeu de NEXUS ; il n'est pas désinstallé", () =>
            {
                repo.Remove(game.Id);
                shell.Nav.Back();
            }, out _));
        }

        sp.Children.Add(Ui.Card("Retour", "Revenir à la liste", () => shell.Nav.Back(), out _));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = sp };
    }

    public void OnShown() => _shell.SetAmbient(GameVisuals.ColorFor(_game.Name));
    public void OnHidden() => _shell.SetAmbient(null);
    public bool HandleAction(NavAction action) => false;
}
