using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Admin.PriceAccuracy;

// Pure query: reads SaleRecords with AsNoTracking (through the repository),
// computes MAE in lei and MAPE in %, overall and per category, then returns
// the ten newest records so an admin can eyeball the model's calls. No
// IUnitOfWork; no state change.
public sealed class PriceAccuracyHandler(ISaleRecordRepository saleRecords)
    : IQueryHandler<PriceAccuracyQuery, PriceAccuracyResponse>
{
    public async Task<Result<PriceAccuracyResponse>> HandleAsync(
        PriceAccuracyQuery query, CancellationToken ct)
    {
        var records = await saleRecords.GetAllAsync(ct);

        if (records.Count == 0)
        {
            return new PriceAccuracyResponse(
                Count: 0,
                Mae: null,
                Mape: null,
                PerCategory: [],
                LastRecords: []);
        }

        var mae = MeanAbsoluteError(records);
        var mape = MeanAbsolutePercentageError(records);

        var perCategory = records
            .GroupBy(r => r.Category)
            .Select(g => new PriceAccuracyPerCategory(
                g.Key,
                g.Count(),
                MeanAbsoluteError(g.ToList())))
            .OrderBy(c => c.Category)
            .ToList();

        var lastRecords = records
            .OrderByDescending(r => r.SoldAt)
            .ThenByDescending(r => r.Id)
            .Take(PriceAccuracyQuery.LastRecordsCount)
            .Select(r => new PriceAccuracyRecord(
                r.ListingId, r.Category, r.Condition,
                r.SuggestedPrice, r.SoldPrice, r.SoldAt))
            .ToList();

        return new PriceAccuracyResponse(records.Count, mae, mape, perCategory, lastRecords);
    }

    // MAE in lei: average of |suggested - sold|.
    private static decimal MeanAbsoluteError(List<SaleRecord> records)
    {
        decimal sum = 0m;
        foreach (var r in records)
            sum += Math.Abs(r.SuggestedPrice - r.SoldPrice);
        return Math.Round(sum / records.Count, 2);
    }

    // MAPE in %: average of 100 * |suggested - sold| / sold. Sold prices
    // are > 0 (Money guards against 0 and the factory enforces it again),
    // so no division-by-zero branch.
    private static decimal MeanAbsolutePercentageError(List<SaleRecord> records)
    {
        decimal sum = 0m;
        foreach (var r in records)
            sum += Math.Abs(r.SuggestedPrice - r.SoldPrice) / r.SoldPrice;
        return Math.Round(100m * sum / records.Count, 2);
    }
}
