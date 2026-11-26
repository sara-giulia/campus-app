using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Infrastructure.Search;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

// Keeps the vectors of the search by meaning up to date, off the requests. At start it indexes every
// Active listing that needs a vector (the seed, listings published before a deploy, a new model), then
// it waits for ListingIndexSignal: a save that touched a listing (publish, edit) rings it. No timer:
// nothing runs while nobody changes listings, so the database (Neon) can sleep.
// After new vectors the cached search results are cleared, so the new listings show up at once.
public sealed partial class ListingIndexService(
    IServiceProvider services,
    IServiceScopeFactory scopeFactory,
    ListingIndexSignal signal,
    IOutputCacheStore outputCache,
    IOptions<SemanticSearchOptions> options,
    ILogger<ListingIndexService> logger)
    : BackgroundService
{
    // A publish is followed by the photo uploads and the order call; one pass after them is enough.
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (services.GetService<ITextEmbedder>() is null)
        {
            LogOff(logger, options.Value.Enabled ? $"the model files are missing in {options.Value.FullModelPath}" : "SemanticSearch:Enabled is false");
            return;
        }

        await IndexAsync(stoppingToken);
        // When the app stops the signal is closed, so the loop ends after the pass it is doing.
        await using var stopping = stoppingToken.Register(signal.Complete);
        await foreach (var _ in signal.ReadAllAsync())
        {
            await Task.Delay(Debounce, stoppingToken);
            await IndexAsync(stoppingToken);
        }
    }

    private async Task IndexAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var indexed = await scope.ServiceProvider.GetRequiredService<ListingIndexer>().IndexPendingAsync(ct);
            if (indexed == 0)
                return;

            await ListingsCache.ClearAsync(outputCache, ct);
            LogIndexed(logger, indexed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The next change tries again; the listings not indexed are still found by their words.
            LogFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search by meaning is off ({Reason}); the search uses the words only")]
    private static partial void LogOff(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Indexed {Count} listings for the search by meaning")]
    private static partial void LogIndexed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Indexing listings for the search by meaning failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
