namespace CampusMarket.Api.Common.Search;

// Finds the published (Active) listings whose title and description are close in meaning to a search
// text. The implementation compares the vector of the search with the stored vectors of the listings
// (pgvector); when the model is not available it finds nothing, so the search works on words only.
// Never throws because of the model or the vectors: the search must still answer.
public interface ISemanticSearch
{
    // The best first, already filtered with SemanticMatchSelection.
    Task<IReadOnlyList<SemanticMatch>> FindAsync(string query, CancellationToken ct);
}

// Similarity: the cosine between the vector of the search and the vector of the listing (1 = same meaning).
public sealed record SemanticMatch(Guid ListingId, double Similarity);
