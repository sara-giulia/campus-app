using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Pricing;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Listings.ConfirmSale;

public sealed partial class ConfirmSaleHandler(
    IListingRepository repository,
    ISaleRecordRepository saleRecords,
    INotificationRepository notifications,
    IPriceSuggester suggester,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ConfirmSaleHandler> logger)
    : ICommandHandler<ConfirmSaleCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        ConfirmSaleCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        if (listing.SellerId != command.RequesterId && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller or an Admin can confirm this sale."));

        var now = timeProvider.GetUtcNow();
        // The buyer of the confirmed sale is the member on the matching reservation.
        var buyerId = listing.Reservations.FirstOrDefault(r => r.Id == command.ReservationId)?.MemberId;
        var confirmed = listing.ConfirmSale(command.ReservationId, now);

        if (confirmed.IsFailure)
            return Result<Guid>.Failure(confirmed.Error);

        repository.Update(listing);
        TryAddSaleRecord(listing, now);

        // Notify the buyer, in the same unit of work.
        if (buyerId is not null)
        {
            var notification = Notification.Create(
                buyerId.Value,
                NotificationType.ListingSold,
                $"{listing.Title} was sold to you",
                listing.Id,
                now);
            if (notification.IsSuccess)
                notifications.Add(notification.Value);
        }

        await unitOfWork.CommitAsync(ct);

        return command.ReservationId;
    }

    // Ask the price suggester for what it would have asked for this listing, pair
    // it with what the seller actually got, and add a SaleRecord. The sale itself
    // is already confirmed above — a suggester throw or a missing suggestion
    // (<= 0) only means we skip this feedback row, never that we lose the sale.
    private void TryAddSaleRecord(Listing listing, DateTimeOffset now)
    {
        int suggested;
        try
        {
            suggested = suggester.Suggest(listing.Title, listing.Description, listing.Category, listing.Condition);
        }
        catch (Exception ex)
        {
            LogSuggesterFailed(logger, ex);
            return;
        }

        if (suggested <= 0)
        {
            LogSuggesterReturnedNothing(logger, listing.Id);
            return;
        }

        var record = SaleRecord.Create(
            listing.Id, listing.Category, listing.Condition,
            suggestedPrice: suggested,
            soldPrice: listing.Price.Amount,
            soldAt: now);

        saleRecords.Add(record.Value);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Price suggester threw while recording a sale; no SaleRecord saved for this confirmation.")]
    private static partial void LogSuggesterFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Price suggester returned no usable suggestion (<= 0) for listing {ListingId}; no SaleRecord saved.")]
    private static partial void LogSuggesterReturnedNothing(ILogger logger, Guid listingId);
}
