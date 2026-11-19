using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Audio;
using Nexus.Core.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>Réglages sonores : activation globale, volume, et chaque son séparément (un appui le bascule et le joue).</summary>
public sealed class AudioSettingsPage : UserControl, INavPage
{
    private static readonly (SoundId Id, string Name)[] Sounds =
    {
        (SoundId.Move, "Déplacement entre éléments"), (SoundId.Select, "Sélection"), (SoundId.Confirm, "Confirmation"),
        (SoundId.Back, "Retour"), (SoundId.MenuOpen, "Ouverture de menu"), (SoundId.MenuClose, "Fermeture de menu"),
        (SoundId.Launch, "Lancement d'un jeu (Phase 6)"), (SoundId.Return, "Retour à l'interface (Phase 6)")
    };
    private static readonly int[] Volumes = { 0, 20, 40, 60, 80, 100 };

    public string Title => "Sons";

    public AudioSettingsPage(IShell shell)
    {
        var s = shell.Settings;
        var list = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var title = Ui.Text("Sons", 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 8);
        list.Children.Add(title);
        var hint = Ui.Text("Bruitages d'interface uniquement, jamais de musique. Un appui sur un son le bascule et le joue s'il est activé.", 14, dim: true);
        hint.Margin = new Thickness(0, 0, 0, 16);
        list.Children.Add(hint);

        var master = Ui.Card("Effets sonores", s.Current.SoundsEnabled ? "Activés" : "Désactivés", null, out var masterSub);
        master.Click += (_, _) =>
        {
            s.Update(c => c.SoundsEnabled = !c.SoundsEnabled);
            masterSub.Text = s.Current.SoundsEnabled ? "Activés" : "Désactivés";
            SoundService.Play(SoundId.Select);
        };
        list.Children.Add(master);

        var vol = Ui.Card("Volume", $"{s.Current.SoundVolume} %", null, out var volSub);
        vol.Click += (_, _) =>
        {
            var i = Array.FindIndex(Volumes, v => v > s.Current.SoundVolume);
            var next = i < 0 ? Volumes[0] : Volumes[i];
            s.Update(c => c.SoundVolume = next);
            volSub.Text = $"{next} %";
            SoundService.Play(SoundId.Select);
        };
        list.Children.Add(vol);

        foreach (var (id, name) in Sounds)
        {
            var captured = id;
            var card = Ui.Card(name, s.Current.SoundsOff.Contains(id.ToString()) ? "Désactivé" : "Activé", null, out var sub);
            card.Click += (_, _) =>
            {
                s.Update(c => { if (!c.SoundsOff.Remove(captured.ToString())) c.SoundsOff.Add(captured.ToString()); });
                sub.Text = s.Current.SoundsOff.Contains(captured.ToString()) ? "Désactivé" : "Activé";
                SoundService.Play(captured);
            };
            list.Children.Add(card);
        }

        var testAll = Ui.Card("Tester tous les sons", "Joue chaque son activé, l'un après l'autre", null, out _);
        testAll.Click += async (_, _) =>
        {
            foreach (var (id, _) in Sounds) { SoundService.Play(id); await Task.Delay(600); }
        };
        list.Children.Add(testAll);
        list.Children.Add(Ui.Card("Retour", "Revenir aux paramètres", () => shell.Nav.Back(), out _));

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = list };
    }

    public bool HandleAction(NavAction action) => false;
    public void OnShown() { }
    public void OnHidden() { }
}
