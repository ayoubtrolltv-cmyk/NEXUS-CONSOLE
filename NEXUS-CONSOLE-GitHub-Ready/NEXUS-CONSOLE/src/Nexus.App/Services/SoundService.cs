using System.Media;
using Nexus.Core.Audio;
using Nexus.Core.Config;
using Nexus.Core.Logging;

namespace Nexus.Shell.Services;

/// <summary>
/// Bruitages d'interface synthétisés en mémoire (aucun fichier, aucune dépendance, aucune musique).
/// Chaque son peut être désactivé séparément ; le volume est appliqué à la synthèse.
/// </summary>
public static class SoundService
{
    private const int Rate = 44100;
    private static readonly Dictionary<SoundId, SoundPlayer> Cache = new();
    private static AppSettings _settings = new();
    private static int _cachedVolume = -1;

    // (fréquence Hz, durée ms) ; chaque note a une attaque courte et une décroissance exponentielle.
    private static readonly Dictionary<SoundId, (double Hz, int Ms)[]> Recipes = new()
    {
        [SoundId.Move] = new[] { (1800.0, 26) },
        [SoundId.Select] = new[] { (880.0, 45), (1320.0, 60) },
        [SoundId.Confirm] = new[] { (660.0, 55), (990.0, 85) },
        [SoundId.Back] = new[] { (990.0, 55), (660.0, 85) },
        [SoundId.MenuOpen] = new[] { (440.0, 40), (660.0, 40), (880.0, 70) },
        [SoundId.MenuClose] = new[] { (880.0, 40), (660.0, 40), (440.0, 70) },
        [SoundId.Launch] = new[] { (523.0, 80), (659.0, 80), (784.0, 80), (1046.0, 160) },
        [SoundId.Return] = new[] { (1046.0, 80), (784.0, 80), (659.0, 80), (523.0, 160) },
    };

    public static void Configure(AppSettings settings)
    {
        _settings = settings;
        if (_cachedVolume == settings.SoundVolume) return;
        foreach (var p in Cache.Values) p.Dispose();
        Cache.Clear();
        _cachedVolume = settings.SoundVolume;
    }

    public static bool IsEnabled(SoundId id) => _settings.SoundsEnabled && !_settings.SoundsOff.Contains(id.ToString());

    public static void Play(SoundId id)
    {
        if (!IsEnabled(id) || _settings.SoundVolume <= 0) return;
        try
        {
            if (!Cache.TryGetValue(id, out var player))
            {
                player = new SoundPlayer(new MemoryStream(Synthesize(Recipes[id], _settings.SoundVolume / 100.0)));
                player.Load();
                Cache[id] = player;
            }
            player.Play();   // asynchrone ; un nouveau son remplace le précédent
        }
        catch (Exception ex) { Log.Warn($"Lecture du son {id} impossible.", ex); }
    }

    private static byte[] Synthesize((double Hz, int Ms)[] notes, double volume)
    {
        var samples = new List<short>();
        foreach (var (hz, ms) in notes)
        {
            var n = Rate * ms / 1000;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                var attack = Math.Min(1.0, t / 0.004);
                var decay = Math.Exp(-t * 18000.0 / ms / 4.0);
                var wave = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(4 * Math.PI * hz * t);
                samples.Add((short)(wave * attack * decay * 0.28 * volume * short.MaxValue));
            }
        }

        using var ms2 = new MemoryStream();
        using var w = new BinaryWriter(ms2);
        var dataLen = samples.Count * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms2.ToArray();
    }
}
