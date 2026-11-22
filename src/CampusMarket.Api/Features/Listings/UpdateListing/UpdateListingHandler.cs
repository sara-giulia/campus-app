using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed class UpdateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryRepository categories)
    : ICommandHandler<UpdateListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        UpdateListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        if (listing.SellerId != command.RequesterId && !command.IsAdmin)
            return Result<Guid>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller or an Admin can change this listing."));

        // Only moving the listing into a deactivated category is refused: a listing that is already
        // in a deactivated category can still be edited while it stays there.
        if (command.Category != listing.Category)
        {
            var category = await categories.GetByKeyAsync(command.Category, ct);
            var categoryError = category?.CheckAcceptsListings();
            if (categoryError is not null)
                return Result<Guid>.Failure(categoryError);
        }

        var updated = listing.UpdateDetails(
            command.Title, command.Category, command.Condition, command.Price, command.Description);

        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
