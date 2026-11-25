using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Listings.PublishListing;

public sealed class PublishListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    ISpamDetector spamDetector,
    IReportRepository reports,
    TimeProvider timeProvider,
    ICategoryRepository categories,
    IOptions<ListingOptions> listingOptions)
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

        // A draft in a deactivated category cannot go public. Only a draft is checked, so a listing
        // that is not a draft still gets Listing.NotDraft from Publish, as before.
        if (listing.Status == ListingStatus.Draft)
        {
            var category = await categories.GetByKeyAsync(listing.Category, ct);
            var categoryError = category?.CheckAcceptsListings();
            if (categoryError is not null)
                return Result<Guid>.Failure(categoryError);
        }

        var published = listing.Publish(timeProvider.GetUtcNow().AddDays(listingOptions.Value.ExpiryDays));

        if (published.IsFailure)
            return Result<Guid>.Failure(published.Error);

        // The listing is public from now on, so the spam model checks it once. A suspicious listing
        // stays published but gets an automatic report, so Admins see it in the moderation list.
        var spam = spamDetector.Check(listing.Title, listing.Description, listing.Price.Amount);
        if (spam.IsSpam)
            await reports.AddAsync(Report.CreateAutomatic(listing.Id, spam.Probability, timeProvider.GetUtcNow()), ct);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
