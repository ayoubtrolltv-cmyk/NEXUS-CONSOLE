using Nexus.Core.Config;
using Nexus.Core.Logging;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Shell.Design;

namespace Nexus.Shell.Services;

/// <summary>Point unique d'accès aux paramètres, aux profils et à la base : modifie, enregistre, applique, notifie.</summary>
public sealed class SettingsService
{
    private readonly SettingsStore? _store;

    public AppSettings Current { get; }
    public GameRepository? Games { get; }
    public ProfileRepository? Profiles { get; }
    public int ProfileId { get; private set; } = 1;
    public string DatabasePath => AppPaths.DatabaseFile;

    public event Action? Changed;
    public event Action? ProfileChanged;

    private SettingsService(SettingsStore? store, GameRepository? games, ProfileRepository? profiles, AppSettings current, int profileId)
    {
        _store = store; Games = games; Profiles = profiles; Current = current; ProfileId = profileId;
    }

    public static SettingsService Create()
    {
        SettingsStore? store = null;
        GameRepository? games = null;
        ProfileRepository? profiles = null;
        AppSettings settings;
        var profileId = 1;
        try
        {
            var db = new Database(AppPaths.DatabaseFile);
            db.Initialize();
            store = new SettingsStore(db);
            games = new GameRepository(db);
            profiles = new ProfileRepository(db);
            profileId = profiles.GetCurrentId();
            settings = store.Load(profileId);
        }
        catch (Exception ex)
        {
            Log.Error("Base de données inaccessible : paramètres par défaut, rien ne sera enregistré.", ex);
            store = null; games = null; profiles = null;
            settings = new AppSettings();
        }
        var svc = new SettingsService(store, games, profiles, settings, profileId);
        svc.ApplyRuntime();
        return svc;
    }

    public Profile? CurrentProfile => Profiles?.Get(ProfileId);

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        try { _store?.Save(Current, ProfileId); }
        catch (Exception ex) { Log.Error("Enregistrement des paramètres impossible.", ex); }
        ApplyRuntime();
        Changed?.Invoke();
    }

    public void Reset() => Update(c => { var keep = c.FirstRunDone; c.CopyFrom(new AppSettings()); c.FirstRunDone = keep; });

    /// <summary>Passe à un autre profil : ses paramètres remplacent ceux en cours.</summary>
    public bool SwitchProfile(int id)
    {
        if (Profiles is null || _store is null || Profiles.Get(id) is null) return false;
        try
        {
            Current.CopyFrom(_store.Load(id));
            ProfileId = id;
            Profiles.SetCurrentId(id);
        }
        catch (Exception ex) { Log.Error("Changement de profil impossible.", ex); return false; }
        ApplyRuntime();
        Changed?.Invoke();
        ProfileChanged?.Invoke();
        return true;
    }

    /// <summary>Crée un profil qui démarre avec une copie des réglages actuels.</summary>
    public Profile? CreateProfile()
    {
        if (Profiles is null || _store is null) return null;
        try
        {
            var p = Profiles.Add($"Joueur {Profiles.List().Count + 1}");
            _store.Save(Current, p.Id);
            return p;
        }
        catch (Exception ex) { Log.Error("Création du profil impossible.", ex); return null; }
    }

    /// <summary>Supprime le profil actif et bascule sur un autre. Refuse de supprimer le dernier.</summary>
    public bool DeleteCurrentProfile()
    {
        if (Profiles is null) return false;
        var toDelete = ProfileId;
        var other = Profiles.List().FirstOrDefault(p => p.Id != toDelete);
        if (other is null || !SwitchProfile(other.Id)) return false;
        return Profiles.Delete(toDelete);
    }

    private void ApplyRuntime()
    {
        Motion.Level = Current.AnimationLevel;
        ThemeService.Apply(Current);
        SoundService.Configure(Current);
        Nexus.Shell.Services.ImageCache.MaxEntries = Current.LowMemory ? 32 : 96;
    }
}
