using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Store;

namespace Nexus.Shell.Pages;

/// <summary>
/// Boutique (une catégorie à la fois) et Découvrir (trois rangées). Source : catalogue public Steam.
/// L'achat et le téléchargement passent toujours par le site officiel (navigateur intégré).
/// </summary>
public sealed class StorePage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly bool _discover;
    private readonly SteamStoreSource _source = new();
    private readonly Border _area = new();
    private readonly TextBlock _status = Ui.Text("", 14, dim: true);
    private CancellationTokenSource? _cts;
    private int _category;
    private bool _loadedOnce;

    public string Title => _discover ? "Découvrir" : "Boutique";

    public StorePage(IShell shell, bool discover)
    {
        _shell = shell;
        _discover = discover;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var head = new StackPanel();
        head.Children.Add(Ui.Text(Title, 36, weight: FontWeights.Bold));
        var sub = Ui.Text("Catalogue Steam (public). L'achat et le téléchargement se font sur le site officiel, avec ta propre connexion.", 14, dim: true);
        sub.Margin = new Thickness(0, 4, 0, 8);
        head.Children.Add(sub);

        if (!discover)
        {
            var cat = Ui.Card("Catégorie", _source.Categories[0].Label, null, out var catSub);
            cat.Click += (_, _) =>
            {
                _category = (_category + 1) % _source.Categories.Count;
                catSub.Text = _source.Categories[_category].Label;
                _ = LoadAsync();
            };
            head.Children.Add(cat);
        }
        head.Children.Add(_status);
        root.Children.Add(head);
        Grid.SetRow(_area, 1);
        root.Children.Add(_area);
        Content = root;
    }

    public void OnShown()
    {
        if (!_loadedOnce) { _loadedOnce = true; _ = LoadAsync(); }
    }

    public void OnHidden() => _cts?.Cancel();
    public bool HandleAction(NavAction action) => false;

    private async Task LoadAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        _status.Text = "Chargement…";
        try
        {
            if (_discover)
            {
                var host = new StackPanel();
                foreach (var (key, label) in _source.Categories.Take(3))
                {
                    var items = await _source.GetCategoryAsync(key, cts.Token);
                    if (items.Count == 0) continue;
                    var t = Ui.Text(label, 22, weight: FontWeights.SemiBold);
                    t.Margin = new Thickness(4, 12, 0, 4);
                    host.Children.Add(t);
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    foreach (var it in items.Take(12)) row.Children.Add(Card(it, cts.Token));
                    host.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Content = row });
                }
                if (cts.IsCancellationRequested) return;
                _area.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = host };
            }
            else
            {
                var items = await _source.GetCategoryAsync(_source.Categories[_category].Key, cts.Token);
                if (cts.IsCancellationRequested) return;
                var wrap = new WrapPanel();
                foreach (var it in items) wrap.Children.Add(Card(it, cts.Token));
                _area.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = wrap };
                if (items.Count == 0) _status.Text = "Rien à afficher dans cette catégorie pour l'instant.";
            }
            if (!cts.IsCancellationRequested && _status.Text == "Chargement…") _status.Text = "";
        }
        catch (OperationCanceledException) { }
        catch (StoreUnavailableException ex) { if (!cts.IsCancellationRequested) _status.Text = ex.Message; }
        catch (Exception ex)
        {
            Log.Error("Chargement de la boutique impossible.", ex);
            if (!cts.IsCancellationRequested) _status.Text = "Les données de la boutique n'ont pas pu être lues (détails dans les journaux).";
        }
    }

    private Button Card(StoreItem item, CancellationToken ct) =>
        StoreCard.Create(item, _source.Cache, 280, () => _shell.Nav.Push(new StoreDetailPage(_shell, _source, item)), ct);
}
