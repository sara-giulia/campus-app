namespace CampusMarket.Api.Common.Search;

// Which of the scored listings count as "found by meaning". The three numbers are chosen on the
// evaluation in docs/ml/semantic-search.md (ML.Tests/SemanticSearchEvaluationTests runs the same code):
// - MinSimilarity: below it a listing is not close enough, whatever the search. Without it a search
//   for something nobody sells ("frigider") would still list the "least far" listings.
// - MaxDistanceFromBest: a listing far below the best match is usually something else. The best score
//   differs a lot from one search to another, so a fixed threshold alone keeps too much or too little.
// - MaxResults: the matches by meaning come after the matches by words; a long tail only adds noise.
public sealed record SemanticSelectionRules(double MinSimilarity, double MaxDistanceFromBest, int MaxResults)
{
    public static readonly SemanticSelectionRules Default = new(0.36, 0.15, 10);
}

public static class SemanticMatchSelection
{
    public static IReadOnlyList<SemanticMatch> Select(IEnumerable<SemanticMatch> scored, SemanticSelectionRules rules)
    {
        var ordered = scored
            .Where(m => m.Similarity >= rules.MinSimilarity)
            .OrderByDescending(m => m.Similarity)
            .ThenBy(m => m.ListingId)
            .ToList();
        if (ordered.Count == 0)
            return [];

        var lowest = ordered[0].Similarity - rules.MaxDistanceFromBest;
        return ordered.TakeWhile(m => m.Similarity >= lowest).Take(rules.MaxResults).ToList();
    }
}
