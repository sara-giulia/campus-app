using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Search;

// The vector of the search against the stored vectors, in PostgreSQL: cosine distance (<=>) with the
// HNSW index, only Active listings and vectors of the current model, at most MaxResults rows closer
// than MinSimilarity; then SemanticMatchSelection keeps the ones near the best.
// The vector of a search is kept in QueryEmbeddingCache, so the next page or the same search again
// does not run the model.
public sealed partial class SemanticListingSearch(
    CampusMarketDbContext db,
    ITextEmbedder embedder,
    QueryEmbeddingCache cache,
    IOptions<SemanticSearchOptions> options,
    ILogger<SemanticListingSearch> logger) : ISemanticSearch
{
    public async Task<IReadOnlyList<SemanticMatch>> FindAsync(string query, CancellationToken ct)
    {
        var rules = options.Value.Rules;
        try
        {
            var vector = new Vector(cache.GetOrAdd(query, embedder.EmbedQuery));
            var maxDistance = 1 - rules.MinSimilarity;

            var rows = await (
                    from e in db.ListingEmbeddings.AsNoTracking()
                    join l in db.Listings on e.ListingId equals l.Id
                    where l.Status == ListingStatus.Active && e.Model == embedder.ModelName
                    let distance = e.Embedding.CosineDistance(vector)
                    where distance <= maxDistance
                    orderby distance
                    select new { e.ListingId, Distance = distance })
                .Take(rules.MaxResults)
                .ToListAsync(ct);

            return SemanticMatchSelection.Select(rows.Select(r => new SemanticMatch(r.ListingId, 1 - r.Distance)), rules);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The words still find what they find: a broken model or vector must not break the search.
            LogFailed(logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Search by meaning failed; only the words were used")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

// Searches that only use the words: the model is turned off (SemanticSearch:Enabled) or its files
// are missing (ml/scripts/download-search-model.sh was not run).
public sealed class NoSemanticSearch : ISemanticSearch
{
    public Task<IReadOnlyList<SemanticMatch>> FindAsync(string query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SemanticMatch>>([]);
}
