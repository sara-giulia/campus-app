using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Reviews.GetMemberReviews;

// Same paging rules as GetListings.
public sealed record GetMemberReviewsQuery(
    Guid MemberId,
    int Page = GetMemberReviewsQuery.DefaultPage,
    int PageSize = GetMemberReviewsQuery.DefaultPageSize) : IQuery<GetMemberReviewsResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
