using System.Windows;
using System.Windows.Controls;
using Nexus.Cloud;
using Nexus.Core.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>
/// Cloud Gaming (services) et Applications (applications natives détectées).
/// Lancement par le même mécanisme que les jeux : NEXUS se masque puis revient à la fermeture.
/// </summary>
public sealed class CloudPage : UserControl, INavPage
{
    private readonly bool _appsOnly;

    public string Title => _appsOnly ? "Applications" : "Cloud Gaming";

    public CloudPage(IShell shell, bool appsOnly)
    {
        _appsOnly = appsOnly;
        var list = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var title = Ui.Text(Title, 36, weight: FontWeights.Bold);
        title.Margin = new Thickness(0, 0, 0, 8);
        list.Children.Add(title);

        var services = CloudDetector.Detect();
        var edge = CloudDetector.FindEdge();
        var gfnInstalled = services.First(s => s.Id == "gfn").Installed;

        var shown = services.Where(s => appsOnly
            ? s.Kind == CloudKind.NativeApp && s.Installed
            : s.Id == "gfn" ? s.Installed : s.Id == "gfn-web" ? !gfnInstalled : true).ToList();

        var hint = Ui.Text(appsOnly
            ? (shown.Count == 0 ? "Aucune application détectée pour l'instant. Les services web se trouvent dans « Cloud Gaming »." : "Applications détectées sur ce PC.")
            : "Services de jeu dans le cloud. NEXUS ne voit rien de ce qui se passe à l'intérieur de ces services : il les ouvre, puis revient quand tu les fermes."
              + (gfnInstalled ? "" : " NVIDIA GeForce NOW n'est pas détecté sur ce PC : la version web est proposée."), 15, dim: true);
        hint.Margin = new Thickness(0, 0, 0, 16);
        list.Children.Add(hint);

        foreach (var s in shown)
        {
            var sub = s.Kind == CloudKind.NativeApp
                ? $"Installé · {s.ExePath}"
                : s.Description + (edge is null ? " · s'ouvre dans le navigateur par défaut (retour manuel)" : " · fenêtre Edge dédiée, connexion conservée");
            var captured = s;
            list.Children.Add(Ui.Card(s.Name, sub, () =>
                _ = shell.LaunchAsync(CloudLauncher.ToGame(captured, edge, shell.Settings.Current.GfnTracking == "folder")), out _));
        }

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = list };
    }

    public bool HandleAction(NavAction action) => false;
    public void OnShown() { }
    public void OnHidden() { }
}
