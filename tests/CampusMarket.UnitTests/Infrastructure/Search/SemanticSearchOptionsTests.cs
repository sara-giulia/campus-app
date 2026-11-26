using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Infrastructure.Search;

namespace CampusMarket.UnitTests.Infrastructure.Search;

public class SemanticSearchOptionsTests
{
    [Fact]
    public void Given_NewOptions_When_Reading_Then_HasTheDefaults()
    {
        var options = new SemanticSearchOptions();

        options.Enabled.Should().BeTrue();
        options.ModelPath.Should().Be(Path.Combine("ml-models", "semantic-search"));
        options.Threads.Should().Be(1);
        options.MinSimilarity.Should().Be(SemanticSelectionRules.Default.MinSimilarity);
        options.MaxDistanceFromBest.Should().Be(SemanticSelectionRules.Default.MaxDistanceFromBest);
        options.MaxResults.Should().Be(SemanticSelectionRules.Default.MaxResults);
    }

    [Fact]
    public void Given_SectionName_When_Reading_Then_IsSemanticSearch()
    {
        var name = SemanticSearchOptions.SectionName;

        name.Should().Be("SemanticSearch");
    }

    [Fact]
    public void Given_DefaultOptions_When_ReadingRules_Then_EqualsTheDefaultRules()
    {
        var options = new SemanticSearchOptions();

        var rules = options.Rules;

        rules.Should().Be(SemanticSelectionRules.Default);
    }

    [Fact]
    public void Given_ChangedNumbers_When_ReadingRules_Then_BuildsTheRecordFromThem()
    {
        var options = new SemanticSearchOptions { MinSimilarity = 0.5, MaxDistanceFromBest = 0.2, MaxResults = 3 };

        var rules = options.Rules;

        rules.Should().Be(new SemanticSelectionRules(0.5, 0.2, 3));
    }

    [Fact]
    public void Given_RelativeModelPath_When_ReadingFullModelPath_Then_IsUnderTheAppFolder()
    {
        var options = new SemanticSearchOptions { ModelPath = Path.Combine("models", "search") };

        var path = options.FullModelPath;

        path.Should().Be(Path.Combine(AppContext.BaseDirectory, "models", "search"));
    }

    [Fact]
    public void Given_AbsoluteModelPath_When_ReadingFullModelPath_Then_KeepsIt()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "semantic-search");
        var options = new SemanticSearchOptions { ModelPath = absolute };

        var path = options.FullModelPath;

        path.Should().Be(absolute);
    }
}
