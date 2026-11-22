using CampusMarket.Api.Common.Similarity;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeSimilarListingsFinder : ISimilarListingsFinder
{
    public Dictionary<string, double> SimilarityByTitle { get; } = new();

    public int CallCount { get; private set; }

    public ListingText? LastTarget { get; private set; }

    public IReadOnlyList<ListingText> LastCandidates { get; private set; } = [];

    public IReadOnlyList<double> Similarities(ListingText target, IReadOnlyList<ListingText> candidates)
    {
        CallCount++;
        LastTarget = target;
        LastCandidates = candidates.ToList();
        return candidates.Select(c => SimilarityByTitle.GetValueOrDefault(c.Title)).ToList();
    }
}
