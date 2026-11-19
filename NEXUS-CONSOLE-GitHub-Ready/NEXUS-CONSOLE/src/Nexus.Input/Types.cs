namespace Nexus.Input;

/// <summary>Boutons physiques, indépendants du modèle (South = croix PlayStation / A Xbox).</summary>
public enum PadButton
{
    South, East, West, North, LeftBumper, RightBumper, LeftTrigger, RightTrigger,
    LeftStick, RightStick, Start, Select, Guide, DpadUp, DpadDown, DpadLeft, DpadRight
}

public enum ControllerKind { Xbox, PlayStation, Generic }

public sealed record ControllerInfo(int Slot, string Name, ControllerKind Kind, string Connection, string Capabilities);

public sealed record PadSnapshot(ControllerInfo Info, IReadOnlyList<PadButton> Pressed,
    float LX, float LY, float RX, float RY, float LT, float RT);
