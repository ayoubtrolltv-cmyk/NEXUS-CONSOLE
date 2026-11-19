using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexus.Core.Input;
using Nexus.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;

namespace Nexus.Shell.Pages;

/// <summary>
/// Affiche l'état réel de la manette. Pendant le test les boutons ne naviguent pas :
/// on quitte avec Menu (Start) ou la touche Échap du clavier.
/// </summary>
public sealed class ControllerTestPage : UserControl, INavPage
{
    private readonly IShell _shell;
    private readonly TextBlock _view = new() { FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 20, TextWrapping = TextWrapping.Wrap };
    private long _lastUi;

    public string Title => "Test de la manette";

    public ControllerTestPage(IShell shell)
    {
        _shell = shell;
        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(Ui.Text("Test de la manette", 36, weight: FontWeights.Bold));
        var hint = Ui.Text("Appuie sur les boutons, bouge les sticks et les gâchettes. Quitter : Menu (Start) ou Échap.", 15, dim: true);
        hint.Margin = new Thickness(0, 8, 0, 24);
        sp.Children.Add(hint);
        sp.Children.Add(_view);
        Content = sp;
    }

    public void OnShown()
    {
        _shell.Pad.SnapshotRaised += OnSnapshot;
        _view.Text = _shell.Pad.Primary is null ? "Aucune manette détectée." : "En attente d'une action…";
    }

    public void OnHidden() => _shell.Pad.SnapshotRaised -= OnSnapshot;

    public bool HandleAction(NavAction action)
    {
        if (action == NavAction.Menu) { _shell.Nav.Back(); return true; }
        if (action == NavAction.Back && (Keyboard.IsKeyDown(Key.Escape) || Keyboard.IsKeyDown(Key.Back))) return false;   // sortie au clavier
        return true;   // tout le reste est avalé pour ne pas naviguer pendant le test
    }

    private void OnSnapshot(PadSnapshot s)
    {
        var now = Environment.TickCount64;
        if (now - _lastUi < 50) return;   // 20 rafraîchissements par seconde suffisent
        _lastUi = now;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var pressed = s.Pressed.Count == 0 ? "—" : string.Join("  ", s.Pressed.Select(b => Glyphs.Name(s.Info.Kind, b)));
            _view.Text =
                $"{s.Info.Name} · {s.Info.Connection}\n\n" +
                $"Boutons       : {pressed}\n" +
                $"Stick gauche  : {s.LX,6:0.00} ; {s.LY,6:0.00}\n" +
                $"Stick droit   : {s.RX,6:0.00} ; {s.RY,6:0.00}\n" +
                $"Gâchettes     : G {s.LT:0.00}   D {s.RT:0.00}";
        }));
    }
}
