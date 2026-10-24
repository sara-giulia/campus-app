using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed class UpdateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        UpdateListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        var updated = listing.UpdateDetails(
            command.Title, command.Category, command.Condition, command.Price);

        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        repository.Update(listing);
        await unitOfWork.SaveChangesAsync(ct);

        return listing.Id;
    }
}
