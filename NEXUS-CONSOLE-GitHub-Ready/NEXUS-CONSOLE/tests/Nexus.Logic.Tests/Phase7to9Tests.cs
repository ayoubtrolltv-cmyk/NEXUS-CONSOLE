using Nexus.Cloud;
using Nexus.Core.Config;
using Nexus.Core.Models;
using Nexus.Data;
using Nexus.Store;
using Xunit;

namespace Nexus.Logic.Tests;

public class CloudTests
{
    [Fact]
    public void Geforce_now_is_detected_only_when_a_known_path_exists()
    {
        var none = CloudDetector.Detect(_ => false, new[] { @"C:\x\GeForceNOW.exe" });
        Assert.False(none.First(s => s.Id == "gfn").Installed);

        var found = CloudDetector.Detect(p => p == @"C:\x\GeForceNOW.exe", new[] { @"C:\y\a.exe", @"C:\x\GeForceNOW.exe" });
        var gfn = found.First(s => s.Id == "gfn");
        Assert.True(gfn.Installed);
        Assert.Equal(@"C:\x\GeForceNOW.exe", gfn.ExePath);
        Assert.Contains(found, s => s.Id == "xbox" && s.Kind == CloudKind.Web);
    }

    [Fact]
    public void Web_service_with_edge_tracks_the_launched_process_and_without_edge_does_not()
    {
        var xbox = CloudDetector.Detect(_ => false, Array.Empty<string>()).First(s => s.Id == "xbox");

        var withEdge = CloudLauncher.ToGame(xbox, @"C:\Edge\msedge.exe", trackFolder: false);
        Assert.True(withEdge.TrackLaunchedProcess);
        Assert.Contains("--app=https://www.xbox.com/play", withEdge.Arguments);
        Assert.Contains("--user-data-dir", withEdge.Arguments);

        var noEdge = CloudLauncher.ToGame(xbox, null, trackFolder: false);
        Assert.False(noEdge.TrackLaunchedProcess);
        Assert.Equal("https://www.xbox.com/play", noEdge.LaunchUri);
    }

    [Fact]
    public void Geforce_now_tracking_mode_follows_the_setting()
    {
        var svc = new CloudService("gfn", "GFN", CloudKind.NativeApp, true, @"C:\g\CEF\GeForceNOW.exe", null, "");
        Assert.True(CloudLauncher.ToGame(svc, null, trackFolder: false).TrackLaunchedProcess);
        var folder = CloudLauncher.ToGame(svc, null, trackFolder: true);
        Assert.False(folder.TrackLaunchedProcess);
        Assert.Equal(@"C:\g\CEF", folder.InstallPath);
    }
}

public class StoreParsingTests
{
    private const string Featured = """
    {
      "specials": { "id": "cat_specials", "items": [
        { "id": 100, "name": "Alpha", "discounted": true, "discount_percent": 50, "original_price": 3000, "final_price": 1500,
          "currency": "EUR", "large_capsule_image": "https://x/l.jpg", "header_image": "https://x/h.jpg" },
        { "id": 101, "name": "Bêta", "discounted": false, "discount_percent": 0, "original_price": 0, "final_price": 0, "currency": "EUR" },
        { "name": "Sans identifiant" }
      ] },
      "coming_soon": { "items": [ { "id": 200, "name": "Futur" } ] }
    }
    """;

    [Fact]
    public void Featured_items_are_parsed_with_prices_and_discounts()
    {
        var items = SteamStoreSource.ParseFeatured(Featured, "specials");
        Assert.Equal(2, items.Count);                         // l'entrée sans id est ignorée
        Assert.Equal("Alpha", items[0].Name);
        Assert.Equal(50, items[0].DiscountPercent);
        Assert.Equal("15,00 €", items[0].PriceText);
        Assert.Equal("30,00 €", items[0].OriginalPriceText);
        Assert.Equal("Gratuit", items[1].PriceText);
        Assert.Equal("https://x/h.jpg", items[0].ImageUrl);
    }

    [Fact]
    public void Missing_category_or_price_is_tolerated()
    {
        Assert.Empty(SteamStoreSource.ParseFeatured(Featured, "inconnue"));
        Assert.Null(SteamStoreSource.ParseFeatured(Featured, "coming_soon")[0].PriceText);
    }

