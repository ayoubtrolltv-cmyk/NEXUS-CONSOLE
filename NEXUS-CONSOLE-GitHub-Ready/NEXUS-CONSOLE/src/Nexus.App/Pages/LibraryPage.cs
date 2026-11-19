using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Config;
using Nexus.Core.Input;
using Nexus.Core.Models;
using Nexus.Detection;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>Bibliothèque : grille ou rangée horizontale, filtres, tris, ajout manuel. Les choix sont mémorisés.</summary>
public sealed class LibraryPage : UserControl, INavPage
{
    private static readonly string[] FilterNames = { "Tous", "Favoris", "Récents", "Masqués" };
    private static readonly string[] SortNames = { "A → Z", "Dernière utilisation" };
    private static readonly string[] ViewNames = { "Grille", "Horizontale" };

    private readonly IShell _shell;
    private readonly Border _area = new();
    private readonly TextBlock _count = Ui.Text("", 14, dim: true);
    private readonly TextBlock _status = Ui.Text("", 14, dim: true);

    public string Title => "Bibliothèque";

    public LibraryPage(IShell shell)
    {
        _shell = shell;
        var s = shell.Settings;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var controls = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        controls.Children.Add(Cycle("Filtre", AppSettings.LibraryFilters, FilterNames, () => s.Current.LibraryFilter, v => s.Update(c => c.LibraryFilter = v)));
        controls.Children.Add(Cycle("Tri", AppSettings.LibrarySorts, SortNames, () => s.Current.LibrarySort, v => s.Update(c => c.LibrarySort = v)));
        controls.Children.Add(Cycle("Affichage", AppSettings.LibraryViews, ViewNames, () => s.Current.LibraryView, v => s.Update(c => c.LibraryView = v)));

        var add = Ui.Card("Ajouter un jeu", "Choisir un .exe ou un raccourci", null, out _);
        add.Click += (_, _) =>
        {
            if (shell.Games is null) { _status.Text = "La base de données est indisponible (voir les journaux)."; return; }
            _status.Text = GameImport.PickAndAdd(shell.Games).Message;
            Fill();
        };
        controls.Children.Add(add);

        var refresh = Ui.Card("Actualiser", "Rechercher Steam, Epic, GOG, Ubisoft", null, out _);
        refresh.Click += async (_, _) =>
        {
            if (shell.Scanner is null) { _status.Text = "Détection indisponible (base de données inaccessible)."; return; }
            _status.Text = "Recherche en cours…";
            var r = await shell.Scanner.ScanAsync();
            _status.Text = r is null ? "Une recherche est déjà en cours." : r.ToText();
            Fill();
        };
        controls.Children.Add(refresh);

        var head = new StackPanel();
        head.Children.Add(Ui.Text("Bibliothèque", 36, weight: FontWeights.Bold));
        head.Children.Add(controls);
        root.Children.Add(head);

        var info = new StackPanel { Margin = new Thickness(4, 0, 0, 8) };
        info.Children.Add(_count);
        info.Children.Add(_status);
        Grid.SetRow(info, 1);
        root.Children.Add(info);

        Grid.SetRow(_area, 2);
        root.Children.Add(_area);
        Content = root;
    }

    private Button Cycle(string label, string[] values, string[] names, Func<string> get, Action<string> set)
    {
        var card = Ui.Card(label, names[Math.Max(0, Array.IndexOf(values, get()))], null, out var sub);
        card.Click += (_, _) =>
        {
            var next = values[(Math.Max(0, Array.IndexOf(values, get())) + 1) % values.Length];
            set(next);
            sub.Text = names[Array.IndexOf(values, next)];
            Fill();
        };
        return card;
    }

    public void OnShown()
    {
        if (_shell.Scanner is { } s) s.Completed += OnScan;
        Fill();
    }

    public void OnHidden()
    {
        if (_shell.Scanner is { } s) s.Completed -= OnScan;
        _shell.SetAmbient(null);
    }

    private void OnScan(ScanSummary r)
    {
        if (r.Added > 0 || r.Missing > 0) Dispatcher.BeginInvoke(new Action(Fill));
    }
    public bool HandleAction(NavAction action) => false;

    private void Fill()
    {
        var repo = _shell.Games;
        if (repo is null) { _area.Child = Ui.Text("La base de données est indisponible (voir les journaux).", 18, dim: true); return; }

        var s = _shell.Settings.Current;
        var all = repo.List(includeHidden: true);
        IEnumerable<Game> q = s.LibraryFilter switch
        {
            "fav" => all.Where(g => g.Favorite && !g.Hidden),
            "recent" => all.Where(g => g.LastPlayedUtc is not null && !g.Hidden),
            "hidden" => all.Where(g => g.Hidden),
            _ => all.Where(g => !g.Hidden)
        };
        q = s.LibrarySort == "recent"
            ? q.OrderByDescending(g => g.LastPlayedUtc ?? DateTime.MinValue).ThenByDescending(g => g.AddedUtc)
            : q.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase);
        var list = q.ToList();
        _count.Text = list.Count == 1 ? "1 jeu" : $"{list.Count} jeux";

        if (list.Count == 0)
        {
            _area.Child = Ui.Text(all.Count == 0
                ? "Aucun jeu pour l'instant. Ajoute-en un avec « Ajouter un jeu » ; la détection automatique remplit la liste au démarrage."
                : "Aucun jeu dans cette vue.", 18, dim: true);
            return;
        }

        var row = s.LibraryView == "row";
        var width = row ? 220 : 190;
        Panel panel = row ? new StackPanel { Orientation = Orientation.Horizontal } : new WrapPanel();
        foreach (var g in list)
            panel.Children.Add(GameCard.Create(g, width, () => _shell.Nav.Push(new GameDetailPage(_shell, g)),
                x => _shell.SetAmbient(GameVisuals.ColorFor(x.Name))));

        _area.Child = row
            ? new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Content = panel }
            : new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = panel };
    }
}
