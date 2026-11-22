using CampusMarket.ML.Training;
using Microsoft.ML;
using static CampusMarket.ML.Training.Evaluation.ErrorAnalysis;

namespace CampusMarket.ML.Tests;

// Error analysis for the model cards (ErrorAnalysis): which mistakes are listed, in what order,
// and the lists on the committed models.
public sealed class ErrorAnalysisTests
{
    private static readonly CategoryMistake[] CategoryRows =
    [
        new("Manual algebra", "Textbook", "Textbook", 0.99),
        new("Set LED", "LabEquipment", "Other", 0.98),
        new("Notite curs", "Other", "Notes", 0.70),
        new("Kit Arduino", "Textbook", "LabEquipment", 0.98),
        new("Casti", "Electronics", "Electronics", 0.40),
    ];

    private static readonly SpamMistake[] SpamRows =
    [
        new("Bocanci", "purtati pe 3 trasee", 280, false, 0.42),
        new("Manual", "stare buna", 40, false, 0.05),
        new("Laptop ieftin", "platesti in avans", 900, true, 0.01),
        new("CASTIGA BANI", "scrie-mi pe telegram", 1, true, 0.97),
        new("Scaun", "", 1, true, 0.20),
        new("Lampa", "bonus la inregistrare", 1, false, 0.40),
    ];

    [Fact]
    public void Only_wrong_predictions_are_listed_most_confident_first()
    {
        var mistakes = MostConfidentMistakes(CategoryRows);

        mistakes.Select(m => m.Title).Should().Equal("Set LED", "Kit Arduino", "Notite curs");
    }

    [Fact]
    public void Equal_confidences_keep_the_order_of_the_rows_and_count_limits_the_list()
    {
        MostConfidentMistakes(CategoryRows, count: 2).Select(m => m.Title).Should().Equal("Set LED", "Kit Arduino");
    }

    [Fact]
    public void Spam_errors_split_flagged_normal_listings_from_spam_let_through()
    {
        var errors = SpamErrors(SpamRows, threshold: 0.40);

        // normal listings at or above the threshold, highest probability first (0.40 is flagged)
        errors.FlaggedNormal.Select(r => r.Title).Should().Equal("Bocanci", "Lampa");

        // spam below the threshold, lowest probability first
        errors.MissedSpam.Select(r => r.Title).Should().Equal("Laptop ieftin", "Scaun");
    }

    [Fact]
    public void A_higher_threshold_flags_fewer_normal_listings_and_misses_more_spam()
    {
        var errors = SpamErrors(SpamRows, threshold: 0.50);

        errors.FlaggedNormal.Should().BeEmpty();
        errors.MissedSpam.Select(r => r.Title).Should().Equal("Laptop ieftin", "Scaun");
    }

    [Fact]
    public void Markdown_lists_the_mistakes_and_says_when_there_are_none()
    {
        ToMarkdown(MostConfidentMistakes(CategoryRows))
            .Should().Contain("| Set LED | LabEquipment | Other | 0.98 |");
        ToMarkdown([]).Should().Be("No wrong predictions.");

        var spam = ToMarkdown(SpamErrors(SpamRows, 0.40), 0.40);
        spam.Should().Contain("Normal listings flagged as spam (probability ≥ 0.40): 2")
            .And.Contain("| Bocanci | purtati pe 3 trasee | 280 | 0.42 |")
            .And.Contain("Spam let through (probability < 0.40): 2");
    }

    [Fact]
    public void Markdown_cells_have_no_pipe_and_long_descriptions_are_shortened()
    {
        var row = new SpamMistake("A | B", new string('x', 100), 10, true, 0.1);

        var text = ToMarkdown(new SpamMistakes([], [row]), 0.40);

        text.Should().Contain("| A / B |");
        text.Should().Contain(new string('x', 69) + "…");
    }

    // ---- the committed models ----

    [Fact]
    public void Committed_category_model_lists_ten_wrong_predictions_on_the_test_split()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "category-classification.zip"), out _);

        var mistakes = CategoryMistakes(mlContext, model, TestRows(mlContext));

        mistakes.Should().HaveCount(DefaultCount).And.OnlyContain(m => m.Actual != m.Predicted);
        mistakes.Select(m => m.Confidence).Should().BeInDescendingOrder();
    }

    [Fact]
    public void Committed_spam_model_makes_no_mistake_on_the_test_split_at_the_configured_threshold()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "spam-detection.zip"), out _);

        var errors = SpamMistakesOf(mlContext, model, TestRows(mlContext), threshold: 0.40);

        errors.FlaggedNormal.Should().BeEmpty("the test part is saturated, see docs/ml/spam.md");
        errors.MissedSpam.Should().BeEmpty();
    }

    private static List<ListingRow> TestRows(MLContext mlContext)
    {
        var data = mlContext.Data.LoadFromTextFile<ListingRow>(
            Path.Combine(TestPaths.MlDirectory, "data", "listings.csv"),
            hasHeader: true,
            separatorChar: ',',
            allowQuoting: true);

        var (_, _, test) = DatasetSplit.Split(mlContext, data);
        return mlContext.Data.CreateEnumerable<ListingRow>(test, reuseRowObject: false).ToList();
    }
}
