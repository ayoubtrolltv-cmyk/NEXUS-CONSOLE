using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Input;
using Nexus.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>État de la manette principale, configuration des boutons, zone morte, inversion d'axe, test.</summary>
public sealed class ControllerSettingsPage : UserControl, INavPage
{
    private static readonly int[] Zones = { 5, 10, 15, 20, 25, 30 };

    private readonly IShell _shell;
    private readonly TextBlock _status = Ui.Text("", 16, dim: true);

    public string Title => "Manette";

    public ControllerSettingsPage(IShell shell)
    {
        _shell = shell;
        var list = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var title = Ui.Text("Manette", 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 8);
        list.Children.Add(title);
        _status.Margin = new Thickness(0, 0, 0, 16);
        list.Children.Add(_status);

        list.Children.Add(Ui.Card("Configurer les boutons", "Valider, retour, menu, sections : appuie sur le bouton voulu", () => shell.Nav.Push(new ControllerWizardPage(shell)), out _));

        var dz = Ui.Card("Zone morte des sticks", $"{shell.Pad.Profile.DeadzonePercent} %", null, out var dzSub);
        dz.Click += (_, _) =>
        {
            var p = shell.Pad.Profile;
            var i = Array.FindIndex(Zones, z => z > p.DeadzonePercent);
            p.DeadzonePercent = i < 0 ? Zones[0] : Zones[i];
            shell.ApplyPadProfile(p);
            dzSub.Text = $"{p.DeadzonePercent} %";
        };
        list.Children.Add(dz);

        var inv = Ui.Card("Inverser l'axe vertical", shell.Pad.Profile.InvertY ? "Activé" : "Désactivé", null, out var invSub);
        inv.Click += (_, _) =>
        {
            var p = shell.Pad.Profile;
            p.InvertY = !p.InvertY;
            shell.ApplyPadProfile(p);
            invSub.Text = p.InvertY ? "Activé" : "Désactivé";
        };
        list.Children.Add(inv);

        list.Children.Add(Ui.Card("Tester la manette", "Boutons, sticks et gâchettes en direct", () => shell.Nav.Push(new ControllerTestPage(shell)), out _));

        var reset = Ui.Card("Restaurer la configuration par défaut", "Boutons, zone morte et axes", null, out _);
        reset.Click += (_, _) =>
        {
            shell.ApplyPadProfile(PadProfile.Default());
            dzSub.Text = $"{shell.Pad.Profile.DeadzonePercent} %";
            invSub.Text = "Désactivé";
            UpdateStatus();
        };
        list.Children.Add(reset);
        list.Children.Add(Ui.Card("Retour", "Revenir aux paramètres", () => shell.Nav.Back(), out _));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = list };
    }

    public void OnShown()
    {
        _shell.Pad.ConnectionChanged += OnConnection;
        UpdateStatus();
    }

    public void OnHidden() => _shell.Pad.ConnectionChanged -= OnConnection;
    public bool HandleAction(NavAction action) => false;

    private void OnConnection() => Dispatcher.BeginInvoke(new Action(UpdateStatus));

    private void UpdateStatus()
    {
        var info = _shell.Pad.Primary;
        if (info is null)
        {
            _status.Text = "Aucune manette détectée. Branche ou allume une manette Xbox (ou compatible XInput).";
            return;
        }
        var p = _shell.Pad.Profile;
        string G(NavAction a) => Glyphs.Name(info.Kind, p.ButtonFor(a));
        _status.Text = $"{info.Name} · {info.Connection}\n{info.Capabilities}\n" +
                       $"Valider : {G(NavAction.Accept)} · Retour : {G(NavAction.Back)} · Menu : {G(NavAction.Menu)} · Sections : {G(NavAction.TabPrev)} / {G(NavAction.TabNext)}";
    }
}
