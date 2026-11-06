using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Moderation.HideListing;

public sealed class HideListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<HideListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(HideListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var hidden = listing.Hide();

        if (hidden.IsFailure)
            return Result<Guid>.Failure(hidden.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
