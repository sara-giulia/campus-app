using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed class UpdateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    ICategoryRepository categories,
    ITrainingExampleRepository trainingExamples,
    TimeProvider timeProvider)
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

        var required = Listing.RequireClassification(command.Category, command.Condition);
        if (required.IsFailure)
            return Result<Guid>.Failure(required.Error);
        var (categoryKey, condition) = required.Value;

        // Only moving the listing into a deactivated category is refused: a listing that is already
        // in a deactivated category can still be edited while it stays there.
        if (categoryKey != listing.Category)
        {
            var category = await categories.GetByKeyAsync(categoryKey, ct);
            var categoryError = category?.CheckAcceptsListings();
            if (categoryError is not null)
                return Result<Guid>.Failure(categoryError);
        }

        var updated = listing.UpdateDetails(
            command.Title, categoryKey, condition, command.Price, command.Description);

        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        await RecordCategoryChoiceAsync(listing, command, ct);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }

    // Only the seller's own choice counts (an Admin editing the listing is not a seller choice).
    // One example per listing: a later edit relabels it. A missing or unknown suggestion, or the
    // suggested category itself, records nothing and never fails the request.
    private async Task RecordCategoryChoiceAsync(Listing listing, UpdateListingCommand command, CancellationToken ct)
    {
        if (command.RequesterId != listing.SellerId)
            return;

        var now = timeProvider.GetUtcNow();
        var existing = await trainingExamples.GetAsync(listing.Id, TrainingExampleKind.Category, ct);
        if (existing is not null)
        {
            existing.RelabelCategory(listing, command.SuggestedCategory, command.RequesterId, now);
            return;
        }

        var created = TrainingExample.ForCategoryChoice(listing, command.SuggestedCategory, command.RequesterId, now);
        if (created.IsSuccess)
            trainingExamples.Add(created.Value);
    }
}
