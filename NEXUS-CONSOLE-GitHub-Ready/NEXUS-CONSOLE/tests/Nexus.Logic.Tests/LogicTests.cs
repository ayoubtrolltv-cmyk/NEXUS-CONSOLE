using Microsoft.Data.Sqlite;
using Nexus.Core.Input;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Detection;
using Nexus.Detection.Sources;
using Nexus.Input;
using Nexus.Launching;
using Xunit;

namespace Nexus.Logic.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nexus-logic-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public void Dispose() { SqliteConnection.ClearAllPools(); try { Directory.Delete(Path, true); } catch { } }
}

public class VdfTests
{
    [Fact]
    public void Parses_nested_blocks_and_values()
    {
        var doc = VdfParser.Parse("\"AppState\" { \"appid\" \"123\" // commentaire\n \"name\" \"Mon \\\"Jeu\\\"\" \"sub\" { \"k\" \"v\" } }");
        Assert.Equal("123", VdfParser.GetString(doc, "AppState", "appid"));
        Assert.Equal("Mon \"Jeu\"", VdfParser.GetString(doc, "AppState", "name"));
        Assert.Equal("v", VdfParser.GetString(doc, "AppState", "sub", "k"));
    }

    [Fact]
    public void Lookup_is_case_insensitive_and_missing_is_null()
    {
        var doc = VdfParser.Parse("\"A\" { \"B\" \"1\" }");
        Assert.Equal("1", VdfParser.GetString(doc, "a", "b"));
        Assert.Null(VdfParser.GetString(doc, "a", "x"));
    }
}

public class SourceTests
{
    [Fact]
    public void Steam_finds_installed_games_and_skips_redistributables()
    {
        using var t = new TempDir();
        var apps = Directory.CreateDirectory(System.IO.Path.Combine(t.Path, "steamapps")).FullName;
        Directory.CreateDirectory(System.IO.Path.Combine(apps, "common", "Alpha"));
        Directory.CreateDirectory(System.IO.Path.Combine(apps, "common", "Redist"));
        File.WriteAllText(System.IO.Path.Combine(apps, "appmanifest_10.acf"),
            "\"AppState\" { \"appid\" \"10\" \"name\" \"Alpha\" \"installdir\" \"Alpha\" \"StateFlags\" \"4\" }");
        File.WriteAllText(System.IO.Path.Combine(apps, "appmanifest_11.acf"),
            "\"AppState\" { \"appid\" \"11\" \"name\" \"Steamworks Common Redistributables\" \"installdir\" \"Redist\" \"StateFlags\" \"4\" }");
        File.WriteAllText(System.IO.Path.Combine(apps, "appmanifest_12.acf"),
            "\"AppState\" { \"appid\" \"12\" \"name\" \"Half\" \"installdir\" \"Missing\" \"StateFlags\" \"2\" }");

        var found = new SteamSource(t.Path).Scan();
        var g = Assert.Single(found);
        Assert.Equal("Alpha", g.Name);
        Assert.Equal("steam://rungameid/10", g.LaunchUri);
    }

    [Fact]
    public void Epic_reads_games_and_ignores_addons()
    {
        using var t = new TempDir();
        var install = Directory.CreateDirectory(System.IO.Path.Combine(t.Path, "Game")).FullName;
        File.WriteAllText(System.IO.Path.Combine(t.Path, "a.item"),
            $"{{\"AppName\":\"Fort\",\"DisplayName\":\"Fort Game\",\"InstallLocation\":{System.Text.Json.JsonSerializer.Serialize(install)},\"LaunchExecutable\":\"game.exe\",\"AppCategories\":[\"public\",\"games\"]}}");
        File.WriteAllText(System.IO.Path.Combine(t.Path, "b.item"),
            $"{{\"AppName\":\"Dlc\",\"DisplayName\":\"Pack\",\"InstallLocation\":{System.Text.Json.JsonSerializer.Serialize(install)},\"LaunchExecutable\":\"\",\"AppCategories\":[\"addons\"]}}");

        var g = Assert.Single(new EpicSource(t.Path).Scan());
        Assert.Equal("Fort Game", g.Name);
        Assert.StartsWith("com.epicgames.launcher://apps/Fort", g.LaunchUri);
    }
}

public class ScannerTests
{
    private sealed class FakeSource : IGameSource
    {
        public string Platform { get; init; } = "steam";
        public List<DetectedGame> Games { get; } = new();
        public bool Fail { get; set; }
        public IReadOnlyList<DetectedGame> Scan() => Fail ? throw new IOException("boom") : Games.ToList();
    }

