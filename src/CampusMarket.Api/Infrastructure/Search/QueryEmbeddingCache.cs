using Microsoft.Extensions.Caching.Memory;

namespace CampusMarket.Api.Infrastructure.Search;

// The vectors of the last searches, in memory (at most 1000, each kept 30 minutes after its last use).
// The key is the search in lower case with single spaces: the model lowercases the text anyway, so
// "Laptop  Dell" and "laptop dell" get the same vector. The diacritics stay, the model reads them.
public sealed class QueryEmbeddingCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1000 });

    public float[] GetOrAdd(string query, Func<string, float[]> embed)
    {
        var key = string.Join(' ', query.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (_cache.TryGetValue(key, out float[]? cached))
            return cached!;

        var vector = embed(key);
        _cache.Set(key, vector, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30) });
        return vector;
    }

    public void Dispose() => _cache.Dispose();
}
