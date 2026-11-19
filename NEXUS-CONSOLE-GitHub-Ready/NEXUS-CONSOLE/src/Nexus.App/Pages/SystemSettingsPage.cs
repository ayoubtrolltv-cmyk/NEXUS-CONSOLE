using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Config;
using Nexus.Core.Input;
using Nexus.Core.Logging;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;
using Nexus.Store;

namespace Nexus.Shell.Pages;

/// <summary>Système et performances : démarrage avec Windows, actualisation, économie de mémoire, cache, sauvegarde, assistant.</summary>
public sealed class SystemSettingsPage : UserControl, INavPage
{
    private static readonly int[] Intervals = { 0, 15, 30, 60 };
    private static readonly string[] IntervalNames = { "Jamais", "Toutes les 15 min", "Toutes les 30 min", "Toutes les heures" };

    public string Title => "Système et performances";

    public SystemSettingsPage(IShell shell)
    {
        var s = shell.Settings;
        var list = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var title = Ui.Text("Système et performances", 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 16);
        list.Children.Add(title);

        var auto = Ui.Card("Démarrer avec Windows", StartupRegistration.IsEnabled() ? "Activé" : "Désactivé", null, out var autoSub);
        auto.Click += (_, _) =>
        {
            var target = !StartupRegistration.IsEnabled();
            autoSub.Text = StartupRegistration.Set(target) ? (target ? "Activé" : "Désactivé") : "Impossible de modifier ce réglage (voir les journaux)";
        };
        list.Children.Add(auto);

        var scan = Ui.Card("Actualisation automatique des jeux", IntervalNames[Array.IndexOf(Intervals, s.Current.ScanMinutes)], null, out var scanSub);
        scan.Click += (_, _) =>
        {
            var next = Intervals[(Array.IndexOf(Intervals, s.Current.ScanMinutes) + 1) % Intervals.Length];
            s.Update(c => c.ScanMinutes = next);
            scanSub.Text = IntervalNames[Array.IndexOf(Intervals, next)];
        };
        list.Children.Add(scan);

        var eco = Ui.Card("Mode économie de mémoire", s.Current.LowMemory ? "Activé" : "Désactivé", null, out var ecoSub);
        eco.Click += (_, _) =>
        {
            s.Update(c => c.LowMemory = !c.LowMemory);
            ecoSub.Text = (s.Current.LowMemory ? "Activé" : "Désactivé") + " · cache d'images réduit tout de suite ; animations à 30 images/s au prochain lancement";
        };
        list.Children.Add(eco);

        var cache = Ui.Card("Vider le cache", "Images de la boutique et images décodées en mémoire", null, out var cacheSub);
        cache.Click += (_, _) =>
        {
            ImageCache.Clear();
            new StoreCache().Clear();
            GC.Collect();
            cacheSub.Text = "Cache vidé";
        };
        list.Children.Add(cache);

        var backup = Ui.Card("Sauvegarder les données", "Copie la base (jeux, profils, réglages) dans Documents", null, out var backupSub);
        backup.Click += (_, _) =>
        {
            try
            {
                var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"NexusConsole-sauvegarde-{DateTime.Now:yyyyMMdd-HHmm}.db");
                File.Copy(s.DatabasePath, target, overwrite: false);
                backupSub.Text = $"Sauvegardé : {target}";
            }
            catch (Exception ex)
            {
                Log.Warn("Sauvegarde impossible.", ex);
                backupSub.Text = "Sauvegarde impossible (voir les journaux).";
            }
        };
        list.Children.Add(backup);

        list.Children.Add(Ui.Card("Relancer l'assistant de premier démarrage", "Manette, jeux, cloud, personnalisation", () => shell.Nav.Push(new FirstRunPage(shell, pushed: true)), out _));
        list.Children.Add(Ui.Card("Retour", "Revenir aux paramètres", () => shell.Nav.Back(), out _));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = list };
    }

    public bool HandleAction(NavAction action) => false;
    public void OnShown() { }
    public void OnHidden() { }
}
