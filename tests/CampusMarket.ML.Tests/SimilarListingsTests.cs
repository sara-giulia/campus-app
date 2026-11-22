using CampusMarket.ML.Training;
using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;
using Microsoft.ML.Data;
using static CampusMarket.ML.Training.Evaluation.SimilarityEvaluation;

namespace CampusMarket.ML.Tests;

// The similar-listings model: the evaluation (SimilarityEvaluation) on small hand-made vectors whose
// results are worked out in the comments, and the quality bar of the committed model.
public sealed class SimilarListingsTests
{
    private const double Precision = 1e-9;

    private static readonly float[] DenseWithGaps = [0, 2, 0, 3];

    private static SparseVector V(params float[] dense) => SparseVector.FromDense(dense);

    // A and D are the same item (group g1); B is another X; C is a Y.
    private static readonly Item[] FourItems =
    [
        new(V(1, 0), "X", "g1"),
        new(V(0.9f, 0.1f), "X", "g2"),
        new(V(0, 1), "Y", "g3"),
        new(V(1, 0), "X", "g1"),
    ];

    [Fact]
    public void A_sparse_vector_keeps_only_the_non_zero_entries_in_order()
    {
        var v = SparseVector.FromDense(DenseWithGaps);

        v.Indices.Should().Equal(1, 3);
        v.Values.Should().Equal(2f, 3f);
    }

    [Fact]
    public void Dot_and_cosine_only_use_the_entries_both_vectors_have()
    {
        Dot(V(1, 2, 0), V(3, 0, 5)).Should().BeApproximately(3, Precision);
        Cosine(V(1, 0), V(2, 0)).Should().BeApproximately(1, Precision);
        Cosine(V(1, 0), V(0, 1)).Should().BeApproximately(0, Precision);
    }

    [Fact]
    public void Cosine_with_an_empty_vector_is_zero()
    {
        Cosine(V(0, 0), V(1, 1)).Should().Be(0);
    }

    [Fact]
    public void Precision_at_k_leaves_out_the_same_item_and_counts_the_same_category()
    {
        // k = 1. A: candidates B, C (D is the same item) → B, same category → 1.
        // B: A, C, D → A (A and D tie, the first index wins), same category → 1.
        // C: A, B, D → B is the closest, other category → 0.   D: B, C → B → 1.   Mean 0.75.
        PrecisionAtK(FourItems, k: 1).Should().BeApproximately(0.75, Precision);
    }

    [Fact]
    public void Random_precision_is_the_share_of_candidates_with_the_same_category()
    {
        // A: B, C → 1/2.  B: A, C, D → 2/3.  C: A, B, D → 0.  D: B, C → 1/2.
        RandomPrecision(FourItems).Should().BeApproximately((0.5 + 2.0 / 3 + 0 + 0.5) / 4, Precision);
    }

    [Fact]
    public void Items_without_any_candidate_are_skipped()
    {
        Item[] sameItem = [new(V(1, 0), "X", "g1"), new(V(1, 0), "X", "g1")];

        PrecisionAtK(sameItem).Should().Be(0);
        RandomPrecision(sameItem).Should().Be(0);
    }

    // ---- the committed model ----

    private const double MinPrecisionAt5 = 0.6;
    private const double MinGainOverRandom = 0.3;

    [Fact]
    public void Committed_model_finds_listings_of_the_same_category_much_more_often_than_chance()
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var model = LoadModel(mlContext);
        var data = mlContext.Data.LoadFromTextFile<ListingRow>(
            Path.Combine(TestPaths.MlDirectory, "data", "listings.csv"), hasHeader: true, separatorChar: ',', allowQuoting: true);
        var (_, _, test) = DatasetSplit.Split(mlContext, data);

        var items = SimilarTrainer.Items(mlContext, model, test);
        var precision = PrecisionAtK(items);
        var random = RandomPrecision(items);

        precision.Should().BeGreaterThanOrEqualTo(MinPrecisionAt5,
            "most of the 5 closest listings of other item types must be of the same category.");
        (precision - random).Should().BeGreaterThanOrEqualTo(MinGainOverRandom,
            "the model must do clearly better than picking 5 listings at random.");
    }

    [Theory]
    [InlineData("Manual Analiza matematica", "Culegere Analiza matematica, exercitii", "Laptop Dell Latitude, i7")]
    [InlineData("Laptop Lenovo ThinkPad, i5", "Laptop HP EliteBook, i7", "Notite curs Drept civil")]
    [InlineData("Notite curs Structuri de date", "Notite curs Algoritmi", "Bicicleta de oras")]
    public void Committed_model_puts_a_listing_closer_to_one_of_its_kind(string target, string sameKind, string otherKind)
    {
        var mlContext = new MLContext(seed: DatasetSplit.Seed);
        var engine = mlContext.Model.CreatePredictionEngine<SimilarInput, SimilarOutput>(LoadModel(mlContext));
        SparseVector Vector(string title) => SparseVector.FromDense(engine.Predict(new SimilarInput { Title = title }).Vector);

        var toSameKind = Cosine(Vector(target), Vector(sameKind));
        var toOtherKind = Cosine(Vector(target), Vector(otherKind));

        toSameKind.Should().BeGreaterThan(toOtherKind, $"'{target}' is closer to '{sameKind}' than to '{otherKind}'.");
    }

    private static ITransformer LoadModel(MLContext mlContext)
    {
        var path = Path.Combine(TestPaths.MlDirectory, "models", "similar-listings.zip");
        File.Exists(path).Should().BeTrue($"the model file must exist at {path}");
        return mlContext.Model.Load(path, out _);
    }

    private sealed class SimilarInput
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }

    private sealed class SimilarOutput
    {
        [ColumnName(SimilarTrainer.VectorColumn)]
        public float[] Vector { get; set; } = [];
    }
}
