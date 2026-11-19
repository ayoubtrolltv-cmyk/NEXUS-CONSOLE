namespace Nexus.Input;

/// <summary>Noms affichés des boutons, adaptés au modèle de manette.</summary>
public static class Glyphs
{
    public static string Name(ControllerKind kind, PadButton b) => kind == ControllerKind.PlayStation
        ? b switch
        {
            PadButton.South => "✕", PadButton.East => "○", PadButton.West => "□", PadButton.North => "△",
            PadButton.LeftBumper => "L1", PadButton.RightBumper => "R1", PadButton.LeftTrigger => "L2", PadButton.RightTrigger => "R2",
            PadButton.LeftStick => "L3", PadButton.RightStick => "R3", PadButton.Start => "Options", PadButton.Select => "Create",
            PadButton.Guide => "PS", _ => Dpad(b)
        }
        : b switch
        {
            PadButton.South => "A", PadButton.East => "B", PadButton.West => "X", PadButton.North => "Y",
            PadButton.LeftBumper => "LB", PadButton.RightBumper => "RB", PadButton.LeftTrigger => "LT", PadButton.RightTrigger => "RT",
            PadButton.LeftStick => "L3", PadButton.RightStick => "R3", PadButton.Start => "Menu", PadButton.Select => "Vue",
            PadButton.Guide => "Xbox", _ => Dpad(b)
        };

    private static string Dpad(PadButton b) => b switch
    {
        PadButton.DpadUp => "Pad ↑", PadButton.DpadDown => "Pad ↓", PadButton.DpadLeft => "Pad ←", PadButton.DpadRight => "Pad →", _ => b.ToString()
    };
}
