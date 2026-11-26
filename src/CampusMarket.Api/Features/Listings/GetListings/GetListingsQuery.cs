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
    Condition? Condition = null,
    string? Sort = null,
    // The logged-in member, when there is one: the listings of the members they blocked are left out.
    Guid? ViewerId = null) : IQuery<GetListingsResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    // "relevance" (the default when there is a search) keeps the order of ListingSearch; without a search the default is "newest".
    public static readonly string[] AllowedSorts = ["relevance", "newest", "price_asc", "price_desc"];
}
    

