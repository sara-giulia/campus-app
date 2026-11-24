using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Photos;
using CampusMarket.Api.Features.Photos.GenerateListingImage;

namespace CampusMarket.Api.Infrastructure.Background;

// Makes the generated images one by one, off the request: publishing a listing only puts its id in the
// queue. At start it also queues every Active listing (the handler skips the ones that have a photo), so
// listings published before a restart, and the demo seed on Render, get their picture too.
// Registered only when ImageGeneration:Enabled is true.
public sealed partial class ImageGenerationService(
    IServiceScopeFactory scopeFactory,
    IImageGenerationQueue queue,
    ILogger<ImageGenerationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await QueueActiveListingsAsync(stoppingToken);

        // When the app stops the queue is closed, so the loop ends after the image it is making.
        await using var stopping = stoppingToken.Register(queue.Complete);
        await foreach (var listingId in queue.ReadAllAsync())
            await GenerateAsync(listingId, stoppingToken);
    }

    private async Task QueueActiveListingsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var listings = await scope.ServiceProvider.GetRequiredService<IListingRepository>().GetAllAsync(ct);

        foreach (var listing in listings.Where(l => l.Status == ListingStatus.Active))
            queue.Enqueue(listing.Id);
    }

    private async Task GenerateAsync(Guid listingId, CancellationToken ct)
    {
        // The handler is Scoped (it uses the DbContext): one scope per image.
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<GenerateListingImageCommand, PhotoResponse>>();

        try
        {
            var result = await handler.HandleAsync(new GenerateListingImageCommand(listingId), ct);
            if (result.IsSuccess)
                LogGenerated(logger, listingId, result.Value.ContentType);
            else
                LogSkipped(logger, listingId, result.Error.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One failed image must not stop the others.
            LogFailed(logger, listingId, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated image ({ContentType}) for listing {ListingId}")]
    private static partial void LogGenerated(ILogger logger, Guid listingId, string contentType);

    // Not an error: the listing got a photo, was hidden or sold, or was deleted while it waited.
    [LoggerMessage(Level = LogLevel.Debug, Message = "No image generated for listing {ListingId}: {Code}")]
    private static partial void LogSkipped(ILogger logger, Guid listingId, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Image generation failed for listing {ListingId}")]
    private static partial void LogFailed(ILogger logger, Guid listingId, Exception exception);
}
