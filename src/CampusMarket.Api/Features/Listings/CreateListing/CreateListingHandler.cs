using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public sealed class CreateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ICategoryRepository categories)
    : ICommandHandler<CreateListingCommand, CreateListingResponse>
{
    public async Task<Result<CreateListingResponse>> HandleAsync(
        CreateListingCommand command, CancellationToken ct)
    {
        var required = Listing.RequireClassification(command.Category, command.Condition);
        if (required.IsFailure)
            return Result<CreateListingResponse>.Failure(required.Error);
        var (categoryKey, condition) = required.Value;

        // A deactivated category takes no new listings. An unknown category is not found here
        // and is refused by Listing.Create (Listing.CategoryInvalid), as before.
        var category = await categories.GetByKeyAsync(categoryKey, ct);
        var categoryError = category?.CheckAcceptsListings();
        if (categoryError is not null)
            return Result<CreateListingResponse>.Failure(categoryError);

        var created = Listing.Create(
            command.SellerId, command.Title, categoryKey,
            condition, command.Price, timeProvider.GetUtcNow(), command.Description);

        if (created.IsFailure)
            return Result<CreateListingResponse>.Failure(created.Error);

        var listing = created.Value;

        var added = await repository.AddAsync(listing, ct);
        if (!added)
            return Result<CreateListingResponse>.Failure(
                Error.Conflict("Listing.Duplicate", $"A listing with id '{listing.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return CreateListingResponse.From(listing);
    }
}
