using System.Security.Cryptography;
using System.Text;
using Nexus.Core.Config;
using Nexus.Core.Logging;

namespace Nexus.Store;

/// <summary>Réponses JSON et images en cache disque (30 min pour le JSON), avec repli sur une copie périmée si le réseau est absent.</summary>
public sealed class StoreCache
{
    private static readonly HttpClient Shared = CreateClient();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly string _dir;

    public StoreCache(HttpClient? http = null, string? dir = null)
    {
        _http = http ?? Shared;
        _dir = dir ?? Path.Combine(AppPaths.Cache, "store");
        Directory.CreateDirectory(Path.Combine(_dir, "img"));
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("NexusConsole/0.12");
        return c;
    }

    public async Task<string> GetJsonAsync(string url, CancellationToken ct)
    {
        var file = Path.Combine(_dir, Hash(url) + ".json");
        if (File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < Ttl) return await File.ReadAllTextAsync(file, ct);
        try
        {
            var text = await _http.GetStringAsync(url, ct);
            await File.WriteAllTextAsync(file, text, ct);
            return text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            if (ct.IsCancellationRequested) throw;
            Log.Warn($"Boutique inaccessible : {url}", ex);
            if (File.Exists(file)) return await File.ReadAllTextAsync(file, ct);   // copie périmée valable en cas de panne
            throw new StoreUnavailableException("La boutique est inaccessible pour le moment. Vérifie ta connexion Internet.", ex);
        }
    }

    /// <summary>Télécharge une image dans le cache et retourne son chemin local, ou null en cas d'échec.</summary>
    public async Task<string?> GetImageAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var file = Path.Combine(_dir, "img", Hash(url) + ".jpg");
        if (File.Exists(file)) return file;
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            var tmp = file + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            File.Move(tmp, file, overwrite: true);
            return file;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            if (!ct.IsCancellationRequested) Log.Warn($"Image de boutique non téléchargée : {url}", ex);
            return null;
        }
    }

    public void Clear()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (Exception ex) { Log.Warn("Cache de la boutique non vidé.", ex); }
        Directory.CreateDirectory(Path.Combine(_dir, "img"));
    }

    private static string Hash(string s) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..20];
}
