using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Nexus.Core.Config;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>
/// Navigateur intégré (WebView2 / Edge). Créé à l'affichage, détruit à la fermeture. Sessions et cookies conservés dans un dossier propre à NEXUS.
/// Manette : haut/bas défilent, gauche/droite changent d'élément, valider clique, retour revient en arrière puis ferme, menu ouvre le navigateur Windows.
/// Si le moteur est absent ou échoue, la page s'ouvre dans le navigateur Windows par défaut.
/// </summary>
public sealed class BrowserPage : UserControl, INavPage
{
    private const string FocusScript =
        "(function(d){var e=Array.from(document.querySelectorAll('a[href],button,input,select,textarea,[tabindex]:not([tabindex=\"-1\"])')).filter(function(x){var r=x.getBoundingClientRect();return r.width>0&&r.height>0&&getComputedStyle(x).visibility!=='hidden';});if(!e.length)return;var i=e.indexOf(document.activeElement);i=i<0?(d>0?0:e.length-1):(i+d+e.length)%e.length;e[i].focus();e[i].scrollIntoView({block:'center'});})";
    private const string ClickScript = "(function(){var e=document.activeElement;if(e&&e!==document.body)e.click();})()";

    private readonly IShell _shell;
    private readonly string _url;
    private readonly Border _host = new();
    private readonly TextBlock _address = Ui.Text("", 14, dim: true);
    private WebView2? _web;
    private bool _disposed;

    public string Title => "Navigateur";

    public BrowserPage(IShell shell, string url)
    {
        _shell = shell;
        _url = url;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        bar.Children.Add(Tab("Fermer", () => shell.Nav.Back()));
        bar.Children.Add(Tab("Précédent", () => { if (_web?.CanGoBack == true) _web.GoBack(); }));
        bar.Children.Add(Tab("Suivant", () => { if (_web?.CanGoForward == true) _web.GoForward(); }));
        bar.Children.Add(Tab("Recharger", () => _web?.Reload()));
        bar.Children.Add(Tab("Navigateur Windows", OpenExternal));
        var head = new StackPanel();
        head.Children.Add(bar);
        head.Children.Add(_address);
        root.Children.Add(head);

        Grid.SetRow(_host, 1);
        _host.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(_host);
        Content = root;
        _address.Text = url;
    }

    private static Button Tab(string label, Action onClick)
    {
        var b = new Button { Content = label, Style = (Style)Application.Current.FindResource("NavTab") };
        b.Click += (_, _) => onClick();
        return b;
    }

    public void OnShown() => _ = InitAsync();

    public void OnHidden()
    {
        _disposed = true;
        _host.Child = null;
        _web?.Dispose();     // libère le processus du moteur
        _web = null;
    }

    private async Task InitAsync()
    {
        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();   // lève une exception si le runtime WebView2 est absent
            var web = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(AppPaths.Root, "webview2") }
            };
            _web = web;
            _host.Child = web;
            await web.EnsureCoreWebView2Async();
            if (_disposed) return;
            web.SourceChanged += (_, _) => { if (!_disposed && web.Source is not null) _address.Text = web.Source.ToString(); };
            web.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; web.CoreWebView2.Navigate(e.Uri); };   // pas de fenêtres flottantes
            web.CoreWebView2.Navigate(_url);
        }
        catch (Exception ex)
        {
            Log.Warn("Navigateur intégré indisponible, ouverture dans le navigateur Windows.", ex);
            if (_disposed) return;
            OpenExternal();
            _shell.Toast("Navigateur intégré indisponible : page ouverte dans le navigateur Windows.");
            _shell.Nav.Back();
        }
    }

    public bool HandleAction(NavAction action)
    {
        var web = _web;
        if (web?.CoreWebView2 is null) return action != NavAction.Back;   // pendant le chargement, seul Retour ferme

        switch (action)
        {
            case NavAction.Up: _ = web.ExecuteScriptAsync("window.scrollBy(0,-260)"); return true;
            case NavAction.Down: _ = web.ExecuteScriptAsync("window.scrollBy(0,260)"); return true;
            case NavAction.Left: _ = web.ExecuteScriptAsync(FocusScript + "(-1)"); return true;
            case NavAction.Right: _ = web.ExecuteScriptAsync(FocusScript + "(1)"); return true;
            case NavAction.Accept: _ = web.ExecuteScriptAsync(ClickScript); return true;
            case NavAction.Back:
                if (web.CanGoBack) { web.GoBack(); return true; }
                return false;                                  // plus d'historique : la navigation ferme la page
            case NavAction.Menu: OpenExternal(); return true;
            default: return true;                              // sections : ignorées dans le navigateur
        }
    }

    private void OpenExternal()
    {
        try { Process.Start(new ProcessStartInfo(_web?.Source?.ToString() ?? _url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Navigateur par défaut introuvable.", ex); }
    }
}
