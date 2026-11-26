extern alias api;

using api::CampusMarket.Api.Common.Search;
using api::CampusMarket.Api.Domain.Listings;
using api::CampusMarket.Api.Infrastructure.Persistence.Seed;
using api::CampusMarket.Api.Infrastructure.Search;
using CampusMarket.ML.Tests.Support;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace CampusMarket.ML.Tests;

// The search by meaning with the real model (EmbeddingGemma-300m, ml/models/semantic-search), on two
// catalogs that were not used to build the model:
// - the demo seed (54 Active listings, the ones on Render) with 31 labelled searches;
// - ml/data/listings.csv (966 listings, 3 of every item type) with 50 searches labelled by item type,
//   so the labels do not depend on the words of the titles.
// The searches are written the way a student would ask, mostly NOT with the words of the title
// ("calculator portabil" for a laptop, "scooter electric" for a trotinetă), plus a few for things
// nobody sells. The numbers are in docs/ml/semantic-search.md; the bars below are a bit under them,
// so a worse model, prompt or threshold fails here.
public sealed class SemanticSearchEvaluationTests(SemanticSearchEvaluationTests.Catalogs catalogs, ITestOutputHelper output)
    : IClassFixture<SemanticSearchEvaluationTests.Catalogs>
{
    private static readonly SemanticSelectionRules Rules = SemanticSelectionRules.Default;

    [SearchModelFact]
    public void The_tokens_are_the_ones_of_the_reference_tokenizer()
    {
        // Ids from the tokenizer.json of the same model revision (Hugging Face tokenizers), diacritics included.
        catalogs.Embedder.TokenIds("task: search result | query: căști fără fir").Should().Equal(
            2, 8071, 236787, 3927, 1354, 1109, 7609, 236787, 17158, 53864, 132666, 10430, 1);
        catalogs.Embedder.TokenIds("title: none | text: Birou pliabil cămin studentesc. Se pliază ușor, ocupă puțin loc.").Should().Equal(
            2, 3250, 236787, 7293, 1109, 1816, 236787, 3482, 521, 101836, 6032, 17158, 1387, 5117, 14323, 236761,
            636, 729, 23625, 237106, 162793, 504, 236764, 34316, 237106, 202234, 1413, 236761, 1);
    }

    [SearchModelFact]
    public void A_vector_has_768_values_and_length_one_and_a_paraphrase_is_closer_than_another_object()
    {
        var laptop = catalogs.Embedder.EmbedListing("Laptop Dell Latitude 5490", "Procesor i5, 8GB RAM, merge perfect.");
        var umbrella = catalogs.Embedder.EmbedListing("Umbrela de ploaie neagra", "Automata, in ambalajul original.");
        var query = catalogs.Embedder.EmbedQuery("computer portabil pentru facultate");

        // The name saved next to every vector: a new model or revision must change it, so the listings are indexed again.
        catalogs.Embedder.ModelName.Should().Be("embeddinggemma-300m-q4@5090578");
        laptop.Should().HaveCount(TextEmbedding.Dimensions);
        Math.Sqrt(laptop.Sum(v => (double)v * v)).Should().BeApproximately(1, 1e-4);
        Dot(query, laptop).Should().BeGreaterThan(Dot(query, umbrella) + 0.1);
    }

    [SearchModelFact]
    public void On_the_demo_catalog_the_new_search_finds_what_the_words_alone_do_not()
    {
        var (keyword, meaning, hybrid) = Evaluate(catalogs.Demo, catalogs.DemoVectors, catalogs.DemoQueries, "demo seed");

        keyword.HitAt3.Should().BeLessThan(0.15, "these searches are written with other words than the titles");
        hybrid.HitAt3.Should().BeGreaterThanOrEqualTo(0.62);
        hybrid.MrrAt10.Should().BeGreaterThanOrEqualTo(0.58);
        hybrid.HitAt3.Should().BeGreaterThanOrEqualTo(keyword.HitAt3 + 0.5);
        hybrid.ResultsForAbsent.Should().BeLessThanOrEqualTo(3, "a search for something nobody sells should show (almost) nothing");
        meaning.HitAt3.Should().BeGreaterThanOrEqualTo(hybrid.HitAt3, "the thresholds only remove listings, they never add one");
    }

    [SearchModelFact]
    public void On_the_dataset_catalog_the_new_search_finds_what_the_words_alone_do_not()
    {
        var (keyword, _, hybrid) = Evaluate(catalogs.Dataset, catalogs.DatasetVectors, catalogs.DatasetQueries, "listings.csv");

        keyword.HitAt3.Should().BeLessThan(0.15);
        hybrid.HitAt3.Should().BeGreaterThanOrEqualTo(0.68);
        hybrid.MrrAt10.Should().BeGreaterThanOrEqualTo(0.65);
        hybrid.PrecisionAt10.Should().BeGreaterThanOrEqualTo(0.42);
        hybrid.ResultsForAbsent.Should().BeLessThanOrEqualTo(6);
    }

    [SearchModelFact]
    public void A_search_with_the_words_of_the_title_still_finds_those_listings_first()
    {
        var demo = catalogs.Demo;
        var scores = SearchEvaluation.Scores(demo, catalogs.DemoVectors, catalogs.Embedder.EmbedQuery("laptop"));

        var results = SearchEvaluation.Hybrid(demo, "laptop", scores, Rules);
        var byWords = SearchEvaluation.Keyword(demo, "laptop");

        byWords.Should().NotBeEmpty();
        results.Take(byWords.Count).Should().Equal(byWords, "the matches by words come first, in the order they had before");
    }

    private (SearchMetrics Keyword, SearchMetrics Meaning, SearchMetrics Hybrid) Evaluate(
        IReadOnlyList<CatalogItem> catalog, float[][] vectors, IReadOnlyList<LabelledQuery> queries, string name)
    {
        var scores = queries.ToDictionary(q => q.Query, q => SearchEvaluation.Scores(catalog, vectors, catalogs.Embedder.EmbedQuery(q.Query)));

        var keyword = SearchEvaluation.Measure(catalog, queries, q => SearchEvaluation.Keyword(catalog, q.Query));
        var meaning = SearchEvaluation.Measure(catalog, queries, q => SearchEvaluation.MeaningOnly(scores[q.Query]));
        var hybrid = SearchEvaluation.Measure(catalog, queries, q => SearchEvaluation.Hybrid(catalog, q.Query, scores[q.Query], Rules));

        output.WriteLine($"{name}: {catalog.Count} listings, {queries.Count} searches");
        output.WriteLine($"  words only (before): {keyword}");
        output.WriteLine($"  meaning, no threshold: {meaning}");
        output.WriteLine($"  words + meaning (now): {hybrid}");
        foreach (var query in queries)
        {
            var found = SearchEvaluation.Hybrid(catalog, query.Query, scores[query.Query], Rules);
            var marks = string.Concat(found.Take(5).Select(id => SearchEvaluation.IsRelevant(catalog.First(c => c.Listing.Id == id), query) ? '+' : '-'));
            output.WriteLine($"    {query.Query,-40} {found.Count,2} {marks}");
        }
        return (keyword, meaning, hybrid);
    }

    private static double Dot(float[] a, float[] b) => a.Zip(b, (x, y) => (double)x * y).Sum();

    // The model and the vectors of both catalogs, made once for all the tests of the class
    // (~1000 listings, about half a minute on 4 threads).
    public sealed class Catalogs : IDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

        public Catalogs()
        {
            if (!GemmaTextEmbedder.FilesExist(SearchModelFactAttribute.ModelFolder))
                return;

            Embedder = new GemmaTextEmbedder(Options.Create(new SemanticSearchOptions
            {
                ModelPath = SearchModelFactAttribute.ModelFolder,
                Threads = 4
            }));

            var sellers = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
            Demo = DemoListings.BuildListings(sellers, Now)
                .Where(l => l.Status == ListingStatus.Active)
                .Select(l => new CatalogItem(l, l.Title))
                .ToList();
            Dataset = SearchEvaluation.DatasetCatalog(Now);
            DemoVectors = Demo.Select(c => Embedder.EmbedListing(c.Listing.Title, c.Listing.Description)).ToArray();
            DatasetVectors = Dataset.Select(c => Embedder.EmbedListing(c.Listing.Title, c.Listing.Description)).ToArray();
        }

        public GemmaTextEmbedder Embedder { get; } = null!;
        public IReadOnlyList<CatalogItem> Demo { get; } = [];
        public IReadOnlyList<CatalogItem> Dataset { get; } = [];
        public float[][] DemoVectors { get; } = [];
        public float[][] DatasetVectors { get; } = [];
        public IReadOnlyList<LabelledQuery> DemoQueries { get; } = SearchEvaluation.LoadQueries("demo-queries.json");
        public IReadOnlyList<LabelledQuery> DatasetQueries { get; } = SearchEvaluation.LoadQueries("dataset-queries.json");

        public void Dispose() => Embedder?.Dispose();
    }
}
