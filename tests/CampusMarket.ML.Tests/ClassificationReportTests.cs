using CampusMarket.ML.Training;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;
using static CampusMarket.ML.Training.Evaluation.ClassificationReport;

namespace CampusMarket.ML.Tests;

// The classification evaluation framework (ClassificationReport): small hand-made examples whose
// results are worked out in the comments, plus the report on the committed category and spam models.
public sealed class ClassificationReportTests
{
    private const double Precision = 1e-4;

    // ---- multiclass ----

    // Confusion (rows = true): A → A 1, B 1 | B → B 2 | C → A 1, C 1
    // A: precision 1/2, recall 1/2, F1 0.5 | B: precision 2/3, recall 1, F1 0.8 | C: precision 1, recall 1/2, F1 2/3
    private static readonly MulticlassPrediction[] SixRows =
    [
        new("A", "A", 0.9), new("A", "B", 0.6),
        new("B", "B", 0.8), new("B", "B", 0.7),
        new("C", "A", 0.55), new("C", "C", 0.95),
    ];

    private static readonly int[][] ExpectedConfusion = [[1, 1, 0], [0, 2, 0], [1, 0, 1]];

    [Fact]
    public void Multiclass_builds_the_confusion_matrix_with_true_rows_and_predicted_columns()
    {
        var report = Multiclass(SixRows, ["A", "B", "C"]);

        report.Confusion.Should().BeEquivalentTo(ExpectedConfusion, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Multiclass_computes_precision_recall_and_F1_per_class()
    {
        var report = Multiclass(SixRows, ["A", "B", "C"]);

        var a = report.PerClass.Single(m => m.Label == "A");
        a.Support.Should().Be(2);
        a.Precision.Should().BeApproximately(0.5, Precision);
        a.Recall.Should().BeApproximately(0.5, Precision);
        a.F1.Should().BeApproximately(0.5, Precision);

        var b = report.PerClass.Single(m => m.Label == "B");
        b.PredictedCount.Should().Be(3);
        b.Precision.Should().BeApproximately(2.0 / 3, Precision);
        b.Recall.Should().BeApproximately(1, Precision);
        b.F1.Should().BeApproximately(0.8, Precision);

        var c = report.PerClass.Single(m => m.Label == "C");
        c.Precision.Should().BeApproximately(1, Precision);
        c.Recall.Should().BeApproximately(0.5, Precision);
        c.F1.Should().BeApproximately(2.0 / 3, Precision);
    }

    [Fact]
    public void Macro_F1_is_the_plain_mean_of_the_class_F1s_and_accuracy_counts_the_diagonal()
    {
        var report = Multiclass(SixRows, ["A", "B", "C"]);

        report.MacroF1.Should().BeApproximately((0.5 + 0.8 + 2.0 / 3) / 3, Precision);
        report.Accuracy.Should().BeApproximately(4.0 / 6, Precision);
    }

    [Fact]
    public void A_label_never_seen_nor_predicted_is_left_out_of_the_macro_F1()
    {
        var withUnused = Multiclass(SixRows, ["A", "B", "C", "D"]);

        withUnused.MacroF1.Should().BeApproximately((0.5 + 0.8 + 2.0 / 3) / 3, Precision);
        withUnused.PerClass.Single(m => m.Label == "D").Support.Should().Be(0);
    }

    [Fact]
    public void A_label_missing_from_the_list_is_rejected()
    {
        var act = () => Multiclass([new MulticlassPrediction("A", "Z", 0.9)], ["A", "B"]);

        act.Should().Throw<ArgumentException>().WithMessage("*'Z'*");
    }

    [Fact]
    public void Multiclass_calibration_uses_the_confidence_of_the_chosen_class_and_whether_it_was_right()
    {
        var report = Multiclass(SixRows, ["A", "B", "C"]);

        // bin 0.9–1.0: 0.9 (right) and 0.95 (right) → mean 0.925, observed 1
        var top = report.Calibration.Bins[9];
        top.Count.Should().Be(2);
        top.MeanPredicted.Should().BeApproximately(0.925, Precision);
        top.ObservedRate.Should().BeApproximately(1, Precision);

        // bin 0.5–0.6: 0.55 (wrong) → observed 0
        report.Calibration.Bins[5].ObservedRate.Should().Be(0);
    }

    // ---- binary ----

    // threshold 0.5: 0.9 T and 0.8 T are TP, 0.7 F is FP, 0.4 T is FN, 0.2 F is TN
    private static readonly BinaryPrediction[] FiveRows =
    [
        new(true, 0.9), new(true, 0.8), new(false, 0.7), new(true, 0.4), new(false, 0.2),
    ];

    [Fact]
    public void Binary_counts_the_confusion_and_computes_precision_recall_and_F1()
    {
        var report = Binary(FiveRows);

        (report.TruePositives, report.FalsePositives, report.TrueNegatives, report.FalseNegatives)
            .Should().Be((2, 1, 1, 1));
        report.Accuracy.Should().BeApproximately(0.6, Precision);
        report.Precision.Should().BeApproximately(2.0 / 3, Precision);
        report.Recall.Should().BeApproximately(2.0 / 3, Precision);
        report.F1.Should().BeApproximately(2.0 / 3, Precision);
    }

    [Fact]
    public void A_higher_threshold_trades_recall_for_precision()
    {
        var report = Binary(FiveRows, threshold: 0.75);

        report.Precision.Should().BeApproximately(1, Precision);
        report.Recall.Should().BeApproximately(2.0 / 3, Precision);
    }

    [Fact]
    public void PR_AUC_is_the_average_precision_over_the_ranking()
    {
        // positives at ranks 1, 2 and 4 of 5: (1/3)·1 + (1/3)·1 + (1/3)·(3/4) = 0.9167
        AveragePrecision(FiveRows).Should().BeApproximately(1.0 / 3 + 1.0 / 3 + 0.25, Precision);
    }

    [Fact]
    public void PR_AUC_is_one_for_a_perfect_ranking_and_zero_without_positives()
    {
        AveragePrecision([new(true, 0.9), new(true, 0.8), new(false, 0.1)]).Should().BeApproximately(1, Precision);
        AveragePrecision([new(false, 0.9), new(false, 0.1)]).Should().Be(0);
    }

    [Fact]
    public void Rows_with_the_same_probability_are_ranked_together_whatever_their_order()
    {
        var spamFirst = AveragePrecision([new(true, 0.5), new(false, 0.5)]);
        var spamLast = AveragePrecision([new(false, 0.5), new(true, 0.5)]);

        spamFirst.Should().BeApproximately(0.5, Precision);
        spamLast.Should().BeApproximately(0.5, Precision);
    }

    // ---- calibration ----

    [Fact]
    public void Calibration_compares_the_mean_probability_of_each_bin_with_how_often_it_happened()
    {
        // bin 0.9–1.0: 0.95 T, 0.95 T, 0.95 F, 1.0 T → mean 0.9625, observed 0.75, gap 0.2125 × 4 rows
        // bin 0.1–0.2: 0.15 F → mean 0.15, observed 0, gap 0.15 × 1 row
        // ECE = (0.85 + 0.15) / 5 = 0.2
        var report = Calibration([(0.95, true), (0.95, true), (0.95, false), (0.15, false), (1.0, true)]);

        report.Bins.Should().HaveCount(10);
        report.Bins[9].Count.Should().Be(4, "a probability of exactly 1 belongs to the last bin");
        report.Bins[9].MeanPredicted.Should().BeApproximately(0.9625, Precision);
        report.Bins[9].ObservedRate.Should().BeApproximately(0.75, Precision);
        report.Bins[1].Count.Should().Be(1);
        report.ExpectedCalibrationError.Should().BeApproximately(0.2, Precision);
    }

    [Fact]
    public void Calibration_of_nothing_has_empty_bins_and_no_error()
    {
        var report = Calibration([], bins: 5);

        report.Bins.Should().HaveCount(5).And.OnlyContain(b => b.Count == 0);
        report.ExpectedCalibrationError.Should().Be(0);
    }

    [Fact]
    public void Calibration_needs_at_least_one_bin()
    {
        var act = () => Calibration([(0.5, true)], bins: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- markdown ----

    [Fact]
    public void Markdown_reports_show_the_matrix_the_metrics_and_the_calibration()
    {
        var multiclass = ToMarkdown(Multiclass(SixRows, ["A", "B", "C"]));
        multiclass.Should().Contain("| A | 1 | 1 | 0 |").And.Contain("**Macro-F1**").And.Contain("ECE");

        var binary = ToMarkdown(Binary(FiveRows));
        binary.Should().Contain("TP 2, FP 1, TN 1, FN 1").And.Contain("| PR-AUC | 0.917 |").And.Contain("ECE");
    }

    // ---- the committed models ----

    private const double MinCategoryMacroF1 = 0.8;
    private const double MaxCategoryEce = 0.1;
    private const double MinSpamPrAuc = 0.95;

    [Fact]
    public void Committed_category_model_reaches_the_macro_F1_bar_and_is_calibrated_on_the_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "category-classification.zip"), out _);
        var test = LoadTestSplit(mlContext);
        var nonSpam = mlContext.Data.LoadFromEnumerable(
            mlContext.Data.CreateEnumerable<ListingRow>(test, reuseRowObject: false).Where(r => !r.IsSpam).ToList());

        var report = CategoryTrainer.Report(mlContext, model.Transform(nonSpam), nonSpam);

        report.Labels.Should().HaveCount(5);
        report.MacroF1.Should().BeGreaterThanOrEqualTo(MinCategoryMacroF1,
            "every category, not only the big ones, must be recognised on item types never seen in training.");
        report.Calibration.ExpectedCalibrationError.Should().BeLessThanOrEqualTo(MaxCategoryEce,
            "the confidence shown with a suggestion must be close to how often it is right.");
    }

    [Fact]
    public void Committed_spam_model_reaches_the_PR_AUC_bar_on_the_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "spam-detection.zip"), out _);

        var report = SpamTrainer.Report(mlContext, model.Transform(LoadTestSplit(mlContext)));

        report.PrAuc.Should().BeGreaterThanOrEqualTo(MinSpamPrAuc,
            "spam is rare, so PR-AUC (not accuracy) shows whether the model finds it without false alarms.");
    }

    private static IDataView LoadTestSplit(MLContext mlContext)
    {
        var data = mlContext.Data.LoadFromTextFile<ListingRow>(
            Path.Combine(TestPaths.MlDirectory, "data", "listings.csv"),
            hasHeader: true,
            separatorChar: ',',
            allowQuoting: true);

        var (_, _, test) = DatasetSplit.Split(mlContext, data);
        return test;
    }
}
