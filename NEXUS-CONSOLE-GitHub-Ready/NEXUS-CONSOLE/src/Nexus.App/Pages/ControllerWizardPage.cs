using System.Windows;
using System.Windows.Controls;
using Nexus.Core.Audio;
using Nexus.Core.Input;
using Nexus.Input;
using Nexus.Shell.Design;
using Nexus.Shell.Navigation;
using Nexus.Shell.Services;

namespace Nexus.Shell.Pages;

/// <summary>
/// Assistant : pour chaque action, détecte le bouton réellement pressé (aucune disposition supposée).
/// Un même bouton ne peut pas servir deux actions. Les directions restent sur la croix et le stick gauche.
/// </summary>
public sealed class ControllerWizardPage : UserControl, INavPage
{
    private static readonly (NavAction Action, string Label)[] Steps =
    {
        (NavAction.Accept, "VALIDER"), (NavAction.Back, "RETOUR"), (NavAction.Menu, "MENU / OPTIONS"),
        (NavAction.TabPrev, "SECTION PRÉCÉDENTE"), (NavAction.TabNext, "SECTION SUIVANTE"),
    };

    private readonly IShell _shell;
    private readonly PadProfile _profile;
    private readonly Dictionary<PadButton, string> _assigned = new();
    private readonly TextBlock _prompt = Ui.Text("", 30, weight: FontWeights.SemiBold);
    private readonly TextBlock _message = Ui.Text("", 15, dim: true);
    private readonly Button _done;
    private int _step;

    public string Title => "Configuration des boutons";

    public ControllerWizardPage(IShell shell)
    {
        _shell = shell;
        var current = shell.Pad.Profile;
        _profile = new PadProfile { DeadzonePercent = current.DeadzonePercent, InvertY = current.InvertY };

        var sp = new StackPanel { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(Ui.Text("Configuration des boutons", 36, weight: FontWeights.Bold));
        _prompt.Margin = new Thickness(0, 24, 0, 8);
        sp.Children.Add(_prompt);
        _message.Margin = new Thickness(0, 0, 0, 24);
        sp.Children.Add(_message);

        _done = Ui.Card("Terminer", "Revenir aux réglages de la manette", () => shell.Nav.Back(), out _);
        _done.Visibility = Visibility.Collapsed;
        sp.Children.Add(_done);
        Content = sp;
    }

    public void OnShown()
    {
        if (_shell.Pad.Primary is null)
        {
            _prompt.Text = "Aucune manette détectée";
            _message.Text = "Branche ou allume une manette, puis relance l'assistant. Échap pour revenir.";
            return;
        }
        _step = 0;
        _assigned.Clear();
        StartStep();
    }

    public void OnHidden() => _shell.Pad.CancelCapture();

    public bool HandleAction(NavAction action)
    {
        if (action == NavAction.Back) _shell.Pad.CancelCapture();   // Échap annule ; la navigation normale revient en arrière
        return false;
    }

    private void StartStep()
    {
        _prompt.Text = $"Appuie sur le bouton pour : {Steps[_step].Label}  ({_step + 1}/{Steps.Length})";
        _shell.Pad.CaptureNext(b => Dispatcher.BeginInvoke(new Action(() => OnCaptured(b))));
    }

    private void OnCaptured(PadButton button)
    {
        var kind = _shell.Pad.Primary?.Kind ?? ControllerKind.Xbox;
        var name = Glyphs.Name(kind, button);

        if (_assigned.TryGetValue(button, out var used))
        {
            _message.Text = $"{name} est déjà utilisé pour « {used} ». Choisis un autre bouton.";
            StartStep();
            return;
        }

        _profile.Set(Steps[_step].Action, button);
        _assigned[button] = Steps[_step].Label;
        _message.Text = $"{Steps[_step].Label} = {name}";
        SoundService.Play(SoundId.Confirm);

        if (++_step < Steps.Length) { StartStep(); return; }

        _shell.ApplyPadProfile(_profile);
        _prompt.Text = "Configuration enregistrée";
        _message.Text = string.Join("   ·   ", Steps.Select(s => $"{s.Label} = {Glyphs.Name(kind, _profile.ButtonFor(s.Action))}"));
        _done.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(new Action(() => _done.Focus()));
    }
}
