using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeTrainingExampleRepository : ITrainingExampleRepository
{
    public List<TrainingExample> Items { get; } = new();

    public int AddCount { get; private set; }

    public void Add(TrainingExample example)
    {
        Items.Add(example);
        AddCount++;
    }

    public Task<TrainingExample?> GetAsync(Guid listingId, TrainingExampleKind kind, CancellationToken ct = default)
    {
        var example = Items.FirstOrDefault(e => e.ListingId == listingId && e.Kind == kind);
        return Task.FromResult(example);
    }

    public Task<IReadOnlyList<TrainingExample>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TrainingExample> snapshot = Items.ToList();
        return Task.FromResult(snapshot);
    }
}