    [Fact]
    public void Details_are_parsed_and_failure_returns_null()
    {
        const string json = """
        { "100": { "success": true, "data": { "name": "Alpha", "is_free": false, "short_description": "Un jeu.",
          "header_image": "https://x/h.jpg", "developers": ["Dev A"], "publishers": ["Pub B"],
          "genres": [ { "id": "1", "description": "Action" }, { "id": "2", "description": "Aventure" } ],
          "screenshots": [ { "path_thumbnail": "https://x/1.jpg", "path_full": "https://x/1f.jpg" } ],
          "price_overview": { "final_formatted": "15,00€", "initial_formatted": "30,00€", "discount_percent": 50 } } },
          "999": { "success": false } }
        """;
        var d = SteamStoreSource.ParseDetails(json, "100")!;
        Assert.Equal("Alpha", d.Item.Name);
        Assert.Equal(new[] { "Action", "Aventure" }, d.Genres);
        Assert.Equal("Dev A", d.Developers);
        Assert.Single(d.Screenshots);
        Assert.Equal("15,00€", d.Item.PriceText);
        Assert.Equal("https://store.steampowered.com/app/100", d.PageUrl);

        Assert.Null(SteamStoreSource.ParseDetails(json, "999"));
        Assert.Null(SteamStoreSource.ParseDetails(json, "absent"));
    }
}

public class ProfileTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly Database _db;
    private readonly ProfileRepository _profiles;
    private readonly SettingsStore _store;

    public ProfileTests()
    {
        _db = new Database(System.IO.Path.Combine(_dir.Path, "p.db"));
        _db.Initialize();
        _profiles = new ProfileRepository(_db);
        _store = new SettingsStore(_db);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void A_default_profile_exists_and_is_current()
    {
        Assert.Single(_profiles.List());
        Assert.Equal(1, _profiles.GetCurrentId());
    }

    [Fact]
    public void Each_profile_keeps_its_own_settings()
    {
        var second = _profiles.Add("Joueur 2");
        _store.Save(new AppSettings { Theme = "light", SoundVolume = 10 }, second.Id);
        _store.Save(new AppSettings { Theme = "dark", SoundVolume = 90 }, 1);

        Assert.Equal("light", _store.Load(second.Id).Theme);
        Assert.Equal(10, _store.Load(second.Id).SoundVolume);
        Assert.Equal(90, _store.Load(1).SoundVolume);
    }

    [Fact]
    public void Current_profile_is_remembered_and_falls_back_when_deleted()
    {
        var second = _profiles.Add("Joueur 2");
        _profiles.SetCurrentId(second.Id);
        Assert.Equal(second.Id, _profiles.GetCurrentId());

        Assert.True(_profiles.Delete(second.Id));
        Assert.Equal(1, _profiles.GetCurrentId());
    }

    [Fact]
    public void Last_profile_cannot_be_deleted_and_deletion_removes_its_settings()
    {
        Assert.False(_profiles.Delete(1));
        var second = _profiles.Add("Joueur 2");
        _store.Save(new AppSettings { SoundVolume = 5 }, second.Id);
        Assert.True(_profiles.Delete(second.Id));
        Assert.Equal(60, _store.Load(second.Id).SoundVolume);   // retombe sur la valeur par défaut
    }

    [Fact]
    public void New_settings_roundtrip_and_invalid_values_are_ignored()
    {
        _store.Save(new AppSettings { LowMemory = true, ScanMinutes = 15, UiScalePercent = 125, GfnTracking = "folder", FirstRunDone = true });
        var r = _store.Load();
        Assert.True(r.LowMemory);
        Assert.Equal(15, r.ScanMinutes);
        Assert.Equal(125, r.UiScalePercent);
        Assert.Equal("folder", r.GfnTracking);
        Assert.True(r.FirstRunDone);

        var s = new AppSettings();
        s.Apply("scan_minutes", "7");
        s.Apply("ui_scale", "333");
        Assert.Equal(30, s.ScanMinutes);
        Assert.Equal(0, s.UiScalePercent);
    }
}
