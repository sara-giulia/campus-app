using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed record GetListingsQuery(
    Category? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Search = null,
    int Page = GetListingsQuery.DefaultPage,
    int PageSize = GetListingsQuery.DefaultPageSize,
    Condition? Condition = null) : IQuery<GetListingsResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
    

