using System.Diagnostics;
using Nexus.Core.Input;
using Nexus.Core.Logging;

namespace Nexus.Input;

/// <summary>
/// Détection et lecture des manettes XInput (Xbox, et toute manette compatible XInput).
/// 60 Hz uniquement quand une manette est connectée et la fenêtre active ; sinon 1 Hz ; arrêt complet pendant un jeu.
/// Les événements sont levés sur un thread d'arrière-plan : l'interface doit les renvoyer vers son thread.
/// Limite connue : DualSense / DualShock 4 / DirectInput natifs ne sont pas lus (voir documentation).
/// </summary>
public sealed class ControllerManager : IDisposable
{
    private const int Slots = 4;
    private static readonly (ushort Bit, PadButton Button)[] Map =
    {
        (0x1000, PadButton.South), (0x2000, PadButton.East), (0x4000, PadButton.West), (0x8000, PadButton.North),
        (0x0100, PadButton.LeftBumper), (0x0200, PadButton.RightBumper), (0x0040, PadButton.LeftStick), (0x0080, PadButton.RightStick),
        (0x0010, PadButton.Start), (0x0020, PadButton.Select), (0x0400, PadButton.Guide),
        (0x0001, PadButton.DpadUp), (0x0002, PadButton.DpadDown), (0x0004, PadButton.DpadLeft), (0x0008, PadButton.DpadRight),
    };

    private readonly object _gate = new();
    private readonly System.Threading.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ControllerInfo?[] _infos = new ControllerInfo?[Slots];
    private readonly List<Action> _outbox = new();

    private long _lastProbeMs, _nextRepeatMs;
    private int _period = 1000, _primary = -1;
    private bool _suspended, _active = true, _warnedMissing;
    private ulong _prevMask;
    private NavAction? _heldDir;
    private Action<PadButton>? _capture;

    public PadProfile Profile { get; set; } = PadProfile.Default();
    public ControllerInfo? Primary { get { lock (_gate) return _primary >= 0 ? _infos[_primary] : null; } }
    public IReadOnlyList<ControllerInfo> Connected { get { lock (_gate) return _infos.OfType<ControllerInfo>().ToList(); } }

    public event Action<NavAction>? ActionRaised;
    public event Action<PadSnapshot>? SnapshotRaised;
    public event Action? ConnectionChanged;

    public ControllerManager() => _timer = new System.Threading.Timer(_ => Tick(), null, 0, 1000);

    // ---- Contrôle ----
    public void Suspend() { lock (_gate) { _suspended = true; _timer.Change(Timeout.Infinite, Timeout.Infinite); } }

    public void Resume()
    {
        lock (_gate)
        {
            _suspended = false;
            _prevMask = ulong.MaxValue;   // un bouton resté enfoncé après le jeu ne déclenche rien
            _heldDir = null;
            _period = -1;
            Rearm();
        }
    }

    public void SetActive(bool active) { lock (_gate) { _active = active; if (!_suspended) Rearm(); } }

    /// <summary>Le prochain bouton pressé est transmis au rappel au lieu de naviguer (assistant de configuration).</summary>
    public void CaptureNext(Action<PadButton> callback) { lock (_gate) _capture = callback; }
    public void CancelCapture() { lock (_gate) _capture = null; }

    public void Dispose() => _timer.Dispose();

    // ---- Boucle ----
    private void Tick()
    {
        List<Action>? run = null;
        lock (_gate)
        {
            if (_suspended) return;
            try { Poll(); }
            catch (Exception ex) { Log.Warn("Lecture des manettes en échec.", ex); }
            Rearm();
            if (_outbox.Count > 0) { run = new List<Action>(_outbox); _outbox.Clear(); }
        }
        if (run is null) return;
        foreach (var a in run)
        {
            try { a(); } catch (Exception ex) { Log.Warn("Gestionnaire de manette en échec.", ex); }
        }
    }

    private void Rearm()
    {
        var period = _primary >= 0 && _active ? 16 : 1000;
        if (period == _period) return;
        _period = period;
        _timer.Change(period, period);
    }

    private void Poll()
    {
        if (XInputNative.LibraryMissing)
        {
            if (!_warnedMissing) { _warnedMissing = true; Log.Warn("xinput1_4.dll introuvable : aucune manette XInput ne sera lue."); }
            return;
        }

        var now = _clock.ElapsedMilliseconds;
        var probe = now - _lastProbeMs >= 1000;
        if (probe) _lastProbeMs = now;

        var changed = false;
        XInputNative.State? primaryState = null;

        for (var slot = 0; slot < Slots; slot++)
        {
            var known = _infos[slot] is not null;
            if (!known && !probe) continue;

            if (!XInputNative.TryGetState(slot, out var st))
            {
                if (known)
                {
                    Log.Info($"Manette {slot} déconnectée.");
                    _infos[slot] = null;
                    changed = true;
                    if (_primary == slot) _primary = -1;
                }
                continue;
            }

            if (!known)
            {
                _infos[slot] = BuildInfo(slot);
                Log.Info($"Manette {slot} connectée : {_infos[slot]!.Name} ({_infos[slot]!.Connection}).");
                changed = true;
                if (_primary < 0) _primary = slot;
            }
            else if (_primary != slot && st.Gamepad.Buttons != 0) _primary = slot;   // la manette utilisée devient principale

            if (slot == _primary) primaryState = st;
        }

        if (_primary < 0)
            for (var s = 0; s < Slots; s++)
                if (_infos[s] is not null) { _primary = s; break; }

        if (changed) _outbox.Add(() => ConnectionChanged?.Invoke());
        if (primaryState is { } ps) Process(ps, now);
    }

