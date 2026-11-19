using System.Runtime.InteropServices;

namespace Nexus.Input;

internal static class XInputNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad { public ushort Buttons; public byte LT; public byte RT; public short LX, LY, RX, RY; }

    [StructLayout(LayoutKind.Sequential)]
    public struct State { public uint Packet; public Gamepad Gamepad; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Vibration { public ushort Left, Right; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Capabilities { public byte Type; public byte SubType; public ushort Flags; public Gamepad Gamepad; public Vibration Vibration; }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint GetState(uint index, out State state);

    // Fonction non documentée (ordinal 100) : donne aussi le bouton Guide. Repli sur GetState si absente.
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern uint GetStateEx(uint index, out State state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    private static extern uint GetCaps(uint index, uint flags, out Capabilities caps);

    private static bool _exMissing;
    public static bool LibraryMissing { get; private set; }

    public static bool TryGetState(int slot, out State state)
    {
        state = default;
        if (LibraryMissing) return false;
        try
        {
            if (!_exMissing)
            {
                try { return GetStateEx((uint)slot, out state) == 0; }
                catch (EntryPointNotFoundException) { _exMissing = true; }
            }
            return GetState((uint)slot, out state) == 0;
        }
        catch (DllNotFoundException) { LibraryMissing = true; return false; }
    }

    public static bool TryGetCaps(int slot, out Capabilities caps)
    {
        caps = default;
        try { return GetCaps((uint)slot, 0, out caps) == 0; }
        catch (Exception) { return false; }
    }
}
