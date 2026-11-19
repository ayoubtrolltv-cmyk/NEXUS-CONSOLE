namespace Nexus.Store;

public sealed record StoreItem(string Source, string Id, string Name, string? ImageUrl, string? PriceText, int DiscountPercent, string? OriginalPriceText);

public sealed record StoreDetails(StoreItem Item, string Description, string? Developers, string? Publishers,
    IReadOnlyList<string> Genres, IReadOnlyList<string> Screenshots, string PageUrl, bool IsFree);

public sealed class StoreUnavailableException : Exception
{
    public StoreUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Source de catalogue. Architecture extensible : une classe par boutique disposant de données autorisées.</summary>
public interface IStoreSource
{
    string Name { get; }
    IReadOnlyList<(string Key, string Label)> Categories { get; }
    Task<IReadOnlyList<StoreItem>> GetCategoryAsync(string categoryKey, CancellationToken ct);
    Task<StoreDetails?> GetDetailsAsync(string id, CancellationToken ct);
    /// <summary>Page officielle du jeu : l'achat et le téléchargement se font toujours sur le site de la boutique.</summary>
    string PageUrl(string id);
}
