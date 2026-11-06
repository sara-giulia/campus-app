using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Reviews.AddReview;

public sealed class AddReviewHandler(
    IListingRepository listings,
    IMemberRepository members,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<AddReviewCommand, AddReviewResponse>
{
    public async Task<Result<AddReviewResponse>> HandleAsync(
        AddReviewCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);

        if (listing is null)
            return Result<AddReviewResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var member = await members.GetByIdAsync(command.ReviewerId, ct);

        if (member is null)
            return Result<AddReviewResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.ReviewerId}' was not found."));

        var created = Review.Create(command.ReviewerId, listing, command.Rating, command.Comment, timeProvider.GetUtcNow());

        if (created.IsFailure)
            return Result<AddReviewResponse>.Failure(created.Error);

        if (await reviews.ExistsForListingAsync(command.ListingId, ct))
            return Result<AddReviewResponse>.Failure(
                Error.Conflict("Review.AlreadyExists", "This sale has already been reviewed."));

        var review = created.Value;

        var added = await reviews.AddAsync(review, ct);
        if (!added)
            return Result<AddReviewResponse>.Failure(
                Error.Conflict("Review.Duplicate", $"A review with id '{review.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return AddReviewResponse.From(review);
    }
}
