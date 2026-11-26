using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListings;

public sealed record GetListingsResponse(IReadOnlyList<GetListingsItem> Items, int Count, int TotalCount, int Page, int PageSize);

public sealed record GetListingsItem(
    Guid Id,
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status,
    // How the search found it (Text or Meaning); null when there is no search.
    SearchMatch? Match = null)
{
    public static GetListingsItem From(Listing listing, SearchMatch? match = null) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        match);
}