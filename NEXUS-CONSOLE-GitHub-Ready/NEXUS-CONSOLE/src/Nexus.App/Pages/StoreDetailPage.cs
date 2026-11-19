using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Store;

namespace Nexus.Shell.Pages;

/// <summary>Fiche immersive d'un jeu : visuel, description, informations, captures, prix, accès à la page officielle.</summary>
public sealed class StoreDetailPage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly SteamStoreSource _source;
    private readonly StoreItem _item;
    private readonly TextBlock _desc = Ui.Text("Chargement des détails…", 16, dim: true);
    private readonly TextBlock _info = Ui.Text("", 14, dim: true);
    private readonly StackPanel _shots = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
    private readonly Border _hero = new() { Height = 220, CornerRadius = new CornerRadius(18) };
    private CancellationTokenSource? _cts;

    public string Title => _item.Name;

    public StoreDetailPage(IShell shell, SteamStoreSource source, StoreItem item)
    {
        _shell = shell; _source = source; _item = item;
        _hero.Background = GameCard.Gradient(GameVisuals.ColorFor(item.Name));

        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(_hero);
        var name = Ui.Text(item.Name, 40, weight: FontWeights.Bold);
        name.Margin = new Thickness(0, 14, 0, 0);
        sp.Children.Add(name);
        var price = item.DiscountPercent > 0 ? $"-{item.DiscountPercent} %  {item.PriceText}  (avant {item.OriginalPriceText})" : item.PriceText ?? "Prix non communiqué";
        sp.Children.Add(Ui.Text($"{price}  ·  Boutique : Steam", 16));
        _desc.Margin = new Thickness(0, 12, 0, 4);
        sp.Children.Add(_desc);
        sp.Children.Add(_info);
        sp.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Content = _shots });

        var url = source.PageUrl(item.Id);
        sp.Children.Add(Ui.Card("Acheter ou télécharger sur Steam", "Ouvre la page officielle dans le navigateur intégré", () => shell.Nav.Push(new BrowserPage(shell, url)), out _));
        sp.Children.Add(Ui.Card("Ouvrir dans le navigateur Windows", "Page officielle, navigateur par défaut", () => OpenExternal(url), out _));
        sp.Children.Add(Ui.Card("Retour", "Revenir à la boutique", () => shell.Nav.Back(), out _));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = sp };
    }

    public void OnShown()
    {
        _shell.SetAmbient(GameVisuals.ColorFor(_item.Name));
        _cts = new CancellationTokenSource();
        _ = LoadAsync(_cts.Token);
    }

    public void OnHidden() { _cts?.Cancel(); _shell.SetAmbient(null); }
    public bool HandleAction(NavAction action) => false;

    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            _ = StoreCard.LoadImageAsync(_hero, _source.Cache, _item.ImageUrl, 1000, ct);
            var d = await _source.GetDetailsAsync(_item.Id, ct);
            if (ct.IsCancellationRequested) return;
            if (d is null) { _desc.Text = "Détails indisponibles pour ce jeu."; return; }

            _desc.Text = string.IsNullOrWhiteSpace(d.Description) ? "Pas de description disponible." : System.Net.WebUtility.HtmlDecode(d.Description);
            _info.Text = string.Join("\n", new[]
            {
                d.Genres.Count > 0 ? "Genres : " + string.Join(", ", d.Genres) : null,
                d.Developers is null ? null : "Développeur : " + d.Developers,
                d.Publishers is null ? null : "Éditeur : " + d.Publishers,
                d.IsFree ? "Jeu gratuit" : null
            }.Where(x => x is not null));

            foreach (var shot in d.Screenshots.Take(3))
            {
                var border = new Border { Width = 280, Height = 158, Margin = new Thickness(0, 0, 10, 0), CornerRadius = new CornerRadius(10), Background = Brushes.Black };
                _shots.Children.Add(border);
                _ = StoreCard.LoadImageAsync(border, _source.Cache, shot, 560, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (StoreUnavailableException ex) { if (!ct.IsCancellationRequested) _desc.Text = ex.Message; }
        catch (Exception ex)
        {
            Log.Error("Détails de boutique illisibles.", ex);
            if (!ct.IsCancellationRequested) _desc.Text = "Les détails n'ont pas pu être lus (voir les journaux).";
        }
    }

    private void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Navigateur par défaut introuvable.", ex); _shell.Toast("Impossible d'ouvrir le navigateur Windows."); }
    }
}
