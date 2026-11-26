using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Listings.ExpireListings;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

public sealed partial class ListingExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<ListingExpiryOptions> options,
    TimeProvider timeProvider,
    ILogger<ListingExpiryService> logger)
    : PeriodicExpiryService(scopeFactory, timeProvider, logger)
{
    protected override string ServiceName => nameof(ListingExpiryService);

    protected override TimeSpan Interval => options.Value.Interval;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var handler = services.GetRequiredService<ICommandHandler<ExpireListingsCommand, int>>();

        var result = await handler.HandleAsync(new ExpireListingsCommand(), ct);

        if (result.IsFailure)
        {
            LogRunFailed(Logger, ServiceName, result.Error.Code, result.Error.Message);
            return;
        }

        var expired = result.Value;
        if (expired == 0)
            return;

        // Expired listings leave the search, so the cached search results are cleared.
        var cache = services.GetRequiredService<IOutputCacheStore>();
        await ListingsCache.ClearAsync(cache, ct);

        LogExpired(Logger, expired);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ListingExpiryService expired {Count} listing(s).")]
    private static partial void LogExpired(ILogger logger, int count);
}
