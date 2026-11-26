using CampusMarket.Api.Common.Search;

namespace CampusMarket.UnitTests.Common.Search;

public class SemanticMatchSelectionTests
{
    private static readonly Guid FirstId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdId = new("00000000-0000-0000-0000-000000000003");
    private static readonly SemanticSelectionRules Rules = new(0.5, 0.25, 10);

    [Fact]
    public void Given_DefaultRules_When_Reading_Then_HasTheEvaluatedNumbers()
    {
        var rules = SemanticSelectionRules.Default;

        rules.MinSimilarity.Should().Be(0.36);
        rules.MaxDistanceFromBest.Should().Be(0.15);
        rules.MaxResults.Should().Be(10);
    }

    [Fact]
    public void Given_MatchUnderMinimum_When_Selecting_Then_DropsIt()
    {
        SemanticMatch[] scored = [new(FirstId, 0.6), new(SecondId, 0.49)];

        var selected = SemanticMatchSelection.Select(scored, Rules);

        selected.Select(m => m.ListingId).Should().Equal(FirstId);
    }

    [Fact]
    public void Given_MatchExactlyAtMinimum_When_Selecting_Then_KeepsIt()
    {
        SemanticMatch[] scored = [new(FirstId, 0.5)];

        var selected = SemanticMatchSelection.Select(scored, Rules);

        selected.Select(m => m.ListingId).Should().Equal(FirstId);
    }

    [Fact]
    public void Given_UnorderedMatches_When_Selecting_Then_OrdersBySimilarityDescending()
    {
        SemanticMatch[] scored = [new(FirstId, 0.55), new(SecondId, 0.7), new(ThirdId, 0.6)];

        var selected = SemanticMatchSelection.Select(scored, Rules);

        selected.Select(m => m.ListingId).Should().Equal(SecondId, ThirdId, FirstId);
    }

    [Fact]
    public void Given_TwoEqualSimilarities_When_Selecting_Then_OrdersByListingId()
    {
        SemanticMatch[] scored = [new(ThirdId, 0.6), new(FirstId, 0.6), new(SecondId, 0.6)];

        var selected = SemanticMatchSelection.Select(scored, Rules);

        selected.Select(m => m.ListingId).Should().Equal(FirstId, SecondId, ThirdId);
    }

    [Fact]
    public void Given_MatchExactlyAtDistanceFromBest_When_Selecting_Then_KeepsIt()
    {
        var rules = new SemanticSelectionRules(0.1, 0.5, 10);
        SemanticMatch[] scored = [new(FirstId, 1.0), new(SecondId, 0.5)];

        var selected = SemanticMatchSelection.Select(scored, rules);

        selected.Select(m => m.ListingId).Should().Equal(FirstId, SecondId);
    }

    [Fact]
    public void Given_MatchJustBelowDistanceFromBest_When_Selecting_Then_DropsIt()
    {
        var rules = new SemanticSelectionRules(0.1, 0.5, 10);
        SemanticMatch[] scored = [new(FirstId, 1.0), new(SecondId, 0.49)];

        var selected = SemanticMatchSelection.Select(scored, rules);

        selected.Select(m => m.ListingId).Should().Equal(FirstId);
    }

    [Fact]
    public void Given_MoreMatchesThanMaxResults_When_Selecting_Then_KeepsTheBestAfterOrdering()
    {
        var rules = new SemanticSelectionRules(0.1, 1.0, 2);
        SemanticMatch[] scored = [new(FirstId, 0.6), new(SecondId, 0.8), new(ThirdId, 0.7)];

        var selected = SemanticMatchSelection.Select(scored, rules);

        selected.Select(m => m.ListingId).Should().Equal(SecondId, ThirdId);
    }

    [Fact]
    public void Given_AllMatchesUnderMinimum_When_Selecting_Then_ReturnsEmpty()
    {
        SemanticMatch[] scored = [new(FirstId, 0.2), new(SecondId, 0.1)];

        var selected = SemanticMatchSelection.Select(scored, Rules);

        selected.Should().BeEmpty();
    }

    [Fact]
    public void Given_NoMatches_When_Selecting_Then_ReturnsEmpty()
    {
        var selected = SemanticMatchSelection.Select([], Rules);

        selected.Should().BeEmpty();
    }
}
