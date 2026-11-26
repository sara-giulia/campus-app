namespace CampusMarket.Api.Domain.TrainingExamples;

public interface ITrainingExampleRepository
{
    /// <summary>
    /// Adds an example to the current unit of work. Saved by the next CommitAsync.
    /// </summary>
    void Add(TrainingExample example);

    /// <summary>
    /// The example of this listing and kind, tracked so a relabel is saved by the next CommitAsync;
    /// null if there is none yet.
    /// </summary>
    Task<TrainingExample?> GetAsync(Guid listingId, TrainingExampleKind kind, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all examples, in no particular order.
    /// </summary>
    Task<IReadOnlyList<TrainingExample>> GetAllAsync(CancellationToken ct = default);
}
