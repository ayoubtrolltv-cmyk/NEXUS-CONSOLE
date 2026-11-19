using System.Text.Json;
using Nexus.Core.Input;

namespace Nexus.Input;

/// <summary>
/// Profil de manette : boutons associés aux actions configurables, zone morte, inversion de l'axe vertical.
/// Les directions restent sur la croix et le stick gauche. Sérialisé en JSON dans les paramètres.
/// </summary>
public sealed class PadProfile
{
    public static readonly NavAction[] Configurable =
        { NavAction.Accept, NavAction.Back, NavAction.Menu, NavAction.TabPrev, NavAction.TabNext };

    public Dictionary<string, string> Buttons { get; set; } = new();
    public int DeadzonePercent { get; set; } = 20;
    public bool InvertY { get; set; }

    public static PadProfile Default()
    {
        var p = new PadProfile();
        p.Set(NavAction.Accept, PadButton.South);
        p.Set(NavAction.Back, PadButton.East);
        p.Set(NavAction.Menu, PadButton.Start);
        p.Set(NavAction.TabPrev, PadButton.LeftBumper);
        p.Set(NavAction.TabNext, PadButton.RightBumper);
        return p;
    }

    public void Set(NavAction action, PadButton button) => Buttons[action.ToString()] = button.ToString();

    public PadButton ButtonFor(NavAction action) =>
        Buttons.TryGetValue(action.ToString(), out var s) && Enum.TryParse<PadButton>(s, out var b) ? b : Default().ButtonFor(action);

    public NavAction? ActionFor(PadButton button)
    {
        foreach (var a in Configurable)
            if (ButtonFor(a) == button) return a;
        return null;
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Lecture tolérante : un JSON absent ou invalide donne le profil par défaut.</summary>
    public static PadProfile FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var p = JsonSerializer.Deserialize<PadProfile>(json) ?? Default();
            p.DeadzonePercent = Math.Clamp(p.DeadzonePercent, 0, 50);
            return p;
        }
        catch { return Default(); }
    }
}
