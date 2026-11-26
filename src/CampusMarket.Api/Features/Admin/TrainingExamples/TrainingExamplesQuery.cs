using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Admin.TrainingExamples;

public sealed record TrainingExamplesQuery : IQuery<TrainingExamplesResponse>
{
    public const int LatestCount = 20;
}
