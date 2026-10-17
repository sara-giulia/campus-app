namespace CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.Api.Domain.Listings;


public sealed record GetListingByIdResponse(
    Guid Id,
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    ListingStatus Status,
    int Version,
    DateTimeOffset CreatedAt)
{
    // Turns the domain object into what the API sends back.
    public static GetListingByIdResponse From(Listing listing) => new(
        listing.Id,
        listing.SellerId,
        listing.Title,
        listing.Category,
        listing.Condition,
        listing.Price.Amount,
        listing.Status,
        listing.Version,
        listing.CreatedAt);
}