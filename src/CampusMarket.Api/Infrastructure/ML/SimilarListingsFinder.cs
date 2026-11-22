using CampusMarket.Api.Common.Similarity;
using Microsoft.Extensions.ML;

namespace CampusMarket.Api.Infrastructure.ML;

// Turns every listing into its text vector with the similar-listings model and compares them with
// the cosine similarity. The vectors are unit length, so the cosine is their dot product; a listing
// with no known word has an empty vector and a similarity of 0. Negative values cannot happen
// (all weights are positive), but the result is clamped to 0–1 anyway.
public sealed class SimilarListingsFinder(PredictionEnginePool<SimilarInput, SimilarVector> pool) : ISimilarListingsFinder
{
    public const string ModelName = "SimilarListings";

    public IReadOnlyList<double> Similarities(ListingText target, IReadOnlyList<ListingText> candidates)
    {
        var targetVector = Vectorize(target);
        return candidates.Select(c => Math.Clamp(Dot(targetVector, Vectorize(c)), 0, 1)).ToList();
    }

    private float[] Vectorize(ListingText listing) =>
        pool.Predict(ModelName, new SimilarInput
        {
            Title = listing.Title,
            Description = listing.Description ?? string.Empty
        }).Vector;

    private static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
            sum += (double)a[i] * b[i];
        return sum;
    }
}
