using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Listings.RenewListing;

public sealed class RenewListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryRepository categories,
    TimeProvider timeProvider,
    IOptions<ListingOptions> listingOptions)
    : ICommandHandler<RenewListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        RenewListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        if (listing.SellerId != command.RequesterId)
            return Result<Guid>.Failure(
                Error.Forbidden("Listing.NotOwner", "Only the seller can renew this listing."));

        // Same rule as publish: a listing in a deactivated category cannot go public again. Only an
        // expired listing is checked, so any other status still gets Listing.NotExpired from Renew.
        if (listing.Status == ListingStatus.Expired)
        {
            var category = await categories.GetByKeyAsync(listing.Category, ct);
            var categoryError = category?.CheckAcceptsListings();
            if (categoryError is not null)
                return Result<Guid>.Failure(categoryError);
        }

        var renewed = listing.Renew(timeProvider.GetUtcNow().AddDays(listingOptions.Value.ExpiryDays));

        if (renewed.IsFailure)
            return Result<Guid>.Failure(renewed.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
