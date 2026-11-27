using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Moderation.GetReportedMembers;

// Same paging rules as GetReportedListings.
public sealed record GetReportedMembersQuery(
    int Page = GetReportedMembersQuery.DefaultPage,
    int PageSize = GetReportedMembersQuery.DefaultPageSize) : IQuery<GetReportedMembersResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
