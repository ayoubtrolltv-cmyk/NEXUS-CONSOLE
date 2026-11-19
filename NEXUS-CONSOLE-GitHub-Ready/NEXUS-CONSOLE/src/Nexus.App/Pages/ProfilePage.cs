using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>
/// Profils locaux : chaque profil a ses propres réglages (thème, accent, sons, manette…).
/// Les jeux et leurs favoris sont communs à tous les profils (limite connue). Pas de renommage pour l'instant (pas de saisie de texte à la manette).
/// </summary>
public sealed class ProfilePage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly StackPanel _list = new() { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _status = Ui.Text("", 14, dim: true);

    public string Title => "Profils";

    public ProfilePage(IShell shell)
    {
        _shell = shell;
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = _list };
    }

    public void OnShown() => Rebuild();
    public void OnHidden() { }
    public bool HandleAction(NavAction action) => false;

    private void Rebuild()
    {
        _list.Children.Clear();
        var title = Ui.Text("Profils", 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 8);
        _list.Children.Add(title);
        _list.Children.Add(_status);

        var svc = _shell.Settings;
        if (svc.Profiles is null)
        {
            _list.Children.Add(Ui.Text("Les profils sont indisponibles (base de données inaccessible).", 16, dim: true));
            return;
        }

        foreach (var p in svc.Profiles.List())
        {
            var current = p.Id == svc.ProfileId;
            var id = p.Id;
            _list.Children.Add(Ui.Card(p.Name, current ? "Profil actif" : "Appuie pour passer à ce profil", () =>
            {
                if (id != svc.ProfileId && svc.SwitchProfile(id)) { _status.Text = $"Profil « {p.Name} » activé."; Rebuild(); }
            }, out _));
        }

        _list.Children.Add(Ui.Card("Nouveau profil", "Démarre avec une copie des réglages actuels", () =>
        {
            var created = svc.CreateProfile();
            _status.Text = created is null ? "Création impossible (voir les journaux)." : $"« {created.Name} » créé.";
            Rebuild();
        }, out _));

        _list.Children.Add(Ui.Card("Supprimer le profil actif", "Supprime ses réglages ; impossible pour le dernier profil", () =>
        {
            _status.Text = svc.DeleteCurrentProfile() ? "Profil supprimé." : "Impossible de supprimer le dernier profil.";
            Rebuild();
        }, out _));
        _list.Children.Add(Ui.Card("Retour", "Revenir aux paramètres", () => _shell.Nav.Back(), out _));
    }
}
