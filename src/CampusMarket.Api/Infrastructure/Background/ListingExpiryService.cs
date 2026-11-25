using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Listings.ExpireListings;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

// Same pattern as ReservationExpiryService: one run at start, then one on every tick of a
// PeriodicTimer driven by the TimeProvider; a failed run is logged and the next tick tries again.
public sealed partial class ListingExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<ListingExpiryOptions> options,
    TimeProvider timeProvider,
    ILogger<ListingExpiryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;
        using var timer = new PeriodicTimer(interval, timeProvider);

        LogStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                // Catch so one failed run does not stop the service.
                LogRunException(logger, ex);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        // The handler is Scoped (depends on the DbContext); each tick gets its own scope.
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ExpireListingsCommand, int>>();

        var result = await handler.HandleAsync(new ExpireListingsCommand(), ct);

        if (result.IsFailure)
        {
            LogRunFailed(logger, result.Error.Code, result.Error.Message);
            return;
        }

        var expired = result.Value;
        if (expired == 0)
            return;

        // Expired listings leave the search, so the cached search results are cleared.
        var cache = scope.ServiceProvider.GetRequiredService<IOutputCacheStore>();
        await ListingsCache.ClearAsync(cache, ct);

        LogExpired(logger, expired);
    }

    // Source-generated log methods: the arguments are only formatted when the log level is enabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "ListingExpiryService started; running every {Interval}.")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Error, Message = "ListingExpiryService run failed.")]
    private static partial void LogRunException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "ListingExpiryService run failed: {Code} {Message}")]
    private static partial void LogRunFailed(ILogger logger, string code, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "ListingExpiryService expired {Count} listing(s).")]
    private static partial void LogExpired(ILogger logger, int count);
}
