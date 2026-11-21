using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Admin.PriceAccuracy;

public sealed record PriceAccuracyQuery : IQuery<PriceAccuracyResponse>
{
    public const int LastRecordsCount = 10;
}
