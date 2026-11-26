using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.Api.Features.Admin.TrainingExamples;

// Count 0 → PerLabel and Latest are empty (no examples yet).
public sealed record TrainingExamplesResponse(
    int Count,
    IReadOnlyList<TrainingExampleCount> PerLabel,
    IReadOnlyList<TrainingExampleItem> Latest);

public sealed record TrainingExampleCount(
    TrainingExampleKind Kind,
    string Label,
    int Count);

// CreatedBy is the member id only: no names or emails leave this endpoint.
// LastChangedAt is UpdatedAt when the example was relabelled, else CreatedAt.
public sealed record TrainingExampleItem(
    Guid Id,
    Guid ListingId,
    TrainingExampleKind Kind,
    string Title,
    string? Description,
    string Label,
    TrainingExampleSource Source,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastChangedAt)
{
    public static TrainingExampleItem From(TrainingExample e) => new(
        e.Id, e.ListingId, e.Kind, e.Title, e.Description, e.Label, e.Source,
        e.CreatedBy, e.CreatedAt, e.UpdatedAt ?? e.CreatedAt);
}
