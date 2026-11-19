using System.Globalization;

namespace Nexus.Core.Config;

/// <summary>Paramètres d'un profil, stockés en paires clé/valeur : ajouter un réglage ne demande aucune migration.</summary>
public sealed class AppSettings
{
    public static readonly string[] LibraryFilters = { "all", "fav", "recent", "hidden" };
    public static readonly string[] LibrarySorts = { "az", "recent" };
    public static readonly string[] LibraryViews = { "grid", "row" };

    public string Theme { get; set; } = "dark";          // "dark" | "light"
    public string Accent { get; set; } = "#3B82F6";
    public int AnimationLevel { get; set; } = 2;          // 0 = aucune, 1 = réduites, 2 = complètes
    public bool TvMode { get; set; }
    public bool StartFullscreen { get; set; }
    public bool AmbientEnabled { get; set; } = true;
    public bool FirstRunDone { get; set; }
    public bool LowMemory { get; set; }                   // mode économie de mémoire
    public int ScanMinutes { get; set; } = 30;            // 0 = jamais, sinon 15 / 30 / 60
    public int UiScalePercent { get; set; }               // 0 = automatique, sinon 100 / 125 / 150
    public string GfnTracking { get; set; } = "process";  // "process" = suivre le processus lancé, "folder" = suivre le dossier

    public bool SoundsEnabled { get; set; } = true;
    public int SoundVolume { get; set; } = 60;            // 0..100
    public HashSet<string> SoundsOff { get; set; } = new();   // noms de SoundId désactivés individuellement

    public string PadProfileJson { get; set; } = "";     // profil manette (JSON), voir Nexus.Input

    public string LibraryFilter { get; set; } = "all";
    public string LibrarySort { get; set; } = "az";
    public string LibraryView { get; set; } = "grid";

    public IEnumerable<KeyValuePair<string, string>> ToPairs()
    {
        var inv = CultureInfo.InvariantCulture;
        yield return new("theme", Theme);
        yield return new("accent", Accent);
        yield return new("animation_level", AnimationLevel.ToString(inv));
        yield return new("tv_mode", TvMode ? "1" : "0");
        yield return new("start_fullscreen", StartFullscreen ? "1" : "0");
        yield return new("ambient", AmbientEnabled ? "1" : "0");
        yield return new("first_run_done", FirstRunDone ? "1" : "0");
        yield return new("low_memory", LowMemory ? "1" : "0");
        yield return new("scan_minutes", ScanMinutes.ToString(inv));
        yield return new("ui_scale", UiScalePercent.ToString(inv));
        yield return new("gfn_tracking", GfnTracking);
        yield return new("sounds_enabled", SoundsEnabled ? "1" : "0");
        yield return new("sound_volume", SoundVolume.ToString(inv));
        yield return new("sounds_off", string.Join(',', SoundsOff.OrderBy(x => x)));
        yield return new("pad_profile", PadProfileJson);
        yield return new("lib_filter", LibraryFilter);
        yield return new("lib_sort", LibrarySort);
        yield return new("lib_view", LibraryView);
    }

    public void Apply(string key, string value)
    {
        var inv = CultureInfo.InvariantCulture;
        switch (key)
        {
            case "theme": Theme = value == "light" ? "light" : "dark"; break;
            case "accent": Accent = value; break;
            case "animation_level":
                if (int.TryParse(value, NumberStyles.Integer, inv, out var l)) AnimationLevel = Math.Clamp(l, 0, 2);
                break;
            case "tv_mode": TvMode = value == "1"; break;
            case "start_fullscreen": StartFullscreen = value == "1"; break;
            case "ambient": AmbientEnabled = value == "1"; break;
            case "first_run_done": FirstRunDone = value == "1"; break;
            case "low_memory": LowMemory = value == "1"; break;
            case "scan_minutes":
                if (int.TryParse(value, NumberStyles.Integer, inv, out var sm) && new[] { 0, 15, 30, 60 }.Contains(sm)) ScanMinutes = sm;
                break;
            case "ui_scale":
                if (int.TryParse(value, NumberStyles.Integer, inv, out var us) && new[] { 0, 100, 125, 150 }.Contains(us)) UiScalePercent = us;
                break;
            case "gfn_tracking": GfnTracking = value == "folder" ? "folder" : "process"; break;
            case "sounds_enabled": SoundsEnabled = value == "1"; break;
            case "sound_volume":
                if (int.TryParse(value, NumberStyles.Integer, inv, out var v)) SoundVolume = Math.Clamp(v, 0, 100);
                break;
            case "sounds_off":
                SoundsOff = new HashSet<string>(value.Split(',', StringSplitOptions.RemoveEmptyEntries));
                break;
            case "pad_profile": PadProfileJson = value; break;
            case "lib_filter": if (LibraryFilters.Contains(value)) LibraryFilter = value; break;
            case "lib_sort": if (LibrarySorts.Contains(value)) LibrarySort = value; break;
            case "lib_view": if (LibraryViews.Contains(value)) LibraryView = value; break;
        }
    }

    public void CopyFrom(AppSettings o)
    {
        Theme = o.Theme; Accent = o.Accent; AnimationLevel = o.AnimationLevel;
        TvMode = o.TvMode; StartFullscreen = o.StartFullscreen; AmbientEnabled = o.AmbientEnabled;
        FirstRunDone = o.FirstRunDone; LowMemory = o.LowMemory; ScanMinutes = o.ScanMinutes; UiScalePercent = o.UiScalePercent; GfnTracking = o.GfnTracking;
        SoundsEnabled = o.SoundsEnabled; SoundVolume = o.SoundVolume; SoundsOff = new HashSet<string>(o.SoundsOff);
        PadProfileJson = o.PadProfileJson; LibraryFilter = o.LibraryFilter; LibrarySort = o.LibrarySort; LibraryView = o.LibraryView;
    }
}
