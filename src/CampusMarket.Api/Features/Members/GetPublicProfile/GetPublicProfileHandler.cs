using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Members.GetPublicProfile;

public sealed class GetPublicProfileHandler(
    IMemberRepository members,
    IListingRepository listings,
    IReviewRepository reviews)
    : IQueryHandler<GetPublicProfileQuery, GetPublicProfileResponse>
{
    public async Task<Result<GetPublicProfileResponse>> HandleAsync(GetPublicProfileQuery query, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(query.MemberId, ct);
        if (member is null)
            return Result<GetPublicProfileResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{query.MemberId}' was not found."));

        // Only what anyone may see: the member's Active listings, newest first.
        var active = (await listings.GetAllAsync(ct))
            .Where(l => l.SellerId == member.Id && l.Status == ListingStatus.Active)
            .OrderByDescending(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .Select(l => new PublicListingItem(l.Id, l.Title, l.Price.Amount))
            .ToList();

        var memberReviews = await reviews.GetBySellerAsync(member.Id, ct);
        double? average = memberReviews.Count == 0 ? null : Math.Round(memberReviews.Average(r => r.Rating), 2);

        return new GetPublicProfileResponse(
            member.Id, member.Name, member.CreatedAt, average, memberReviews.Count, active);
    }
}