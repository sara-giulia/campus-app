using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CancelReservation;

public sealed class CancelReservationHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelReservationCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CancelReservationCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var reservation = listing.Reservations.FirstOrDefault(r => r.Id == command.ReservationId);

        if (reservation is null)
            return Result<Guid>.Failure(
                Error.NotFound("Reservation.NotFound", $"Reservation '{command.ReservationId}' was not found."));

        var isBuyer = reservation.MemberId == command.RequesterId;
        var isSeller = listing.SellerId == command.RequesterId;

        if (!isBuyer && !isSeller && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Reservation.NotAllowed", "Only the buyer, the seller or an Admin can cancel this reservation."));

        var cancelled = listing.CancelReservation(command.ReservationId);

        if (cancelled.IsFailure)
            return Result<Guid>.Failure(cancelled.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return reservation.Id;
    }
}
