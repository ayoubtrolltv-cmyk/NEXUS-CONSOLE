using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexus.Core.Input;
using Nexus.Core.Models;
using Nexus.Detection;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>Accueil : jeu à reprendre en grand, jeux récents, actions. Données réelles de la base uniquement.</summary>
public sealed class HomePage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly StackPanel _root = new();

    public string Title => "Accueil";

    public HomePage(IShell shell)
    {
        _shell = shell;
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = _root };
    }

    public void OnShown()
    {
        if (_shell.Scanner is { } s) s.Completed += OnScan;
        Rebuild();
    }

    public void OnHidden()
    {
        if (_shell.Scanner is { } s) s.Completed -= OnScan;
        _shell.SetAmbient(null);
    }

    // Reconstruit seulement si la détection a changé la liste (évite de faire sauter le focus pour rien).
    private void OnScan(ScanSummary r)
    {
        if (r.Added > 0 || r.Missing > 0) Dispatcher.BeginInvoke(new Action(Rebuild));
    }
    public bool HandleAction(NavAction action) => false;

    private void Rebuild()
    {
        _root.Children.Clear();
        var games = (_shell.Games?.List(includeHidden: false) ?? new List<Game>()).Where(g => g.Installed).ToList();

        if (games.Count == 0) { BuildEmpty(); _shell.SetAmbient(null); return; }

        var ordered = games.OrderByDescending(g => g.LastPlayedUtc ?? DateTime.MinValue).ThenByDescending(g => g.AddedUtc).ToList();
        var hero = ordered[0];
        _root.Children.Add(BuildHero(hero));

        var title = Ui.Text("Bibliothèque récente", 22, weight: FontWeights.SemiBold);
        title.Margin = new Thickness(4, 0, 0, 8);
        _root.Children.Add(title);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var g in ordered.Take(12))
            row.Children.Add(GameCard.Create(g, 170, () => _shell.Nav.Push(new GameDetailPage(_shell, g)),
                x => _shell.SetAmbient(GameVisuals.ColorFor(x.Name))));
        _root.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false, Content = row
        });

        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        actions.Children.Add(Ui.Card("Ouvrir la bibliothèque", "Tous les jeux, filtres et tris", () => _shell.GoToSection("Bibliothèque"), out _));
        _root.Children.Add(actions);

        _shell.SetAmbient(GameVisuals.ColorFor(hero.Name));
    }

    private Border BuildHero(Game hero)
    {
        var color = GameVisuals.ColorFor(hero.Name);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var kicker = Ui.Text(hero.LastPlayedUtc is null ? "DERNIER AJOUT" : "REPRENDRE", 13, weight: FontWeights.SemiBold);
        kicker.Foreground = new SolidColorBrush(Color.FromArgb(0xB8, 255, 255, 255));
        var name = Ui.Text(hero.Name, 46, weight: FontWeights.Bold);
        name.Foreground = Brushes.White;
        var info = Ui.Text(
            (hero.LastPlayedUtc is { } d ? $"Dernière utilisation : {d.ToLocalTime().ToString("d MMMM yyyy", GameVisuals.Fr)}" : "Pas encore lancé depuis NEXUS")
            + " · " + GameVisuals.FormatPlaytime(hero.PlaytimeSeconds), 15);
        info.Foreground = new SolidColorBrush(Color.FromArgb(0xD0, 255, 255, 255));
        info.Margin = new Thickness(0, 6, 0, 18);
        left.Children.Add(kicker);
        left.Children.Add(name);
        left.Children.Add(info);
        left.Children.Add(Ui.Card(hero.LastPlayedUtc is null ? "Lancer" : "Reprendre", "Lance le jeu ; NEXUS revient à sa fermeture", () => _ = _shell.LaunchAsync(hero), out _));
        left.Children.Add(Ui.Card("Voir la fiche", "Détails et réglages du jeu", () => _shell.Nav.Push(new GameDetailPage(_shell, hero)), out _));
        grid.Children.Add(left);

        var icon = ImageCache.Load(hero.CoverPath ?? hero.IconPath, 192);
        if (icon is not null)
        {
            var img = new Image { Source = icon, Width = 128, Height = 128, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(img, 1);
            grid.Children.Add(img);
        }

        return new Border { CornerRadius = new CornerRadius(22), Padding = new Thickness(40), Margin = new Thickness(0, 0, 0, 28), Background = GameCard.Gradient(color), Child = grid };
    }

    private void BuildEmpty()
    {
        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 80, 0, 0) };
        sp.Children.Add(Ui.Text("Bienvenue sur NEXUS", 44, weight: FontWeights.Bold));
        var d = Ui.Text("Ta bibliothèque est vide. La détection automatique des jeux arrive en Phase 5 ; en attendant, tu peux ajouter un jeu à la main (un .exe ou un raccourci).", 18, dim: true);
        d.Margin = new Thickness(0, 8, 0, 28);
        sp.Children.Add(d);

        var status = Ui.Text("", 14, dim: true);
        var add = Ui.Card("Ajouter un jeu", "Choisir un .exe ou un raccourci", null, out _);
        add.Click += (_, _) =>
        {
            if (_shell.Games is null) { status.Text = "La base de données est indisponible (voir les journaux)."; return; }
            var r = GameImport.PickAndAdd(_shell.Games);
            if (r.Game is not null) Rebuild(); else status.Text = r.Message;
        };
        sp.Children.Add(add);
        sp.Children.Add(Ui.Card("Paramètres", "Apparence, sons, mode TV", () => _shell.GoToSection("Paramètres"), out _));
        sp.Children.Add(status);
        _root.Children.Add(sp);
    }
}
