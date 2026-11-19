using Microsoft.Data.Sqlite;
using Nexus.Core.Config;
using Nexus.Data;
using Xunit;

namespace Nexus.Data.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nexus-tests-" + Guid.NewGuid().ToString("N"));

    public DatabaseTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Empty_database_returns_default_settings()
    {
        var db = new Database(Path.Combine(_dir, "a.db"));
        db.Initialize();
        var s = new SettingsStore(db).Load();
        Assert.Equal("dark", s.Theme);
        Assert.Equal(2, s.AnimationLevel);
    }

    [Fact]
    public void Settings_survive_a_roundtrip()
    {
        var db = new Database(Path.Combine(_dir, "b.db"));
        db.Initialize();
        var store = new SettingsStore(db);
        store.Save(new AppSettings { Theme = "light", Accent = "#10B981", AnimationLevel = 1, TvMode = true });
        var r = store.Load();
        Assert.Equal("light", r.Theme);
        Assert.Equal("#10B981", r.Accent);
        Assert.Equal(1, r.AnimationLevel);
        Assert.True(r.TvMode);
    }

    [Fact]
    public void Initialize_twice_is_harmless()
    {
        var db = new Database(Path.Combine(_dir, "c.db"));
        db.Initialize();
        db.Initialize();
    }

    [Fact]
    public void Corrupt_file_is_set_aside_and_recreated()
    {
        var path = Path.Combine(_dir, "d.db");
        File.WriteAllText(path, new string('x', 4096));
        var db = new Database(path);
        db.Initialize();
        Assert.Single(Directory.GetFiles(_dir, "d.db.corrupt-*"));
        Assert.Equal("dark", new SettingsStore(db).Load().Theme);
    }
}

public sealed class GameRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nexus-tests-" + Guid.NewGuid().ToString("N"));
    private readonly GameRepository _repo;
    private readonly Database _db;

    public GameRepositoryTests()
    {
        Directory.CreateDirectory(_dir);
        _db = new Database(Path.Combine(_dir, "g.db"));
        _db.Initialize();
        _repo = new GameRepository(_db);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static Nexus.Core.Models.Game Make(string name, string ext) =>
        new() { Name = name, Platform = "manual", ExternalId = ext, ExePath = ext };

    [Fact]
    public void Add_then_list_returns_the_game()
    {
        var g = _repo.Add(Make("Alpha", @"c:\a.exe"));
        Assert.NotNull(g);
        Assert.True(g!.Id > 0);
        Assert.Single(_repo.List());
    }

    [Fact]
    public void Duplicate_is_rejected()
    {
        Assert.NotNull(_repo.Add(Make("Alpha", @"c:\a.exe")));
        Assert.Null(_repo.Add(Make("Alpha bis", @"c:\a.exe")));
        Assert.Single(_repo.List());
    }

    [Fact]
    public void Hidden_games_are_excluded_unless_requested()
    {
        var g = _repo.Add(Make("Alpha", @"c:\a.exe"))!;
        _repo.SetHidden(g.Id, true);
        Assert.Empty(_repo.List());
        Assert.Single(_repo.List(includeHidden: true));
    }

    [Fact]
    public void Favorite_and_remove_work()
    {
        var g = _repo.Add(Make("Alpha", @"c:\a.exe"))!;
        _repo.SetFavorite(g.Id, true);
        Assert.True(_repo.Get(g.Id)!.Favorite);
        _repo.Remove(g.Id);
        Assert.Null(_repo.Get(g.Id));
    }

    [Fact]
    public void Dates_survive_a_roundtrip()
    {
        var when = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
        var g = Make("Alpha", @"c:\a.exe");
        g.LastPlayedUtc = when;
        _repo.Add(g);
        Assert.Equal(when, _repo.List()[0].LastPlayedUtc);
    }

    [Fact]
    public void New_settings_survive_a_roundtrip()
    {
        var store = new SettingsStore(_db);
        store.Save(new AppSettings { SoundVolume = 40, SoundsEnabled = false, SoundsOff = { "Move", "Back" }, LibraryView = "row", AmbientEnabled = false });
        var r = store.Load();
        Assert.Equal(40, r.SoundVolume);
        Assert.False(r.SoundsEnabled);
        Assert.Equal(new[] { "Back", "Move" }, r.SoundsOff.OrderBy(x => x).ToArray());
        Assert.Equal("row", r.LibraryView);
        Assert.False(r.AmbientEnabled);
    }
}
