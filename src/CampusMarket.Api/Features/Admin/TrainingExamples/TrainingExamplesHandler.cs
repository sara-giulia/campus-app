using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Admin.TrainingExamples;

// Pure query: how many labelled examples the feedback loop has collected, per kind and label,
// and the latest ones (newest change first). No IUnitOfWork; no state change.
public sealed class TrainingExamplesHandler(ITrainingExampleRepository trainingExamples)
    : IQueryHandler<TrainingExamplesQuery, TrainingExamplesResponse>
{
    public async Task<Result<TrainingExamplesResponse>> HandleAsync(
        TrainingExamplesQuery query, CancellationToken ct)
    {
        var all = await trainingExamples.GetAllAsync(ct);

        var perLabel = all
            .GroupBy(e => (e.Kind, e.Label))
            .Select(g => new TrainingExampleCount(g.Key.Kind, g.Key.Label, g.Count()))
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Label, StringComparer.Ordinal)
            .ToList();

        var latest = all
            .Select(TrainingExampleItem.From)
            .OrderByDescending(i => i.LastChangedAt)
            .ThenBy(i => i.Id)
            .Take(TrainingExamplesQuery.LatestCount)
            .ToList();

        return new TrainingExamplesResponse(all.Count, perLabel, latest);
    }
}
