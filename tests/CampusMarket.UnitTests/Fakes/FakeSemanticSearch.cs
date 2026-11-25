using CampusMarket.Api.Common.Search;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeSemanticSearch : ISemanticSearch
{
    public List<SemanticMatch> Matches { get; } = [];

    public List<string> Queries { get; } = [];

    public Task<IReadOnlyList<SemanticMatch>> FindAsync(string query, CancellationToken ct)
    {
        Queries.Add(query);
        IReadOnlyList<SemanticMatch> snapshot = Matches.ToList();
        return Task.FromResult(snapshot);
    }
}
