using CampusMarket.Api.Common.Search;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;

namespace CampusMarket.UnitTests.Features.Listings.GetListings;

public class ListingSearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnknownId = new("99999999-9999-9999-9999-999999999999");

    private static Listing NewListing(string title, DateTimeOffset createdAt) =>
        Listing.Create(SellerId, title, Category.Electronics, Condition.Good, 50m, createdAt).Value;

    [Fact]
    public void Given_TitleWithoutDiacritics_When_SearchingWithDiacriticsAndOtherCase_Then_ReturnsItAsText()
    {
        var headphones = NewListing("Casti wireless", Now);
        Listing[] candidates = [headphones];

        var ranked = ListingSearch.Rank(candidates, "Căști", []);

        ranked.Should().ContainSingle();
        ranked[0].Listing.Should().BeSameAs(headphones);
        ranked[0].Match.Should().Be(SearchMatch.Text);
    }

    [Fact]
    public void Given_SearchWithSpacesAround_When_Ranking_Then_MatchesTheTrimmedText()
    {
        var laptop = NewListing("Laptop Dell", Now);
        Listing[] candidates = [laptop];

        var ranked = ListingSearch.Rank(candidates, "  dell  ", []);

        ranked.Select(r => r.Listing.Id).Should().Equal(laptop.Id);
    }

    [Fact]
    public void Given_SeveralTextMatches_When_Ranking_Then_ReturnsNewestFirst()
    {
        var oldest = NewListing("Laptop Dell", Now);
        var newest = NewListing("Laptop Lenovo", Now.AddHours(2));
        var middle = NewListing("Laptop HP", Now.AddHours(1));
        Listing[] candidates = [oldest, newest, middle];

        var ranked = ListingSearch.Rank(candidates, "laptop", []);

        ranked.Select(r => r.Listing.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public void Given_TextMatchesWithSameCreatedAt_When_Ranking_Then_OrdersById()
    {
        var first = NewListing("Laptop Dell", Now);
        var second = NewListing("Laptop HP", Now);
        Listing[] candidates = [first, second];

        var ranked = ListingSearch.Rank(candidates, "laptop", []);

        ranked.Select(r => r.Listing.Id).Should().BeInAscendingOrder();
        ranked.Should().HaveCount(2);
    }

    [Fact]
    public void Given_MeaningMatchThatIsAlsoTextMatch_When_Ranking_Then_ReturnsItOnceAsText()
    {
        var laptop = NewListing("Laptop Dell", Now);
        Listing[] candidates = [laptop];
        SemanticMatch[] meaning = [new(laptop.Id, 0.9)];

        var ranked = ListingSearch.Rank(candidates, "laptop", meaning);

        ranked.Should().ContainSingle();
        ranked[0].Listing.Should().BeSameAs(laptop);
        ranked[0].Match.Should().Be(SearchMatch.Text);
    }

    [Fact]
    public void Given_MeaningMatchNotAmongCandidates_When_Ranking_Then_DropsIt()
    {
        var calculator = NewListing("Calculator portabil", Now);
        Listing[] candidates = [calculator];
        SemanticMatch[] meaning = [new(UnknownId, 0.9), new(calculator.Id, 0.8)];

        var ranked = ListingSearch.Rank(candidates, "laptop", meaning);

        ranked.Select(r => r.Listing.Id).Should().Equal(calculator.Id);
        ranked[0].Match.Should().Be(SearchMatch.Meaning);
    }

    [Fact]
    public void Given_TextAndMeaningMatches_When_Ranking_Then_TextComesFirstAndMeaningKeepsTheGivenOrder()
    {
        var laptop = NewListing("Laptop Dell", Now);
        var notebook = NewListing("Notebook Asus", Now.AddHours(1));
        var calculator = NewListing("Calculator portabil", Now.AddHours(2));
        Listing[] candidates = [calculator, laptop, notebook];
        SemanticMatch[] meaning = [new(notebook.Id, 0.8), new(calculator.Id, 0.7)];

        var ranked = ListingSearch.Rank(candidates, "laptop", meaning);

        ranked.Select(r => r.Listing.Id).Should().Equal(laptop.Id, notebook.Id, calculator.Id);
        ranked.Select(r => r.Match).Should().Equal(SearchMatch.Text, SearchMatch.Meaning, SearchMatch.Meaning);
    }

    [Fact]
    public void Given_CandidatesAsSequence_When_Ranking_Then_ReturnsTheSameMatches()
    {
        var laptop = NewListing("Laptop Dell", Now);
        var chair = NewListing("Scaun birou", Now);
        Listing[] all = [laptop, chair];

        var ranked = ListingSearch.Rank(all.Where(l => l.Price.Amount > 0), "laptop", []);

        ranked.Select(r => r.Listing.Id).Should().Equal(laptop.Id);
    }

    [Fact]
    public void Given_NoTextAndNoMeaningMatch_When_Ranking_Then_ReturnsEmpty()
    {
        Listing[] candidates = [NewListing("Scaun birou", Now)];

        var ranked = ListingSearch.Rank(candidates, "laptop", []);

        ranked.Should().BeEmpty();
    }
}
