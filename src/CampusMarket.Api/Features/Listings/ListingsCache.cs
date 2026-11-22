using Microsoft.AspNetCore.OutputCaching;

namespace CampusMarket.Api.Features.Listings;

// The search results are cached under this tag; every endpoint that changes a listing clears it.
public static class ListingsCache
{
    public const string Tag = "listings";

    public static Task ClearAsync(IOutputCacheStore cache, CancellationToken ct) =>
        cache.EvictByTagAsync(Tag, ct).AsTask();
}
