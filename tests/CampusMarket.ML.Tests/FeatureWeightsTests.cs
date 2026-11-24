using CampusMarket.ML.Training;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;
using static CampusMarket.ML.Training.Evaluation.FeatureWeights;

namespace CampusMarket.ML.Tests;

// Explainability (FeatureWeights): reading the feature names of FeaturizeText, choosing the words
// with the biggest weights, and the word tables of the committed category and spam models.
public sealed class FeatureWeightsTests
{
    private static readonly string[] Slots =
    [
        "TitleFeatures.Word.notite",
        "DescriptionFeatures.Word.la|0738",
        "TitleFeatures.Char.n|o|t",
        "DescriptionFeatures.Word.manual",
        "TitleFeatures.Word.casti",
        "PriceFeatures",
    ];

    private static readonly double[] Weights = [2.5, 4.0, 9.0, -1.5, -3.0, 0.7];

    private static readonly string[] TieSlots = ["TitleFeatures.Word.b", "TitleFeatures.Word.a", "TitleFeatures.Word.c"];

    private static readonly double[] TieWeights = [1, 1, 1];

    private static readonly double[] OneWeight = [1.0];

    [Fact]
    public void Describe_reads_the_source_and_the_word_of_a_word_feature()
    {
        var single = Describe("TitleFeatures.Word.notite", 1.2);
        single.Should().Be(new WeightedFeature("notite", "Title", true, 1.2));

        var pair = Describe("DescriptionFeatures.Word.la|0738", 0.4);
        pair.Feature.Should().Be("la 0738", "the two words of a pair are joined with a space");
        pair.Source.Should().Be("Description");
    }

    [Fact]
    public void Letter_groups_and_other_features_are_not_words()
    {
        Describe("TitleFeatures.Char.n|o|t", 1).IsWord.Should().BeFalse();

        var price = Describe("PriceFeatures", 0.7);
        price.IsWord.Should().BeFalse();
        price.Feature.Should().Be("PriceFeatures");
    }

    [Fact]
    public void Top_lists_the_words_with_the_highest_weights_first_and_skips_the_letter_groups()
    {
        var top = Top(Slots, Weights, count: 5);

        // the letter group (9.0) and the price (not a word) are left out; negative weights too
        top.Select(f => f.Feature).Should().Equal("la 0738", "notite");
        top.Select(f => f.Weight).Should().BeInDescendingOrder();
    }

    [Fact]
    public void Top_toward_the_negative_side_lists_the_lowest_weights_first()
    {
        var top = Top(Slots, Weights, count: 5, towardPositive: false);

        top.Select(f => f.Feature).Should().Equal("casti", "manual");
    }

    [Fact]
    public void Top_returns_at_most_count_words()
    {
        Top(Slots, Weights, count: 1).Should().ContainSingle().Which.Feature.Should().Be("la 0738");
    }

    [Fact]
    public void Equal_weights_keep_the_order_of_the_slots()
    {
        Top(TieSlots, TieWeights, count: 3).Select(f => f.Feature).Should().Equal("b", "a", "c");
    }

    [Fact]
    public void There_must_be_one_weight_for_every_slot()
    {
        var act = () => Top(Slots, OneWeight, count: 3);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Markdown_tables_show_the_words_with_their_source_and_weight()
    {
        var byClass = new Dictionary<string, IReadOnlyList<WeightedFeature>>
        {
            ["Notes"] = [new("notite", "Title", true, 2.5)],
        };
        FeatureWeights.ToMarkdown(byClass).Should().Contain("| Notes | notite (T, 2.50) |");

        var spam = new SpamWeights(
            [new("whatsapp", "Description", true, 3.25)],
            [new("manual", "Title", true, -1.5)],
            PriceWeight: 0.7);
        FeatureWeights.ToMarkdown(spam)
            .Should().Contain("| whatsapp (D) | 3.25 | manual (T) | -1.50 |")
            .And.Contain("Price bins (one feature): weight 0.70");
    }

    // ---- the committed models ----

    [Fact]
    public void Committed_category_model_has_ten_positive_words_for_every_category()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "category-classification.zip"), out _);

        var byClass = ForCategory(model, LoadData(mlContext).Schema);

        byClass.Keys.Should().BeEquivalentTo("Electronics", "LabEquipment", "Notes", "Other", "Textbook");
        foreach (var (_, words) in byClass)
        {
            words.Should().HaveCount(DefaultCount);
            words.Should().OnlyContain(w => w.IsWord && w.Weight > 0);
            words.Select(w => w.Weight).Should().BeInDescendingOrder();
        }

        byClass["Notes"].Select(w => w.Feature).Should().Contain("notite", "notes are recognised by the word itself");
        byClass["Textbook"].Select(w => w.Feature).Should().Contain("manual");
    }

    [Fact]
    public void Committed_spam_model_pushes_contact_words_towards_spam()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = mlContext.Model.Load(Path.Combine(TestPaths.MlDirectory, "models", "spam-detection.zip"), out _);

        var spam = ForSpam(model, LoadData(mlContext).Schema);

        spam.TowardSpam.Should().HaveCount(DefaultCount).And.OnlyContain(w => w.Weight > 0);
        spam.TowardNormal.Should().HaveCount(DefaultCount).And.OnlyContain(w => w.Weight < 0);

        // After the 12 new spam patterns the top 10 words towards spam are generic words of the
        // generated spam texts (pe, in, intra, e, pret, lei, raspund, doar), so no contact word is in
        // the top 10 any more. The contact words still push towards spam: in the real run mesaj is
        // #23, link #29, "whatsapp," #38 and telegram #44 of the words towards spam.
        var allTowardSpam = ForSpam(model, LoadData(mlContext).Schema, count: MaxContactWordRank)
            .TowardSpam.Select(w => w.Feature).ToList();
        allTowardSpam.Should().Contain(ContactWords,
            "asking to move the talk to another app must still be a sign of spam (positive weight, top 50)");
    }

    private const int MaxContactWordRank = 50;

    // "whatsapp," keeps its comma: FeaturizeText splits on spaces only and the spam texts write
    // "pe whatsapp, ...", so that is the real word feature.
    private static readonly string[] ContactWords = ["whatsapp,", "telegram", "link", "mesaj"];

    private static IDataView LoadData(MLContext mlContext) =>
        mlContext.Data.LoadFromTextFile<ListingRow>(
            Path.Combine(TestPaths.MlDirectory, "data", "listings.csv"),
            hasHeader: true,
            separatorChar: ',',
            allowQuoting: true);
}
