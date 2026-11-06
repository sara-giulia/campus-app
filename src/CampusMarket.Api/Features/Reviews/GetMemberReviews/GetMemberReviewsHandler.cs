using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Reviews.GetMemberReviews;

public sealed class GetMemberReviewsHandler(
    IMemberRepository members,
    IReviewRepository reviews)
    : IQueryHandler<GetMemberReviewsQuery, GetMemberReviewsResponse>
{
    public async Task<Result<GetMemberReviewsResponse>> HandleAsync(
        GetMemberReviewsQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetMemberReviewsResponse>.Failure(
                Error.Validation("Reviews.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetMemberReviewsQuery.MaxPageSize)
            return Result<GetMemberReviewsResponse>.Failure(
                Error.Validation("Reviews.InvalidPageSize",
                    $"Page size must be between 1 and {GetMemberReviewsQuery.MaxPageSize}."));

        var member = await members.GetByIdAsync(query.MemberId, ct);

        if (member is null)
            return Result<GetMemberReviewsResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{query.MemberId}' was not found."));

        var memberReviews = await reviews.GetBySellerAsync(query.MemberId, ct);

        double? average = memberReviews.Count == 0
            ? null
            : Math.Round(memberReviews.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero);

        var items = memberReviews
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MemberReviewItem.From)
            .ToList();

        return new GetMemberReviewsResponse(
            query.MemberId, average, memberReviews.Count, items, items.Count, query.Page, query.PageSize);
    }
}
