extern alias api;

using api::CampusMarket.Api.Infrastructure.Search;

namespace CampusMarket.ML.Tests.Support;

// A [Fact] that needs the search model (ml/models/semantic-search, not in git). Without the files the
// test is skipped with the command to run; the CI downloads the model first, so there it always runs.
public sealed class SearchModelFactAttribute : FactAttribute
{
    public static string ModelFolder { get; } = Path.Combine(TestPaths.MlDirectory, "models", "semantic-search");

    public SearchModelFactAttribute()
    {
        if (!GemmaTextEmbedder.FilesExist(ModelFolder))
            Skip = "The search model is missing: run ml/scripts/download-search-model.sh";
    }
}
