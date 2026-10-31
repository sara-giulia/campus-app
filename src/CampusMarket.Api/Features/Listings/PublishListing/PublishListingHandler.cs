using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.PublishListing;

public sealed class PublishListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PublishListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        PublishListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        if (listing.SellerId != command.RequesterId && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller or an Admin can publish this listing."));

        var published = listing.Publish();

        if (published.IsFailure)
            return Result<Guid>.Failure(published.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
