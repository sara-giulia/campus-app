using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.GetReportedListings;

// Same paging rules as GetListings.
public sealed record GetReportedListingsQuery(
    int Page = GetReportedListingsQuery.DefaultPage,
    int PageSize = GetReportedListingsQuery.DefaultPageSize) : IQuery<GetReportedListingsResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