    private static DetectedGame D(string id, string name, string? exe = null) =>
        new("steam", id, name, @"c:\games\" + name, exe, "steam://rungameid/" + id, null);

    private static (GameRepository Repo, TempDir Dir) NewRepo()
    {
        var t = new TempDir();
        var db = new Database(System.IO.Path.Combine(t.Path, "s.db"));
        db.Initialize();
        return (new GameRepository(db), t);
    }

    [Fact]
    public void Adds_then_updates_then_marks_missing_without_losing_favorites()
    {
        var (repo, dir) = NewRepo();
        using var _ = dir;
        var src = new FakeSource();
        src.Games.Add(D("1", "Alpha"));
        var scanner = new GameScanner(repo, new[] { src });

        Assert.Equal(1, scanner.Scan().Added);
        var g = repo.List().Single();
        repo.SetFavorite(g.Id, true);

        var second = scanner.Scan();
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Updated);
        Assert.True(repo.List().Single().Favorite);

        src.Games.Clear();
        Assert.Equal(1, scanner.Scan().Missing);
        Assert.False(repo.List(true).Single().Installed);

        src.Games.Add(D("1", "Alpha"));
        scanner.Scan();
        Assert.True(repo.List(true).Single().Installed);
    }

    [Fact]
    public void Failing_source_does_not_mark_its_games_missing()
    {
        var (repo, dir) = NewRepo();
        using var _ = dir;
        var src = new FakeSource();
        src.Games.Add(D("1", "Alpha"));
        var scanner = new GameScanner(repo, new[] { src });
        scanner.Scan();

        src.Fail = true;
        var r = scanner.Scan();
        Assert.Equal(0, r.Missing);
        Assert.Contains("steam", r.Failed);
        Assert.True(repo.List().Single().Installed);
    }

    [Fact]
    public void Game_already_added_manually_is_not_duplicated()
    {
        var (repo, dir) = NewRepo();
        using var _ = dir;
        repo.Add(new Game { Name = "Alpha", Platform = "manual", ExternalId = @"c:\games\alpha\a.exe", ExePath = @"C:\Games\Alpha\a.exe" });
        var src = new FakeSource();
        src.Games.Add(D("1", "Alpha", @"c:\games\alpha\a.exe"));

        var r = new GameScanner(repo, new[] { src }).Scan();
        Assert.Equal(0, r.Added);
        Assert.Single(repo.List());
    }
}

public class InputTests
{
    [Fact]
    public void Default_profile_maps_expected_actions()
    {
        var p = PadProfile.Default();
        Assert.Equal(PadButton.South, p.ButtonFor(NavAction.Accept));
        Assert.Equal(NavAction.Back, p.ActionFor(PadButton.East));
        Assert.Null(p.ActionFor(PadButton.DpadUp));
    }

    [Fact]
    public void Profile_survives_json_and_invalid_json_falls_back()
    {
        var p = PadProfile.Default();
        p.Set(NavAction.Accept, PadButton.North);
        p.DeadzonePercent = 30;
        p.InvertY = true;
        var r = PadProfile.FromJson(p.ToJson());
        Assert.Equal(PadButton.North, r.ButtonFor(NavAction.Accept));
        Assert.Equal(30, r.DeadzonePercent);
        Assert.True(r.InvertY);
        Assert.Equal(PadButton.South, PadProfile.FromJson("{pas du json").ButtonFor(NavAction.Accept));
        Assert.Equal(PadButton.South, PadProfile.FromJson("").ButtonFor(NavAction.Accept));
    }

    [Fact]
    public void Glyphs_follow_the_controller_family()
    {
        Assert.Equal("A", Glyphs.Name(ControllerKind.Xbox, PadButton.South));
        Assert.Equal("✕", Glyphs.Name(ControllerKind.PlayStation, PadButton.South));
    }
}

public class LauncherTests
{
    [Fact]
    public void IsUnder_is_case_insensitive_and_respects_folder_boundaries()
    {
        var dir = GameLauncher.NormalizeDir(@"C:\Games\Alpha")!;
        Assert.True(GameLauncher.IsUnder(@"c:\games\alpha\bin\game.exe", dir));
        Assert.False(GameLauncher.IsUnder(@"C:\Games\AlphaTwo\game.exe", dir));
    }

    [Fact]
    public async Task Missing_executable_gives_a_friendly_error()
    {
        var game = new Game { Name = "X", Platform = "manual", ExePath = @"C:\introuvable\x.exe" };
        var ex = await Assert.ThrowsAsync<LaunchException>(() =>
            new GameLauncher().RunAsync(game, () => { }, CancellationToken.None));
        Assert.Contains("introuvable", ex.Message);
    }
}
