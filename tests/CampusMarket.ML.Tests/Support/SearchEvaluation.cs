extern alias api;

using System.Text.Json;
using System.Text.RegularExpressions;
using api::CampusMarket.Api.Common.Search;
using api::CampusMarket.Api.Domain.Listings;
using api::CampusMarket.Api.Features.Listings.GetListings;

namespace CampusMarket.ML.Tests.Support;

// The evaluation of the search: a catalog of listings, labelled searches (ml/data/search/*.json) and
// the metrics, for three ways of searching:
// - Keyword: the search before this change (the title contains the text, case ignored, newest first);
// - Meaning: only the order by similarity, every listing, no threshold (what the model can do);
// - Hybrid: what GET /listings does now (ListingSearch.Rank with SemanticMatchSelection).
public sealed record LabelledQuery(string Query, IReadOnlyList<string> Relevant);

public sealed record CatalogItem(Listing Listing, string Label);

public sealed record SearchMetrics(double HitAt3, double MrrAt10, double PrecisionAt10, double RecallAt10, double ResultsForAbsent)
{
    public override string ToString() =>
        $"Hit@3 {HitAt3:F2} | MRR@10 {MrrAt10:F2} | P@10 {PrecisionAt10:F2} | R@10 {RecallAt10:F2} | results when nothing matches {ResultsForAbsent:F1}";
}

public static partial class SearchEvaluation
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<LabelledQuery> LoadQueries(string fileName)
    {
        var path = Path.Combine(TestPaths.MlDirectory, "data", "search", fileName);
        var file = JsonSerializer.Deserialize<QueryFile>(File.ReadAllText(path), Json)!;
        return file.Queries;
    }

    // A label matches a pattern exactly, or by prefix when the pattern ends with '*'.
    public static bool IsRelevant(CatalogItem item, LabelledQuery query) =>
        query.Relevant.Any(p => p.EndsWith('*') ? item.Label.StartsWith(p[..^1], StringComparison.Ordinal) : item.Label == p);

    public static IReadOnlyList<Guid> Keyword(IReadOnlyList<CatalogItem> catalog, string query) =>
        catalog.Select(c => c.Listing)
            .Where(l => l.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id)
            .Select(l => l.Id).ToList();

    public static IReadOnlyList<SemanticMatch> Scores(IReadOnlyList<CatalogItem> catalog, float[][] vectors, float[] query) =>
        catalog.Select((c, i) => new SemanticMatch(c.Listing.Id, Dot(vectors[i], query))).ToList();

    public static IReadOnlyList<Guid> MeaningOnly(IReadOnlyList<SemanticMatch> scores) =>
        scores.OrderByDescending(s => s.Similarity).Select(s => s.ListingId).ToList();

    public static IReadOnlyList<Guid> Hybrid(IReadOnlyList<CatalogItem> catalog, string query, IReadOnlyList<SemanticMatch> scores, SemanticSelectionRules rules) =>
        ListingSearch.Rank(catalog.Select(c => c.Listing), query, SemanticMatchSelection.Select(scores, rules))
            .Select(r => r.Listing.Id).ToList();

    public static SearchMetrics Measure(IReadOnlyList<CatalogItem> catalog, IReadOnlyList<LabelledQuery> queries, Func<LabelledQuery, IReadOnlyList<Guid>> search)
    {
        double hits = 0, mrr = 0, precision = 0, recall = 0, absent = 0;
        int withAnswer = 0, without = 0;
        foreach (var query in queries)
        {
            var results = search(query);
            var relevant = catalog.Where(c => IsRelevant(c, query)).Select(c => c.Listing.Id).ToHashSet();
            if (relevant.Count == 0)
            {
                without++;
                absent += results.Count;
                continue;
            }

            withAnswer++;
            var top10 = results.Take(10).ToList();
            if (results.Take(3).Any(relevant.Contains))
                hits++;
            var first = top10.FindIndex(relevant.Contains);
            if (first >= 0)
                mrr += 1.0 / (first + 1);
            if (top10.Count > 0)
                precision += top10.Count(relevant.Contains) / (double)top10.Count;
            recall += top10.Count(relevant.Contains) / (double)Math.Min(10, relevant.Count);
        }

        return new SearchMetrics(hits / withAnswer, mrr / withAnswer, precision / withAnswer, recall / withAnswer,
            without == 0 ? 0 : absent / without);
    }

    // The listings of ml/data/listings.csv that are not spam, the first three of every item type.
    public static IReadOnlyList<CatalogItem> DatasetCatalog(DateTimeOffset now)
    {
        var lines = File.ReadAllLines(Path.Combine(TestPaths.MlDirectory, "data", "listings.csv")).Skip(1);
        var perTemplate = new Dictionary<string, int>();
        var items = new List<CatalogItem>();
        var row = 0;
        foreach (var fields in lines.Select(ParseCsvLine))
        {
            row++;
            var (title, description, category, condition, price, isSpam, template) =
                (fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6]);
            if (isSpam == "true" || perTemplate.GetValueOrDefault(template) >= 3)
                continue;

            var created = Listing.Create(Guid.NewGuid(), title, Enum.Parse<Category>(category), Enum.Parse<Condition>(condition),
                decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), now.AddMinutes(-row),
                description.Length == 0 ? null : description);
            if (created.IsFailure)
                continue;
            perTemplate[template] = perTemplate.GetValueOrDefault(template) + 1;
            items.Add(new CatalogItem(created.Value, template));
        }
        return items;
    }

    private static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
            sum += a[i] * b[i];
        return sum;
    }

    // Standard CSV (ml/data/README.md): fields in "…", a quote inside written as "".
    private static string[] ParseCsvLine(string line) =>
        CsvField().Matches(line).Select(m => m.Groups[1].Success ? m.Groups[1].Value.Replace("\"\"", "\"") : m.Groups[2].Value).ToArray();

    // One field per match: a quoted field (group 1) or a plain one (group 2), then a comma or the end.
    [GeneratedRegex("""\G(?:"((?:[^"]|"")*)"|([^,]*))(?:,|$)""")]
    private static partial Regex CsvField();

    private sealed record QueryFile(IReadOnlyList<LabelledQuery> Queries);
}
