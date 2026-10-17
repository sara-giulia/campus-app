namespace CampusMarket.Api.Features.Listings.DeleteListing;

/// <summary>
/// The id comes from the route: DELETE /api/v1/listings/{id}
/// </summary>
public sealed record DeleteListingRequest(Guid Id);