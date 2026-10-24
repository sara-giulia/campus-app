using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.DeleteListing;

public sealed class DeleteListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        DeleteListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        repository.Delete(listing);
        await unitOfWork.SaveChangesAsync(ct);

        return listing.Id;
    }
}
