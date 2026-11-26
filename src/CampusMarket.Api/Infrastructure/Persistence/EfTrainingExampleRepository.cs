using CampusMarket.Api.Domain.TrainingExamples;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfTrainingExampleRepository(CampusMarketDbContext db) : ITrainingExampleRepository
{
    public void Add(TrainingExample example) => db.TrainingExamples.Add(example);

    public Task<TrainingExample?> GetAsync(Guid listingId, TrainingExampleKind kind, CancellationToken ct = default) =>
        db.TrainingExamples.FirstOrDefaultAsync(e => e.ListingId == listingId && e.Kind == kind, ct);

    public async Task<IReadOnlyList<TrainingExample>> GetAllAsync(CancellationToken ct = default) =>
        await db.TrainingExamples.AsNoTracking().ToListAsync(ct);
}
