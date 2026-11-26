using System.Text.Json;
using CampusMarket.ML.Training;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Tests;

// Quality bar for the saved spam model of the chat messages (ml/models/message-spam.zip).
//  - On the test part of ml/data/messages.csv (whole kinds of messages and spam patterns the model never
//    saw): AUC at least 0.97 and F1 at the warning threshold at least 0.85.
//  - On the messages written by hand (ml/data/messages-external.csv, not made by the generator): at the
//    warning threshold precision at least 0.95 and recall at least 0.80, and no normal message reaches the
//    block threshold, so the API never refuses a normal message of that set.
//  - The thresholds in message-spam.json are the ones in the API's appsettings.json.
//  - The split keeps every template in one part, and both parts have normal messages and spam.
public sealed class MessageSpamModelQualityTests
{
    private const double MinAuc = 0.97;
    private const double MinF1AtThreshold = 0.85;
    private const double MinExternalPrecision = 0.95;
    private const double MinExternalRecall = 0.80;

    [Fact]
    public void Model_reaches_the_quality_bar_on_the_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var data = MessageSpamTraining.Load(mlContext, Path.Combine(TestPaths.MlDirectory, "data", "messages.csv"));
        var split = MessageSplit.Of(mlContext.Data.CreateEnumerable<MessageRow>(data, reuseRowObject: false));

        var scored = LoadModel(mlContext).Transform(split.Test(mlContext, data));

        var metrics = mlContext.BinaryClassification.Evaluate(scored, labelColumnName: nameof(MessageRow.IsSpam));
        metrics.AreaUnderRocCurve.Should().BeGreaterThanOrEqualTo(MinAuc,
            "the model must separate spam from normal messages on kinds of messages it never saw.");

        var report = ClassificationReport.Binary(SpamTrainer.Predictions(mlContext, scored), Setting("threshold"));
        report.F1.Should().BeGreaterThanOrEqualTo(MinF1AtThreshold,
            "at the warning threshold the model must find spam without flagging many normal messages.");
    }

    [Fact]
    public void Hand_written_messages_are_classified_well_and_no_normal_one_is_blocked()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var engine = mlContext.Model.CreatePredictionEngine<MessageRow, Scored>(LoadModel(mlContext));
        var rows = mlContext.Data.CreateEnumerable<MessageRow>(
            MessageSpamTraining.Load(mlContext, Path.Combine(TestPaths.MlDirectory, "data", "messages-external.csv")),
            reuseRowObject: false).ToList();
        var predictions = rows.Select(r => new ClassificationReport.BinaryPrediction(r.IsSpam, engine.Predict(r).Probability)).ToList();

        var atWarning = ClassificationReport.Binary(predictions, Setting("threshold"));
        atWarning.Precision.Should().BeGreaterThanOrEqualTo(MinExternalPrecision);
        atWarning.Recall.Should().BeGreaterThanOrEqualTo(MinExternalRecall);

        var blockedNormal = rows.Zip(predictions)
            .Where(x => !x.First.IsSpam && x.Second.Probability >= Setting("blockThreshold"))
            .Select(x => x.First.Text)
            .ToList();
        blockedNormal.Should().BeEmpty("a normal message must never be refused by the API.");
        rows.Should().Contain(r => r.IsSpam).And.Contain(r => !r.IsSpam);
    }

    [Fact]
    public void Thresholds_of_the_model_are_the_ones_the_API_uses()
    {
        var appSettingsPath = Path.Combine(Directory.GetParent(TestPaths.MlDirectory)!.FullName, "src", "CampusMarket.Api", "appsettings.json");
        using var appSettings = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var section = appSettings.RootElement.GetProperty("Ml").GetProperty("MessageSpam");

        section.GetProperty("Threshold").GetDouble().Should().Be(Setting("threshold"));
        section.GetProperty("BlockThreshold").GetDouble().Should().Be(Setting("blockThreshold"));
        Setting("blockThreshold").Should().BeGreaterThanOrEqualTo(Setting("threshold"));
    }

    [Fact]
    public void Split_keeps_every_template_in_one_part_with_both_kinds_of_messages_in_each()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var data = MessageSpamTraining.Load(mlContext, Path.Combine(TestPaths.MlDirectory, "data", "messages.csv"));
        var rows = mlContext.Data.CreateEnumerable<MessageRow>(data, reuseRowObject: false).ToList();
        var split = MessageSplit.Of(rows);

        var test = rows.Where(split.IsTest).ToList();
        var training = rows.Where(r => !split.IsTest(r)).ToList();

        test.Select(r => r.TemplateId).Intersect(training.Select(r => r.TemplateId)).Should().BeEmpty();
        test.Should().Contain(r => r.IsSpam).And.Contain(r => !r.IsSpam);
        training.Should().Contain(r => r.IsSpam).And.Contain(r => !r.IsSpam);
        training.Select(split.FoldOf).Distinct().Should().BeEquivalentTo(Enumerable.Range(0, MessageSplit.FoldCount));
        training.GroupBy(r => r.TemplateId).Should().OnlyContain(g => g.Select(split.FoldOf).Distinct().Count() == 1);
        ((double)test.Count / rows.Count).Should().BeInRange(0.08, 0.25);
    }

    [Fact]
    public void Warning_threshold_is_the_best_F_half_and_the_block_threshold_the_first_one_without_false_positives()
    {
        ThresholdSelection.SpamRow Row(double t, double p, double r, int fp) => new(t, p, r, 0, fp, 0);
        var table = new[]
        {
            Row(0.40, 0.70, 0.95, 9),
            Row(0.50, 0.90, 0.85, 3),
            Row(0.60, 0.95, 0.80, 1),
            Row(0.70, 1.00, 0.50, 0),
            Row(0.80, 1.00, 0.40, 0),
        };

        // F0.5: 0.739, 0.890, 0.916, 0.833, 0.769.
        MessageSpamTrainer.ChooseWarningThreshold(table).Should().Be(0.60);
        MessageSpamTrainer.ChooseBlockThreshold(table, 0.60).Should().Be(0.70);
        MessageSpamTrainer.ChooseBlockThreshold(table, 0.75).Should().Be(0.80);
        MessageSpamTrainer.ChooseBlockThreshold([Row(0.5, 0.9, 0.9, 2), Row(0.9, 0.9, 0.5, 1)], 0.5).Should().Be(0.9,
            "when every threshold blocks a normal message, the highest one is used.");
    }

    private static ITransformer LoadModel(MLContext mlContext)
    {
        var modelPath = Path.Combine(TestPaths.MlDirectory, "models", MessageSpamTrainer.ModelFileName);
        File.Exists(modelPath).Should().BeTrue($"the model file must exist at {modelPath}");
        return mlContext.Model.Load(modelPath, out _);
    }

    private static double Setting(string name)
    {
        var path = Path.Combine(TestPaths.MlDirectory, "models", "message-spam.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("settings").GetProperty(name).GetDouble();
    }

    private sealed class Scored
    {
        public float Probability { get; set; }
    }
}
