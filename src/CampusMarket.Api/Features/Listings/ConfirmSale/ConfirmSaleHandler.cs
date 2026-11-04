using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.ConfirmSale;

public sealed class ConfirmSaleHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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

        var confirmed = listing.ConfirmSale(command.ReservationId, timeProvider.GetUtcNow());

        if (confirmed.IsFailure)
            return Result<Guid>.Failure(confirmed.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return command.ReservationId;
    }
}
