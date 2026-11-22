namespace CampusMarket.Api.Features.Listings.GetSimilarListings;

/// <summary>
/// The id comes from the route: GET /api/v1/listings/{id}/similar
/// </summary>
public sealed record GetSimilarListingsRequest(Guid Id);
