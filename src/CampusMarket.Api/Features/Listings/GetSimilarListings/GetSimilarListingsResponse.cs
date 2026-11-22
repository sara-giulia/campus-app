using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetSimilarListings;

// At most five Active listings, the most similar first. Similarity is between 0 and 1, two decimals.
public sealed record GetSimilarListingsResponse(IReadOnlyList<SimilarListingItem> Items);

public sealed record SimilarListingItem(
    Guid Id,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price,
    double Similarity);
