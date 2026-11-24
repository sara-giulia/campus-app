using Microsoft.ML;

namespace CampusMarket.ML.Training;

// Shared 70/15/15 train/validation/test split used both by the training
// program and by the model quality tests. Keeping it here means the split
// and the seed live in one place and cannot drift.
//
// The split is group-aware: every row of a TemplateId ends up in exactly one
// of train, validation or test, so the quality tests evaluate on item types
// (and spam patterns) the model has never seen.
//
// Non-spam rows: both TrainTestSplit calls pass samplingKeyColumnName =
// TemplateId. ML.NET hashes the samplingKey column internally. If we pass the
// same seed to both cuts, the second one would hash each TemplateId to the same
// value as the first and shove every leftover row into one of the two new
// buckets, so the second cut uses Seed + 1 for an independent hash. The hash
// depends only on the TemplateId, so a non-spam row lands in the same part
// whatever other rows are in the file.
//
// Spam rows: every spam row has its pattern as TemplateId (spam-00, spam-01, ...),
// only about two dozen groups. Hashing so few groups gives no guarantee that a
// part gets any spam: with 24 patterns the hash sent none to test (each group
// has a 15% chance, 0.85^24 ≈ 2% for none). So the spam patterns are shuffled
// with Random(Seed) and cut by count, 70/15/15 of the patterns, so test and
// validation always hold whole patterns the model has not seen. A spam row
// whose TemplateId also belongs to non-spam rows follows those rows.
//
// Each part keeps the rows in file order.
public static class DatasetSplit
{
    public const int Seed = 2026;

    // 70% train, 15% validation, 15% test. First split holds out 30%,
    // the second cuts that 30% in half.
    public const double FirstTestFraction = 0.3;

    public const double SecondTestFraction = 0.5;

    private enum Part
    {
        Train,
        Validation,
        Test,
    }

    public static (IDataView Train, IDataView Validation, IDataView Test) Split(MLContext mlContext, IDataView data)
    {
        // The two cuts run on the caller's MLContext exactly as before: every component created
        // on an MLContext draws a seed from it, so doing anything else here would hand different
        // seeds to the trainers that run later (the category model would change).
        var firstSplit = mlContext.Data.TrainTestSplit(
            data,
            testFraction: FirstTestFraction,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: Seed);

        var secondSplit = mlContext.Data.TrainTestSplit(
            firstSplit.TestSet,
            testFraction: SecondTestFraction,
            samplingKeyColumnName: nameof(ListingRow.TemplateId),
            seed: Seed + 1);

        // Everything else runs on a private context with the same seed.
        var own = new MLContext(seed: Seed);
        var partOf = new Dictionary<string, Part>(StringComparer.Ordinal);
        AddNonSpamTemplates(own, firstSplit.TrainSet, Part.Train, partOf);
        AddNonSpamTemplates(own, secondSplit.TrainSet, Part.Validation, partOf);
        AddNonSpamTemplates(own, secondSplit.TestSet, Part.Test, partOf);

        var rows = own.Data.CreateEnumerable<ListingRow>(data, reuseRowObject: false).ToList();
        AssignSpamPatterns(rows, partOf);

        // Each part is a filter over the loaded file, so it keeps the rows in file order.
        IDataView Keep(Part part) =>
            own.Data.FilterByCustomPredicate<ListingRow>(data, row => partOf[row.TemplateId] != part);

        return (Keep(Part.Train), Keep(Part.Validation), Keep(Part.Test));
    }

    // The part of every non-spam TemplateId (the hash of ML.NET depends only on the TemplateId).
    private static void AddNonSpamTemplates(MLContext mlContext, IDataView view, Part part, Dictionary<string, Part> partOf)
    {
        foreach (var row in mlContext.Data.CreateEnumerable<ListingRow>(view, reuseRowObject: false).Where(r => !r.IsSpam))
        {
            partOf[row.TemplateId] = part;
        }
    }

    // Spam-only TemplateIds (the spam patterns), sorted, shuffled with Random(Seed), then the
    // first 15% (rounded) go to test, the next 15% to validation and the rest to train.
    private static void AssignSpamPatterns(List<ListingRow> rows, Dictionary<string, Part> partOf)
    {
        var patterns = rows
            .Where(r => r.IsSpam && !partOf.ContainsKey(r.TemplateId))
            .Select(r => r.TemplateId)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var rng = new Random(Seed); // NOSONAR: deterministic split, not security
        for (var i = patterns.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (patterns[i], patterns[j]) = (patterns[j], patterns[i]);
        }

        var heldOutShare = FirstTestFraction * SecondTestFraction;
        var testCount = (int)Math.Round(patterns.Count * heldOutShare, MidpointRounding.AwayFromZero);
        var validationCount = (int)Math.Round(patterns.Count * heldOutShare, MidpointRounding.AwayFromZero);

        for (var i = 0; i < patterns.Count; i++)
        {
            partOf[patterns[i]] = i < testCount
                ? Part.Test
                : i < testCount + validationCount ? Part.Validation : Part.Train;
        }
    }
}
