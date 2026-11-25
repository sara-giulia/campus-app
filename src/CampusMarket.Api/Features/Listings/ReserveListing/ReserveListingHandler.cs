using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Listings.ReserveListing;

public sealed class ReserveListingHandler(
    IListingRepository listings,
    IMemberRepository members,
    INotificationRepository notifications,
    IMemberBlockRepository blocks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ReserveListingCommand, ReserveListingResponse>
{
    // README: "A buyer can hold at most 3 active reservations."
    public const int MaxActiveReservations = 3;

    public async Task<Result<ReserveListingResponse>> HandleAsync(
        ReserveListingCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<ReserveListingResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var buyer = await members.GetByIdAsync(command.BuyerId, ct);

        if (buyer is null)
            return Result<ReserveListingResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.BuyerId}' was not found."));

        if (await blocks.IsBlockedAsync(listing.SellerId, command.BuyerId, ct))
            return Result<ReserveListingResponse>.Failure(MemberBlock.BlockedError());

        var allListings = await listings.GetAllAsync(ct);
        var activeReservations = allListings
            .SelectMany(l => l.Reservations)
            .Count(r => r.MemberId == command.BuyerId && r.Status == ReservationStatus.Active);

        if (activeReservations >= MaxActiveReservations)
            return Result<ReserveListingResponse>.Failure(
                Error.Validation("Reservation.LimitReached",
                    $"A buyer can hold at most {MaxActiveReservations} active reservations."));

        var now = timeProvider.GetUtcNow();
        var reserved = listing.Reserve(command.BuyerId, now);

        if (reserved.IsFailure)
            return Result<ReserveListingResponse>.Failure(reserved.Error);

        // Notify the seller that their listing was reserved, in the same unit of work.
        var notification = Notification.Create(
            listing.SellerId,
            NotificationType.ReservationCreated,
            $"{buyer.Name} reserved {listing.Title}",
            listing.Id,
            now);
        if (notification.IsSuccess)
            notifications.Add(notification.Value);

        // No repository.Update here: the listing loaded by GetByIdAsync is already tracked,
        // and EF Core finds the new reservation by itself and inserts it.
        // Update would mark the new reservation as "modified" and the save would fail.
        await unitOfWork.CommitAsync(ct);

        return ReserveListingResponse.From(reserved.Value);
    }
}