    private void Process(XInputNative.State st, long now)
    {
        var gp = st.Gamepad;
        var profile = Profile;
        var dz = Math.Clamp(profile.DeadzonePercent, 0, 50) / 100f;
        var (lx, ly) = Axis(gp.LX, gp.LY, dz);
        var (rx, ry) = Axis(gp.RX, gp.RY, dz);
        if (profile.InvertY) { ly = -ly; ry = -ry; }
        float lt = gp.LT / 255f, rt = gp.RT / 255f;

        ulong mask = 0;
        foreach (var (bit, button) in Map)
            if ((gp.Buttons & bit) != 0) mask |= 1UL << (int)button;
        if (lt > 0.5f) mask |= 1UL << (int)PadButton.LeftTrigger;
        if (rt > 0.5f) mask |= 1UL << (int)PadButton.RightTrigger;

        var pressed = mask & ~_prevMask;
        _prevMask = mask;

        if (SnapshotRaised is { } snap && _primary >= 0 && _infos[_primary] is { } info)
        {
            var list = Enum.GetValues<PadButton>().Where(b => (mask & (1UL << (int)b)) != 0).ToList();
            var s = new PadSnapshot(info, list, lx, ly, rx, ry, lt, rt);
            _outbox.Add(() => snap(s));
        }

        if (_capture is { } capture)
        {
            _heldDir = null;
            if (pressed != 0)
            {
                _capture = null;
                var first = Enum.GetValues<PadButton>().First(b => (pressed & (1UL << (int)b)) != 0);
                _outbox.Add(() => capture(first));
            }
            return;
        }

        foreach (var b in Enum.GetValues<PadButton>())
        {
            if ((pressed & (1UL << (int)b)) == 0) continue;
            if (profile.ActionFor(b) is { } action) Raise(action);
        }

        NavAction? dir = DpadDir(mask) ?? StickDir(lx, ly);
        if (dir != _heldDir)
        {
            _heldDir = dir;
            if (dir is { } d) { Raise(d); _nextRepeatMs = now + 400; }
        }
        else if (dir is { } held && now >= _nextRepeatMs)
        {
            Raise(held);
            _nextRepeatMs = now + 110;
        }
    }

    private void Raise(NavAction a) => _outbox.Add(() => ActionRaised?.Invoke(a));

    private static NavAction? DpadDir(ulong m)
    {
        bool Has(PadButton b) => (m & (1UL << (int)b)) != 0;
        if (Has(PadButton.DpadUp)) return NavAction.Up;
        if (Has(PadButton.DpadDown)) return NavAction.Down;
        if (Has(PadButton.DpadLeft)) return NavAction.Left;
        if (Has(PadButton.DpadRight)) return NavAction.Right;
        return null;
    }

    private static NavAction? StickDir(float x, float y)
    {
        if (x * x + y * y < 0.25f) return null;
        return Math.Abs(x) > Math.Abs(y) ? (x > 0 ? NavAction.Right : NavAction.Left) : (y > 0 ? NavAction.Up : NavAction.Down);
    }

    /// <summary>Zone morte radiale : sous le seuil, 0 ; au-dessus, rééchelonné de 0 à 1.</summary>
    private static (float X, float Y) Axis(short rawX, short rawY, float deadzone)
    {
        float x = Math.Clamp(rawX / 32767f, -1f, 1f), y = Math.Clamp(rawY / 32767f, -1f, 1f);
        var mag = MathF.Sqrt(x * x + y * y);
        if (mag <= deadzone || mag == 0) return (0, 0);
        var scale = (Math.Min(mag, 1f) - deadzone) / (1f - deadzone) / mag;
        return (x * scale, y * scale);
    }

    private static ControllerInfo BuildInfo(int slot)
    {
        var name = "Manette XInput (compatible Xbox)";
        var connection = "Connexion inconnue";
        if (XInputNative.TryGetCaps(slot, out var caps))
        {
            name = caps.SubType switch
            {
                1 => name, 2 => "Volant XInput", 3 => "Arcade stick XInput", 4 => "Joystick de vol XInput",
                5 => "Tapis de danse XInput", 6 => "Guitare XInput", 8 => "Batterie XInput", 19 => "Manette arcade XInput",
                _ => "Périphérique XInput"
            };
            connection = (caps.Flags & 0x0002) != 0 ? "Sans fil" : "Filaire";
        }
        return new ControllerInfo(slot, name, ControllerKind.Xbox, connection, "Sticks analogiques, gâchettes analogiques, croix directionnelle");
    }
}
