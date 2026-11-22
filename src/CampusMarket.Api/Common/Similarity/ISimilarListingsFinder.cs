namespace CampusMarket.Api.Common.Similarity;

// Says how similar listings are, from their title and description. The implementation uses the
// similar-listings model (Infrastructure/ML/SimilarListingsFinder); handlers only see this interface.
public interface ISimilarListingsFinder
{
    // One similarity between 0 and 1 for every candidate, in the same order as the candidates.
    IReadOnlyList<double> Similarities(ListingText target, IReadOnlyList<ListingText> candidates);
}

public sealed record ListingText(string Title, string? Description);
