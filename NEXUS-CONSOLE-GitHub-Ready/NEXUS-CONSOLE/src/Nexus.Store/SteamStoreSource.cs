using System.Globalization;
using System.Text.Json;

namespace Nexus.Store;

/// <summary>
/// Catalogue Steam via les points d'accès publics de la boutique (featuredcategories, appdetails).
/// Ces points d'accès ne sont pas une API officielle documentée : leur format peut changer, d'où une lecture tolérante.
/// </summary>
public sealed class SteamStoreSource : IStoreSource
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private readonly StoreCache _cache;

    public SteamStoreSource(StoreCache? cache = null) => _cache = cache ?? new StoreCache();

    public string Name => "Steam";

    public IReadOnlyList<(string Key, string Label)> Categories { get; } = new[]
    {
        ("specials", "Promotions"), ("top_sellers", "Meilleures ventes"), ("new_releases", "Nouveautés"), ("coming_soon", "Prochainement")
    };

    public StoreCache Cache => _cache;

    public async Task<IReadOnlyList<StoreItem>> GetCategoryAsync(string categoryKey, CancellationToken ct) =>
        ParseFeatured(await _cache.GetJsonAsync("https://store.steampowered.com/api/featuredcategories?cc=fr&l=french", ct), categoryKey);

    public async Task<StoreDetails?> GetDetailsAsync(string id, CancellationToken ct) =>
        ParseDetails(await _cache.GetJsonAsync($"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(id)}&cc=fr&l=french", ct), id);

    public string PageUrl(string id) => $"https://store.steampowered.com/app/{Uri.EscapeDataString(id)}";

    // ---- Lecture (publique pour les tests) ----
    public static IReadOnlyList<StoreItem> ParseFeatured(string json, string key)
    {
        var list = new List<StoreItem>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(key, out var cat) || !cat.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var it in items.EnumerateArray())
        {
            var id = Str(it, "id");
            var name = Str(it, "name");
            if (id is null || name is null) continue;
            var currency = Str(it, "currency");
            var final = Int(it, "final_price");
            var original = Int(it, "original_price");
            var discount = Int(it, "discount_percent") ?? 0;
            list.Add(new StoreItem("steam", id, name,
                Str(it, "header_image") ?? Str(it, "large_capsule_image"),
                final is null ? null : FormatPrice(final.Value, currency),
                discount,
                discount > 0 && original is not null ? FormatPrice(original.Value, currency) : null));
        }
        return list;
    }

    public static StoreDetails? ParseDetails(string json, string id)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(id, out var entry)) return null;
        if (!entry.TryGetProperty("success", out var ok) || ok.ValueKind != JsonValueKind.True) return null;
        if (!entry.TryGetProperty("data", out var d)) return null;

        string? price = null, original = null;
        var discount = 0;
        if (d.TryGetProperty("price_overview", out var po))
        {
            price = Str(po, "final_formatted");
            discount = Int(po, "discount_percent") ?? 0;
            if (discount > 0) original = Str(po, "initial_formatted");
        }
        var isFree = d.TryGetProperty("is_free", out var f) && f.ValueKind == JsonValueKind.True;
        if (isFree) price = "Gratuit";

        var item = new StoreItem("steam", id, Str(d, "name") ?? id, Str(d, "header_image"), price, discount, original);
        return new StoreDetails(item,
            Str(d, "short_description") ?? "",
            Join(d, "developers"), Join(d, "publishers"),
            Array(d, "genres", "description"),
            Array(d, "screenshots", "path_thumbnail"),
            $"https://store.steampowered.com/app/{id}", isFree);
    }

    private static string FormatPrice(int cents, string? currency) =>
        cents == 0 ? "Gratuit" : (cents / 100.0).ToString("0.00", Fr) + " " + (currency is null or "EUR" ? "€" : currency);

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        } : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static string? Join(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var parts = arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static IReadOnlyList<string> Array(JsonElement e, string name, string field)
    {
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return System.Array.Empty<string>();
        return arr.EnumerateArray().Select(x => Str(x, field)).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).ToList();
    }
}
