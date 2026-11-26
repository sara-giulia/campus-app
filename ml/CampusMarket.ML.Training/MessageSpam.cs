using System.Globalization;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CampusMarket.ML.Training;

// One row of ml/data/messages.csv: a chat message, whether it is spam, and its template.
public sealed class MessageRow
{
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1)]
    public bool IsSpam { get; set; }

    [LoadColumn(2)]
    public string TemplateId { get; set; } = string.Empty;
}

// The split of the messages, by template: the normal templates and the spam templates are each sorted,
// shuffled with Random(Seed), the first 15% go to test and the rest are dealt into 5 folds. So test and
// every fold only hold kinds of messages (and spam patterns) the model did not see while training, and
// every part gets some of both. The folds choose the thresholds (cross-validation); test is used once.
public sealed class MessageSplit
{
    public const int FoldCount = 5;
    public const double TestFraction = 0.15;
    public const string Description = "85/15 grouped by TemplateId, thresholds by 5-fold grouped cross-validation on the 85%";

    private const int TestPart = -1;
    private readonly Dictionary<string, int> _partOf;

    private MessageSplit(Dictionary<string, int> partOf) => _partOf = partOf;

    public static MessageSplit Of(IEnumerable<MessageRow> rows)
    {
        var list = rows.ToList();
        var partOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var isSpam in new[] { false, true })
        {
            var ids = list.Where(r => r.IsSpam == isSpam).Select(r => r.TemplateId).Distinct()
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            var rng = new Random(DatasetSplit.Seed); // NOSONAR: deterministic split, not security
            for (var i = ids.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            var testCount = (int)Math.Round(ids.Count * TestFraction, MidpointRounding.AwayFromZero);
            for (var i = 0; i < ids.Count; i++)
                partOf[ids[i]] = i < testCount ? TestPart : (i - testCount) % FoldCount;
        }

        return new MessageSplit(partOf);
    }

    public bool IsTest(MessageRow row) => _partOf[row.TemplateId] == TestPart;

    public int FoldOf(MessageRow row) => _partOf[row.TemplateId];

    public IDataView Test(MLContext mlContext, IDataView data) =>
        mlContext.Data.FilterByCustomPredicate<MessageRow>(data, row => !IsTest(row));

    public IDataView Training(MLContext mlContext, IDataView data) =>
        mlContext.Data.FilterByCustomPredicate<MessageRow>(data, IsTest);

    // The training rows without one fold, and that fold.
    public (IDataView Train, IDataView Held) Fold(MLContext mlContext, IDataView data, int fold) => (
        mlContext.Data.FilterByCustomPredicate<MessageRow>(data, row => IsTest(row) || FoldOf(row) == fold),
        mlContext.Data.FilterByCustomPredicate<MessageRow>(data, row => IsTest(row) || FoldOf(row) != fold));
}

// The spam model of the chat messages: the text through FeaturizeText (words, pairs of words and
// groups of three letters, lower case, no diacritics) and SDCA logistic regression, like the spam model
// of the listings, but trained on messages. Two thresholds, chosen on the predictions of the 5 folds
// (each fold scored by a model trained without it):
//  - threshold: from here the message is shown with a warning; the best F0.5, which counts precision
//    twice as much as recall: a warning on a normal message between two students hurts the chat, and
//    the clear scams (a link, card data, money in advance) are also caught by the rules in the API;
//  - blockThreshold: from here the message is not sent at all; the smallest threshold, not below the
//    first one, at which no normal message of the folds would be blocked.
// The saved model is then trained on all the training rows and measured once on test.
public static class MessageSpamTrainer
{
    public const string ModelFileName = "message-spam.zip";
    public const string Name = "Message spam detection (SDCA logistic regression)";

