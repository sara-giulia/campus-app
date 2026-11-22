using System.Text.Json;
using CampusMarket.ML.Training.Evaluation;
using static CampusMarket.ML.Training.Evaluation.ClassificationReport;
using static CampusMarket.ML.Training.Evaluation.ThresholdSelection;

namespace CampusMarket.ML.Tests;

// Choosing the category minimum confidence and the spam threshold on validation data
// (ThresholdSelection), and the check that the API is configured with the chosen values.
public sealed class ThresholdSelectionTests
{
    private const double Precision = 1e-9;

    // confidence / right: 0.9 yes, 0.8 yes, 0.7 no, 0.6 yes, 0.4 no
    private static readonly MulticlassPrediction[] Category =
    [
        new("A", "A", 0.9), new("B", "B", 0.8), new("A", "B", 0.7), new("C", "C", 0.6), new("B", "C", 0.4),
    ];

    // spam / probability: yes 0.9, yes 0.6, no 0.3, no 0.1
    private static readonly BinaryPrediction[] Spam =
    [
        new(true, 0.9), new(true, 0.6), new(false, 0.3), new(false, 0.1),
    ];

    private static readonly double[] TwoCandidates = [0.5, 0.75];

    private static readonly double[] FourCandidates = [0.2, 0.4, 0.5, 0.7];

    private static readonly double[] FiveCandidates = [0.2, 0.4, 0.5, 0.6, 0.7];

    private static readonly double[] OnlyHighCandidate = [0.99];

    [Fact]
    public void Candidates_go_from_the_first_to_the_last_value_in_equal_steps()
    {
        var candidates = Candidates(0.30, 0.95, 0.05);

        candidates.Should().HaveCount(14);
        candidates[0].Should().Be(0.30);
        candidates[1].Should().Be(0.35);
        candidates[^1].Should().Be(0.95);
    }

    [Fact]
    public void Candidates_need_a_positive_step_and_a_valid_range()
    {
        var zeroStep = () => Candidates(0.1, 0.9, 0);
        var reversed = () => Candidates(0.9, 0.1, 0.1);

        zeroStep.Should().Throw<ArgumentOutOfRangeException>();
        reversed.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Confidence_table_counts_the_suggestions_shown_and_how_many_of_them_are_right()
    {
        var table = ConfidenceTable(Category, TwoCandidates);

        // 0.5: 0.9, 0.8, 0.7, 0.6 shown (4 of 5), 3 right
        table[0].Shown.Should().Be(4);
        table[0].Coverage.Should().BeApproximately(0.8, Precision);
        table[0].Accuracy.Should().BeApproximately(0.75, Precision);

        // 0.75: 0.9 and 0.8 shown, both right
        table[1].Shown.Should().Be(2);
        table[1].Coverage.Should().BeApproximately(0.4, Precision);
        table[1].Accuracy.Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void The_minimum_confidence_is_the_smallest_threshold_that_reaches_the_target()
    {
        var table = ConfidenceTable(Category, TwoCandidates);

        ChooseMinConfidence(table, targetAccuracy: 0.9).Should().Be(0.75);
        ChooseMinConfidence(table, targetAccuracy: 0.7).Should().Be(0.5);
    }

    [Fact]
    public void When_no_threshold_reaches_the_target_the_highest_one_is_kept()
    {
        var table = ConfidenceTable(Category, TwoCandidates);

        ChooseMinConfidence(table, targetAccuracy: 1.01).Should().Be(0.75);
    }

    [Fact]
    public void A_threshold_that_shows_nothing_is_never_chosen_as_good()
    {
        var table = ConfidenceTable(Category, OnlyHighCandidate);

        table[0].Shown.Should().Be(0);
        table[0].Accuracy.Should().Be(0);
        ChooseMinConfidence(table).Should().Be(0.99, "it is the only candidate, kept as the highest one");
    }

    [Fact]
    public void Choosing_needs_at_least_one_candidate()
    {
        var category = () => ChooseMinConfidence([]);
        var spam = () => ChooseSpamThreshold([]);

        category.Should().Throw<ArgumentException>();
        spam.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Spam_table_shows_good_listings_flagged_and_spam_missed_at_every_threshold()
    {
        var table = SpamTable(Spam, FourCandidates);

        // 0.2: the 0.3 good listing is flagged → precision 2/3, F1 0.8
        table[0].FalsePositives.Should().Be(1);
        table[0].F1.Should().BeApproximately(0.8, Precision);

        // 0.4 and 0.5: perfect
        table[1].F1.Should().BeApproximately(1, Precision);
        table[2].F1.Should().BeApproximately(1, Precision);

        // 0.7: the 0.6 spam is missed → recall 1/2
        table[3].FalseNegatives.Should().Be(1);
        table[3].Recall.Should().BeApproximately(0.5, Precision);
    }

    [Fact]
    public void The_spam_threshold_has_the_best_F1_and_is_the_middle_one_when_several_tie()
    {
        // best F1 at 0.4 and 0.5 → the first of the two middle ones
        ChooseSpamThreshold(SpamTable(Spam, FourCandidates)).Should().Be(0.4);

        // best F1 at 0.4, 0.5 and 0.6 → 0.5, away from both edges
        ChooseSpamThreshold(SpamTable(Spam, FiveCandidates)).Should().Be(0.5);
    }

    [Fact]
    public void Markdown_tables_mark_the_chosen_threshold()
    {
        ThresholdSelection.ToMarkdown(ConfidenceTable(Category, TwoCandidates), 0.75)
            .Should().Contain("| 0.75 ← chosen | 2 | 0.400 | 1.000 |");

        ThresholdSelection.ToMarkdown(SpamTable(Spam, FourCandidates), 0.4)
            .Should().Contain("| 0.40 ← chosen | 1.000 | 1.000 | 1.000 | 0 | 0 |");
    }

    // ---- the API uses the values chosen on validation ----

    [Fact]
    public void The_API_settings_are_the_thresholds_saved_with_the_trained_models()
    {
        var appSettings = ReadJson(Path.Combine(TestPaths.MlDirectory, "..", "src", "CampusMarket.Api", "appsettings.json"));
        var categoryModel = ReadJson(Path.Combine(TestPaths.MlDirectory, "models", "category-classification.json"));
        var spamModel = ReadJson(Path.Combine(TestPaths.MlDirectory, "models", "spam-detection.json"));

        var ml = appSettings.GetProperty("Ml");

        ml.GetProperty("Category").GetProperty("MinConfidence").GetDouble()
            .Should().Be(categoryModel.GetProperty("settings").GetProperty("minConfidence").GetDouble(),
                "Ml:Category:MinConfidence must be the value CategoryTrainer chose on validation; after retraining, copy it to appsettings.json.");

        ml.GetProperty("Spam").GetProperty("Threshold").GetDouble()
            .Should().Be(spamModel.GetProperty("settings").GetProperty("threshold").GetDouble(),
                "Ml:Spam:Threshold must be the value SpamTrainer chose on validation; after retraining, copy it to appsettings.json.");
    }

    private static JsonElement ReadJson(string path)
    {
        File.Exists(path).Should().BeTrue($"{path} must exist");
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return document.RootElement.Clone();
    }
}
