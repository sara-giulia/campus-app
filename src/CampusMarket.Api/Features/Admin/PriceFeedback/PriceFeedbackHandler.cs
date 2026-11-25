using System.Globalization;
using System.Text;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Admin.PriceFeedback;

// Pure query: exports every SaleRecord as one CSV row in the ML dataset format,
// so the training project can evaluate the saved price model on real sales
// (ml/data/feedback.csv, evaluation only). Each record is joined with its
// Listing for Title and Description (the CURRENT text of the listing);
// records whose listing no longer exists are skipped. Category and Condition
// come from the record (the values at the time of the sale), label = SoldPrice.
// No IUnitOfWork; no state change.
public sealed class PriceFeedbackHandler(
    ISaleRecordRepository saleRecords,
    IListingRepository listings)
    : IQueryHandler<PriceFeedbackQuery, PriceFeedbackResponse>
{
    public async Task<Result<PriceFeedbackResponse>> HandleAsync(
        PriceFeedbackQuery query, CancellationToken ct)
    {
        var records = await saleRecords.GetAllAsync(ct);
        var listingsById = (await listings.GetAllAsync(ct)).ToDictionary(l => l.Id);

        // LF line endings, like the generated listings.csv.
        var csv = new StringBuilder();
        csv.Append(PriceFeedbackQuery.Header).Append('\n');

        var rowCount = 0;
        foreach (var record in records.OrderBy(r => r.SoldAt).ThenBy(r => r.Id))
        {
            if (!listingsById.TryGetValue(record.ListingId, out var listing))
                continue;

            csv.Append(Quote(listing.Title)).Append(',')
                .Append(Quote(listing.Description ?? string.Empty)).Append(',')
                .Append(record.Category.ToString()).Append(',')
                .Append(record.Condition.ToString()).Append(',')
                .Append(FormatPrice(record.SoldPrice)).Append(',')
                .Append("false").Append(',')
                .Append(Quote(PriceFeedbackQuery.TemplateId)).Append('\n');
            rowCount++;
        }

        return new PriceFeedbackResponse(rowCount, csv.ToString());
    }

    // Same quoting as the dataset generator: every text field in "…", an
    // embedded " doubled. Commas and new lines stay inside the quotes.
    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    // Invariant culture, no thousands separator: whole lei as "100" (like the
    // other CSV files), a fractional price as "99.5".
    private static string FormatPrice(decimal price) =>
        price.ToString("0.##", CultureInfo.InvariantCulture);
}
