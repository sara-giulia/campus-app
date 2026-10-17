namespace CampusMarket.Api.Features.Listings.GetListingById;

/// <summary>
/// The id comes from the route: GET /api/v1/listings/{id}
/// </summary>
public sealed record GetListingByIdRequest(Guid Id);