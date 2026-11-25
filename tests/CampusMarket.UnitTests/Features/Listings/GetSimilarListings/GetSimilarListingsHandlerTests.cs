using CampusMarket.Api.Common.Similarity;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetSimilarListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetSimilarListings;

public class GetSimilarListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");

    private static Listing BuildDraftListing(
        string title,
        DateTimeOffset createdAt,
        string? description = null,
        Category category = Category.Textbook,
        Condition condition = Condition.Good,
        decimal price = 25m)
    {
        return Listing.Create(SellerId, title, category, condition, price, createdAt, description).Value;
    }

    private static Listing BuildActiveListing(
        string title,
        DateTimeOffset createdAt,
        string? description = null,
        Category category = Category.Textbook,
        Condition condition = Condition.Good,
        decimal price = 25m)
    {
        var listing = BuildDraftListing(title, createdAt, description, category, condition, price);
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Listing BuildReservedListing(string title)
    {
        var listing = BuildActiveListing(title, Now);
        listing.Reserve(BuyerId, Now);
        return listing;
    }

    private static Listing BuildSoldListing(string title)
    {
        var listing = BuildActiveListing(title, Now);
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    private static Listing BuildHiddenListing(string title)
    {
        var listing = BuildActiveListing(title, Now);
        listing.Hide();
        return listing;
    }

    [Fact]
    public async Task Given_UnknownId_When_Handling_Then_ReturnsNotFoundAndDoesNotCallFinder()
    {
        var repository = new FakeListingRepository();
        var finder = new FakeSimilarListingsFinder();
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());
        var id = Guid.NewGuid();

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{id}' was not found.");
        finder.CallCount.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TargetIsTheOnlyActiveListing_When_Handling_Then_ReturnsEmptyListAndDoesNotCallFinder()
    {
        var target = BuildActiveListing("Algebra book", Now);
        var draft = BuildDraftListing("Draft book", Now);
        var reserved = BuildReservedListing("Reserved book");
        var sold = BuildSoldListing("Sold book");
        var hidden = BuildHiddenListing("Hidden book");
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[draft.Id] = draft;
        repository.Items[reserved.Id] = reserved;
        repository.Items[sold.Id] = sold;
        repository.Items[hidden.Id] = hidden;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        finder.CallCount.Should().Be(0);
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftTargetAndThreeActiveListings_When_Handling_Then_FinderGetsTargetTextAndOnlyActiveCandidates()
    {
        var target = BuildDraftListing("Algebra book", Now, "Linear algebra, first year");
        var first = BuildActiveListing("Calculus book", Now, "Second edition");
        var second = BuildActiveListing("Physics notes", Now.AddHours(1), "Mechanics");
        var third = BuildActiveListing("Casio calculator", Now.AddHours(2));
        var otherDraft = BuildDraftListing("Draft book", Now);
        var reserved = BuildReservedListing("Reserved book");
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        repository.Items[third.Id] = third;
        repository.Items[otherDraft.Id] = otherDraft;
        repository.Items[reserved.Id] = reserved;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo([first.Id, second.Id, third.Id]);
        finder.CallCount.Should().Be(1);
        finder.LastTarget.Should().Be(new ListingText("Algebra book", "Linear algebra, first year"));
        finder.LastCandidates.Should().BeEquivalentTo(
        [
            new ListingText("Calculus book", "Second edition"),
            new ListingText("Physics notes", "Mechanics"),
            new ListingText("Casio calculator", null)
        ]);
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SevenActiveCandidates_When_Handling_Then_ReturnsFiveMostSimilarHighestFirst()
    {
        var target = BuildActiveListing("Algebra book", Now);
        var c1 = BuildActiveListing("Candidate 1", Now);
        var c2 = BuildActiveListing("Candidate 2", Now);
        var c3 = BuildActiveListing("Candidate 3", Now);
        var c4 = BuildActiveListing("Candidate 4", Now);
        var c5 = BuildActiveListing("Candidate 5", Now);
        var c6 = BuildActiveListing("Candidate 6", Now);
        var c7 = BuildActiveListing("Candidate 7", Now);
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[c1.Id] = c1;
        repository.Items[c2.Id] = c2;
        repository.Items[c3.Id] = c3;
        repository.Items[c4.Id] = c4;
        repository.Items[c5.Id] = c5;
        repository.Items[c6.Id] = c6;
        repository.Items[c7.Id] = c7;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        finder.SimilarityByTitle["Candidate 1"] = 0.3;
        finder.SimilarityByTitle["Candidate 2"] = 0.9;
        finder.SimilarityByTitle["Candidate 3"] = 0.1;
        finder.SimilarityByTitle["Candidate 4"] = 0.7;
        finder.SimilarityByTitle["Candidate 5"] = 0.5;
        finder.SimilarityByTitle["Candidate 6"] = 0.2;
        finder.SimilarityByTitle["Candidate 7"] = 0.8;
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(GetSimilarListingsQuery.MaxResults);
        result.Value.Items.Select(i => i.Id).Should().Equal(c2.Id, c7.Id, c4.Id, c5.Id, c1.Id);
        result.Value.Items.Select(i => i.Similarity).Should().Equal(0.9, 0.8, 0.7, 0.5, 0.3);
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SameSimilarityAndDifferentCreatedAt_When_Handling_Then_ReturnsNewerFirst()
    {
        var target = BuildActiveListing("Algebra book", Now);
        var older = BuildActiveListing("Older book", Now.AddHours(-2));
        var newer = BuildActiveListing("Newer book", Now.AddHours(-1));
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[older.Id] = older;
        repository.Items[newer.Id] = newer;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        finder.SimilarityByTitle["Older book"] = 0.5;
        finder.SimilarityByTitle["Newer book"] = 0.5;
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SameSimilarityAndSameCreatedAt_When_Handling_Then_ReturnsSmallerIdFirst()
    {
        var target = BuildActiveListing("Algebra book", Now);
        var first = BuildActiveListing("First book", Now.AddHours(-1));
        var second = BuildActiveListing("Second book", Now.AddHours(-1));
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        finder.SimilarityByTitle["First book"] = 0.5;
        finder.SimilarityByTitle["Second book"] = 0.5;
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Id).Should().BeInAscendingOrder();
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnroundedSimilarities_When_Handling_Then_ItemsHaveTwoDecimalsAndCandidateDetails()
    {
        var target = BuildActiveListing("Algebra book", Now);
        var calculator = BuildActiveListing("Casio calculator", Now, category: Category.Electronics, condition: Condition.New, price: 80m);
        var notes = BuildActiveListing("Physics notes", Now, category: Category.Notes, condition: Condition.Worn, price: 10.5m);
        var repository = new FakeListingRepository();
        repository.Items[target.Id] = target;
        repository.Items[calculator.Id] = calculator;
        repository.Items[notes.Id] = notes;
        var before = repository.Items.Values.ToList();
        var finder = new FakeSimilarListingsFinder();
        finder.SimilarityByTitle["Casio calculator"] = 0.456;
        finder.SimilarityByTitle["Physics notes"] = 0.125;
        var handler = new GetSimilarListingsHandler(repository, finder, new FakeMemberBlockRepository());

        var result = await handler.HandleAsync(new GetSimilarListingsQuery(target.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().Equal(
            new SimilarListingItem(calculator.Id, "Casio calculator", Category.Electronics, Condition.New, 80m, 0.46),
            new SimilarListingItem(notes.Id, "Physics notes", Category.Notes, Condition.Worn, 10.5m, 0.13));
        repository.Items.Values.Should().Equal(before);
        repository.UpdateCount.Should().Be(0);
    }
}
