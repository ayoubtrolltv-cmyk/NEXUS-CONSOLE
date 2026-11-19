using System.Windows;
using System.Windows.Controls;
using Nexus.Cloud;
using Nexus.Core.Input;
using Nexus.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>Assistant de premier démarrage en six écrans : bienvenue, manette, jeux, cloud, personnalisation, résumé. Relançable depuis les paramètres.</summary>
public sealed class FirstRunPage : UserControl, INavPage
{
    private static readonly (string Name, string Hex)[] Accents =
    {
        ("Bleu", "#3B82F6"), ("Violet", "#8B5CF6"), ("Rose", "#EC4899"), ("Rouge", "#EF4444"), ("Orange", "#F59E0B"), ("Vert", "#10B981")
    };
    private static readonly int[] Volumes = { 0, 20, 40, 60, 80, 100 };

    private readonly IShell _shell;
    private readonly bool _pushed;
    private readonly StackPanel _panel = new() { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private int _step;
    private string _scanText = "";
    private bool _scanning, _scanDone;

    public string Title => "Premier démarrage";

    public FirstRunPage(IShell shell, bool pushed = false)
    {
        _shell = shell;
        _pushed = pushed;
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = _panel };
    }

    public void OnShown() => Render();    // aussi appelé au retour de l'assistant de manette : l'état est relu
    public void OnHidden() { }
    public bool HandleAction(NavAction action) => false;

    private void Render()
    {
        _panel.Children.Clear();
        var s = _shell.Settings;
        void Head(string text, string? sub = null)
        {
            _panel.Children.Add(Ui.Text(text, 38, weight: FontWeights.Bold));
            if (sub is not null) { var t = Ui.Text(sub, 16, dim: true); t.Margin = new Thickness(0, 8, 0, 24); _panel.Children.Add(t); }
        }
        void Next(string label = "Continuer") => _panel.Children.Add(Ui.Card(label, $"Étape {_step + 1} sur 6", () => { _step++; Render(); }, out _));

        switch (_step)
        {
            case 0:
                Head("Bienvenue sur NEXUS", "Transforme ton PC en console. Quelques réglages rapides, et tu pourras tout modifier plus tard dans les paramètres.");
                Next("Commencer");
                break;

            case 1:
            {
                var info = _shell.Pad.Primary;
                Head("Manette", info is null
                    ? "Aucune manette détectée. Branche ou allume une manette Xbox (ou compatible XInput) ; tu peux aussi passer cette étape."
                    : $"{info.Name} détectée ({info.Connection}).");
                if (info is not null)
                    _panel.Children.Add(Ui.Card("Configurer les boutons", "Valider, retour, menu, sections", () => _shell.Nav.Push(new ControllerWizardPage(_shell)), out _));
                Next();
                break;
            }

            case 2:
                Head("Recherche de jeux", "NEXUS analyse Steam, Epic Games, GOG et Ubisoft Connect.");
                _panel.Children.Add(Ui.Text(_scanText, 16));
                if (!_scanning && !_scanDone) _ = ScanAsync();
                if (_scanDone) Next();
                break;

            case 3:
            {
                var services = CloudDetector.Detect();
                var gfn = services.First(x => x.Id == "gfn");
                Head("Cloud Gaming", gfn.Installed
                    ? "NVIDIA GeForce NOW est installé : il apparaît dans Cloud Gaming et Applications."
                    : "NVIDIA GeForce NOW n'est pas détecté. Les services web (Xbox Cloud Gaming, Amazon Luna, GeForce NOW web) restent disponibles dans Cloud Gaming.");
                Next();
                break;
            }

            case 4:
            {
                Head("Personnalisation");
                var theme = Ui.Card("Thème", s.Current.Theme == "light" ? "Clair" : "Sombre", null, out var themeSub);
                theme.Click += (_, _) => { s.Update(c => c.Theme = c.Theme == "light" ? "dark" : "light"); themeSub.Text = s.Current.Theme == "light" ? "Clair" : "Sombre"; };
                _panel.Children.Add(theme);

                var accent = Ui.Card("Couleur d'accentuation", AccentName(s.Current.Accent), null, out var accentSub);
                accent.Click += (_, _) =>
                {
                    var i = Array.FindIndex(Accents, a => string.Equals(a.Hex, s.Current.Accent, StringComparison.OrdinalIgnoreCase));
                    var next = Accents[(i + 1) % Accents.Length];
                    s.Update(c => c.Accent = next.Hex);
                    accentSub.Text = next.Name;
                };
                _panel.Children.Add(accent);

                var vol = Ui.Card("Volume des effets sonores", $"{s.Current.SoundVolume} %", null, out var volSub);
                vol.Click += (_, _) =>
                {
                    var i = Array.FindIndex(Volumes, v => v > s.Current.SoundVolume);
                    var next = i < 0 ? Volumes[0] : Volumes[i];
                    s.Update(c => c.SoundVolume = next);
                    volSub.Text = $"{next} %";
                };
                _panel.Children.Add(vol);

                var view = Ui.Card("Affichage de la bibliothèque", s.Current.LibraryView == "row" ? "Horizontale" : "Grille", null, out var viewSub);
                view.Click += (_, _) => { s.Update(c => c.LibraryView = c.LibraryView == "row" ? "grid" : "row"); viewSub.Text = s.Current.LibraryView == "row" ? "Horizontale" : "Grille"; };
                _panel.Children.Add(view);
                Next();
                break;
            }

            default:
            {
                var info = _shell.Pad.Primary;
                var games = _shell.Games?.List().Count ?? 0;
                Head("Tout est prêt");
                _panel.Children.Add(Ui.Text(
                    $"Manette : {(info is null ? "aucune détectée" : info.Name)}\n" +
                    $"Jeux dans la bibliothèque : {games}\n" +
                    $"Thème : {(s.Current.Theme == "light" ? "clair" : "sombre")} · accent : {AccentName(s.Current.Accent)} · volume : {s.Current.SoundVolume} %", 17));
                _panel.Children.Add(new Border { Height = 24 });
                _panel.Children.Add(Ui.Card("Démarrer", "Ouvrir NEXUS", Finish, out _));
                break;
            }
        }
    }

    private static string AccentName(string hex) =>
        Accents.FirstOrDefault(a => string.Equals(a.Hex, hex, StringComparison.OrdinalIgnoreCase)).Name ?? "Personnalisée";

    private async Task ScanAsync()
    {
        if (_shell.Scanner is null) { _scanText = "Détection indisponible (base de données inaccessible)."; _scanDone = true; Render(); return; }
        _scanning = true;
        _scanText = "Recherche en cours…";
        Render();
        var r = await _shell.Scanner.ScanAsync();
        _scanning = false;
        _scanDone = true;
        _scanText = r is null ? "Une recherche était déjà en cours : elle se termine en arrière-plan." : r.ToText();
        if (_step == 2) Render();
    }

    private void Finish()
    {
        _shell.Settings.Update(c => c.FirstRunDone = true);
        if (_pushed) _shell.Nav.Back(); else _shell.GoToSection("Accueil");
    }
}
