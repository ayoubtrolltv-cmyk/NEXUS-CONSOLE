using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Config;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>Chaque réglage est réel : il est appliqué immédiatement et enregistré en base.</summary>
public sealed class SettingsPage : UserControl, INavPage
{
    private static readonly (string Name, string Hex)[] Accents =
    {
        ("Bleu", "#3B82F6"), ("Violet", "#8B5CF6"), ("Rose", "#EC4899"),
        ("Rouge", "#EF4444"), ("Orange", "#F59E0B"), ("Vert", "#10B981")
    };
    private static readonly string[] AnimNames = { "Désactivées", "Réduites", "Complètes" };

    private readonly List<Action> _refresh = new();

    public string Title => "Paramètres";

    public SettingsPage(IShell shell)
    {
        var s = shell.Settings;
        var list = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var title = Ui.Text("Paramètres", 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 20);
        list.Children.Add(title);

        void Add(string label, Func<string> value, Action cycle)
        {
            var card = Ui.Card(label, value(), null, out var sub);
            card.Click += (_, _) => { cycle(); foreach (var r in _refresh) r(); };
            _refresh.Add(() => sub.Text = value());
            list.Children.Add(card);
        }

        Add("Thème", () => s.Current.Theme == "light" ? "Clair" : "Sombre",
            () => s.Update(c => c.Theme = c.Theme == "light" ? "dark" : "light"));

        Add("Couleur d'accentuation",
            () => Accents.FirstOrDefault(a => string.Equals(a.Hex, s.Current.Accent, StringComparison.OrdinalIgnoreCase)).Name ?? "Personnalisée",
            () =>
            {
                var i = Array.FindIndex(Accents, a => string.Equals(a.Hex, s.Current.Accent, StringComparison.OrdinalIgnoreCase));
                var next = Accents[(i + 1) % Accents.Length];
                s.Update(c => c.Accent = next.Hex);
            });

        Add("Animations", () => AnimNames[s.Current.AnimationLevel],
            () => s.Update(c => c.AnimationLevel = (c.AnimationLevel + 1) % 3));

        Add("Mode TV (interface agrandie)", () => s.Current.TvMode ? "Activé" : "Désactivé",
            () => s.Update(c => c.TvMode = !c.TvMode));

        Add("Plein écran au démarrage", () => s.Current.StartFullscreen ? "Activé" : "Désactivé",
            () => s.Update(c => c.StartFullscreen = !c.StartFullscreen));

        Add("Ambiance selon le jeu", () => s.Current.AmbientEnabled ? "Activée" : "Désactivée",
            () => s.Update(c => c.AmbientEnabled = !c.AmbientEnabled));

        Add("Taille de l'interface", () => s.Current.UiScalePercent == 0 ? "Automatique (selon la fenêtre)" : $"{s.Current.UiScalePercent} %",
            () =>
            {
                var steps = new[] { 0, 100, 125, 150 };
                var next = steps[(Array.IndexOf(steps, s.Current.UiScalePercent) + 1) % steps.Length];
                s.Update(c => c.UiScalePercent = next);
            });

        list.Children.Add(Ui.Card("Profils", "Réglages séparés par profil", () => shell.Nav.Push(new ProfilePage(shell)), out _));
        list.Children.Add(Ui.Card("Système et performances", "Démarrage, actualisation, économie de mémoire, cache, sauvegarde", () => shell.Nav.Push(new SystemSettingsPage(shell)), out _));
        list.Children.Add(Ui.Card("Manette", "Boutons, zone morte, test", () => shell.Nav.Push(new ControllerSettingsPage(shell)), out _));
        list.Children.Add(Ui.Card("Sons", "Volume et réglage de chaque son", () => shell.Nav.Push(new AudioSettingsPage(shell)), out _));
        list.Children.Add(Ui.Card("Ouvrir le dossier des journaux", AppPaths.Logs, OpenLogs, out _));
        list.Children.Add(Ui.Card("Réinitialiser les paramètres", "Revenir aux valeurs par défaut", () =>
        {
            s.Reset();
            foreach (var r in _refresh) r();
        }, out _));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = list };
    }

    private static void OpenLogs()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.Logs}\"") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Warn("Ouverture du dossier des journaux impossible.", ex); }
    }

    public bool HandleAction(NavAction action) => false;
    public void OnShown() { }
    public void OnHidden() { }
}
