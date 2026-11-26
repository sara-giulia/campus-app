using CampusMarket.Api.Common.Search;

namespace CampusMarket.Api.Infrastructure.Search;

// Section "SemanticSearch". Enabled: load the model when its files are there (off in the integration
// tests, which use a fake embedder). ModelPath: relative to the API folder. Threads: ONNX Runtime threads
// for one text (1 on Render, which gives 0.1 CPU). The three selection numbers: see SemanticSelectionRules.
public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";

    public bool Enabled { get; set; } = true;
    public string ModelPath { get; set; } = Path.Combine("ml-models", "semantic-search");
    public int Threads { get; set; } = 1;

    public double MinSimilarity { get; set; } = SemanticSelectionRules.Default.MinSimilarity;
    public double MaxDistanceFromBest { get; set; } = SemanticSelectionRules.Default.MaxDistanceFromBest;
    public int MaxResults { get; set; } = SemanticSelectionRules.Default.MaxResults;

    public SemanticSelectionRules Rules => new(MinSimilarity, MaxDistanceFromBest, MaxResults);

    public string FullModelPath => Path.Combine(AppContext.BaseDirectory, ModelPath);
}