    public static IEstimator<ITransformer> Pipeline(MLContext mlContext) =>
        mlContext.Transforms.Text.FeaturizeText("Features", nameof(MessageRow.Text))
            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                new Microsoft.ML.Trainers.SdcaLogisticRegressionBinaryTrainer.Options
                {
                    LabelColumnName = nameof(MessageRow.IsSpam),
                    FeatureColumnName = "Features",
                    NumberOfThreads = 1,
                }));

    public static TrainingResult Train(MLContext mlContext, IDataView data, MessageSplit split)
    {
        var crossValidated = new List<ClassificationReport.BinaryPrediction>();
        for (var fold = 0; fold < MessageSplit.FoldCount; fold++)
        {
            var (train, held) = split.Fold(mlContext, data, fold);
            crossValidated.AddRange(SpamTrainer.Predictions(mlContext, Pipeline(mlContext).Fit(train).Transform(held)));
        }

        var table = ThresholdSelection.SpamTable(crossValidated, ThresholdSelection.SpamCandidates);
        var threshold = ChooseWarningThreshold(table);
        var blockThreshold = ChooseBlockThreshold(table, threshold);
        var cvAtThreshold = ClassificationReport.Binary(crossValidated, threshold);

        var model = Pipeline(mlContext).Fit(split.Training(mlContext, data));
        var scored = model.Transform(split.Test(mlContext, data));
        var metrics = mlContext.BinaryClassification.Evaluate(scored, labelColumnName: nameof(MessageRow.IsSpam));
        var testPredictions = SpamTrainer.Predictions(mlContext, scored);
        var report = ClassificationReport.Binary(testPredictions);
        var atThreshold = ClassificationReport.Binary(testPredictions, threshold);
        var atBlock = ClassificationReport.Binary(testPredictions, blockThreshold);

        var text =
            $"Test, at the model's own 0.5:{Environment.NewLine}" +
            ClassificationReport.ToMarkdown(report) + Environment.NewLine +
            Environment.NewLine +
            "Thresholds, chosen on the 5 folds (warning: best F0.5; block: no normal message blocked):" + Environment.NewLine +
            ThresholdSelection.ToMarkdown(table, threshold) + Environment.NewLine +
            FormattableString.Invariant($"Folds at {threshold:0.00}: precision {cvAtThreshold.Precision:0.000}, recall {cvAtThreshold.Recall:0.000}, F1 {cvAtThreshold.F1:0.000}.") + Environment.NewLine +
            FormattableString.Invariant($"Test at {threshold:0.00} (warning): precision {atThreshold.Precision:0.000}, recall {atThreshold.Recall:0.000}, F1 {atThreshold.F1:0.000} (FP {atThreshold.FalsePositives}, FN {atThreshold.FalseNegatives}).") + Environment.NewLine +
            FormattableString.Invariant($"Test at {blockThreshold:0.00} (block): recall {atBlock.Recall:0.000}, normal messages blocked {atBlock.FalsePositives}.") + Environment.NewLine +
            FormattableString.Invariant($"API settings: Ml:MessageSpam:Threshold = {threshold:0.00}, Ml:MessageSpam:BlockThreshold = {blockThreshold:0.00}");

        var structured = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["accuracy"] = metrics.Accuracy,
            ["auc"] = metrics.AreaUnderRocCurve,
            ["cvF1AtThreshold"] = cvAtThreshold.F1,
            ["ece"] = report.Calibration.ExpectedCalibrationError,
            ["f1"] = metrics.F1Score,
            ["f1AtThreshold"] = atThreshold.F1,
            ["prAuc"] = report.PrAuc,
            ["precision"] = metrics.PositivePrecision,
            ["recall"] = metrics.PositiveRecall,
            ["recallAtBlock"] = atBlock.Recall,
        };

        var settings = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["blockThreshold"] = blockThreshold,
            ["threshold"] = threshold,
        };

        return new TrainingResult(model, text, "SDCA logistic regression", structured, settings);
    }

    // The candidate with the best F0.5 = 1.25·P·R / (0.25·P + R); the middle one when several tie.
    public static double ChooseWarningThreshold(IReadOnlyList<ThresholdSelection.SpamRow> table)
    {
        static double F05(ThresholdSelection.SpamRow r) =>
            r.Precision + r.Recall == 0 ? 0 : 1.25 * r.Precision * r.Recall / (0.25 * r.Precision + r.Recall);

        var best = table.Max(F05);
        var tied = table.Where(r => Math.Abs(F05(r) - best) < 1e-12).OrderBy(r => r.Threshold).ToList();
        return tied[(tied.Count - 1) / 2].Threshold;
    }

    // The smallest candidate at or above the warning threshold that blocks no normal message of the
    // folds; the highest candidate when every one would block some.
    public static double ChooseBlockThreshold(IReadOnlyList<ThresholdSelection.SpamRow> table, double warningThreshold)
    {
        var safe = table.Where(r => r.Threshold >= warningThreshold - 1e-9 && r.FalsePositives == 0)
            .OrderBy(r => r.Threshold)
            .FirstOrDefault();
        return safe?.Threshold ?? table.Max(r => r.Threshold);
    }
}

