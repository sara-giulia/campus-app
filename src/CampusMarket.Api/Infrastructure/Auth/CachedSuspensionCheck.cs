using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Domain.Members;
using Microsoft.Extensions.Caching.Memory;

namespace CampusMarket.Api.Infrastructure.Auth;

// One cache entry for the whole app: the ids of the members suspended right now (few), read from the
// database at most every CacheDuration. Scoped (it uses the request's repository); the cache itself is
// a singleton, so every request shares the entry. It works because the API runs as a single instance
// (Render), like InMemoryLiveEvents. A temporary suspension that ends by itself is noticed within
// CacheDuration; suspending or lifting one invalidates the entry at once.
public sealed class CachedSuspensionCheck(IMemoryCache cache, IMemberRepository members, TimeProvider timeProvider)
    : ISuspensionCheck
{
    public const string CacheKey = "auth:suspended-member-ids";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> IsSuspendedAsync(Guid memberId, CancellationToken ct = default)
    {
        if (!cache.TryGetValue(CacheKey, out IReadOnlySet<Guid>? suspendedIds) || suspendedIds is null)
        {
            suspendedIds = await members.GetSuspendedIdsAsync(timeProvider.GetUtcNow(), ct);
            cache.Set(CacheKey, suspendedIds, CacheDuration);
        }

        return suspendedIds.Contains(memberId);
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
