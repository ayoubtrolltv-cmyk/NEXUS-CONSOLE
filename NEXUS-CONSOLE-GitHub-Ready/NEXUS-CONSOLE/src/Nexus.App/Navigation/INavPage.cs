using System.Windows.Media;
using Nexus.Core.Input;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Detection;
using Nexus.Input;
using Nexus.Shell.Services;

namespace Nexus.Shell.Navigation;

public interface INavPage
{
    string Title { get; }
    /// <summary>Retourne true si la page a consommé l'action ; sinon la navigation par défaut s'applique.</summary>
    bool HandleAction(NavAction action);
    void OnShown();
    void OnHidden();
}

public interface IShell
{
    SettingsService Settings { get; }
    GameRepository? Games { get; }
    GameScanner? Scanner { get; }
    ControllerManager Pad { get; }
    NavigationService Nav { get; }
    void GoToSection(string label);
    /// <summary>Teinte d'ambiance du fond ; null pour la retirer.</summary>
    void SetAmbient(Color? color);
    /// <summary>Applique et enregistre un profil de manette, puis met à jour les indications à l'écran.</summary>
    void ApplyPadProfile(PadProfile profile);
    /// <summary>Lance un jeu, masque NEXUS pendant la session et le réaffiche à la fermeture.</summary>
    Task LaunchAsync(Game game);
    /// <summary>Message bref en bas à gauche de l'écran.</summary>
    void Toast(string text);
}