// Loads ml/data/messages.csv, trains the message spam model on its own MLContext (so the four listing
// models do not change), saves it with its .json, and scores the hand-written messages of
// ml/data/messages-external.csv.
public static class MessageSpamTraining
{
    public static void Run(string mlDir, string modelsDir)
    {
        var csvPath = Path.Combine(mlDir, "data", "messages.csv");
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var data = Load(mlContext, csvPath);
        var rows = mlContext.Data.CreateEnumerable<MessageRow>(data, reuseRowObject: false).ToList();
        var split = MessageSplit.Of(rows);
        var testCount = rows.Count(split.IsTest);
        var trainCount = rows.Count - testCount;

        Console.WriteLine();
        Console.WriteLine($"=== {MessageSpamTrainer.Name} ===");
        Console.WriteLine($"Messages: training {trainCount} ({rows.Where(r => !split.IsTest(r)).Select(r => r.TemplateId).Distinct().Count()} templates), " +
                          $"test {testCount} ({rows.Where(split.IsTest).Select(r => r.TemplateId).Distinct().Count()} templates)");

        var result = MessageSpamTrainer.Train(mlContext, data, split);
        Console.WriteLine(result.MetricsText);

        var modelPath = Path.Combine(modelsDir, MessageSpamTrainer.ModelFileName);
        mlContext.Model.Save(result.Model, data.Schema, modelPath);
        ModelMetadata.Save(modelPath, MessageSpamTrainer.Name, result.Algorithm, result.TestMetrics, csvPath,
            trainCount, validationRowCount: 0, testCount, result.Settings, MessageSplit.Description);
        Console.WriteLine($"Saved model to {modelPath}");

        var externalPath = Path.Combine(mlDir, "data", "messages-external.csv");
        if (File.Exists(externalPath))
            PrintExternal(mlContext, result.Model, externalPath, result.Settings!);
    }

    public static IDataView Load(MLContext mlContext, string path) =>
        mlContext.Data.LoadFromTextFile<MessageRow>(path, hasHeader: true, separatorChar: ',', allowQuoting: true);

    private static void PrintExternal(MLContext mlContext, ITransformer model, string path, IReadOnlyDictionary<string, double> settings)
    {
        var rows = mlContext.Data.CreateEnumerable<MessageRow>(Load(mlContext, path), reuseRowObject: false).ToList();
        var engine = mlContext.Model.CreatePredictionEngine<MessageRow, Scored>(model);
        var predictions = rows.Select(r => new ClassificationReport.BinaryPrediction(r.IsSpam, engine.Predict(r).Probability)).ToList();
        var warning = ClassificationReport.Binary(predictions, settings["threshold"]);
        var block = ClassificationReport.Binary(predictions, settings["blockThreshold"]);

        Console.WriteLine();
        Console.WriteLine($"Hand-written messages ({rows.Count}, {rows.Count(r => r.IsSpam)} spam), the model alone:");
        Console.WriteLine(FormattableString.Invariant($"  warning at {settings["threshold"]:0.00}: precision {warning.Precision:0.000}, recall {warning.Recall:0.000}, F1 {warning.F1:0.000} (FP {warning.FalsePositives}, FN {warning.FalseNegatives})"));
        Console.WriteLine(FormattableString.Invariant($"  block at {settings["blockThreshold"]:0.00}: recall {block.Recall:0.000}, normal messages blocked {block.FalsePositives}"));
        foreach (var (row, p) in rows.Zip(predictions).Where(x => (x.Second.Probability >= settings["threshold"]) != x.First.IsSpam))
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  mistake: {p.Probability:0.00} spam={row.IsSpam}  {row.Text}"));
    }

    private sealed class Scored
    {
        public float Probability { get; set; }
    }
}
