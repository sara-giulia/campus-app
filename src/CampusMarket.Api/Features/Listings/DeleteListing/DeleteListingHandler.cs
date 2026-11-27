using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public sealed class DeleteListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    IPhotoStorage photoStorage)
    : ICommandHandler<DeleteListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        DeleteListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        if (listing.SellerId != command.RequesterId && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller or an Admin can delete this listing."));

        repository.Delete(listing);
        await unitOfWork.CommitAsync(ct);

        // The photo rows went with the listing (cascade); their files are removed here, even if the request
        // is cancelled now, because the listing is already gone.
        await photoStorage.DeleteListingAsync(listing.Id, CancellationToken.None);

        return listing.Id;
    }
}
