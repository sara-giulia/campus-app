using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListings;

public class GetListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_EmptyRepository_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_RepositoryWithTwoListings_When_Handling_Then_ReturnsThemAndLeavesRepositoryUnchanged()
    {
        var first = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        var second = Listing.Create(Guid.NewGuid(), "Lab coat", Category.LabEquipment, Condition.New, 60m, Now).Value;
        first.Publish();
        second.Publish();
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        repository.Items.Should().HaveCount(2);
        repository.Items.Should().ContainKey(first.Id);
        repository.Items.Should().ContainKey(second.Id);
    }

    [Fact]
    public async Task Given_DraftActiveAndReservedListings_When_Handling_Then_ReturnsOnlyActive()
    {
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var draft = Listing.Create(sellerId, "Draft book", Category.Textbook, Condition.Good, 10m, Now).Value;
        var active = Listing.Create(sellerId, "Active book", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        active.Publish();
        var reserved = Listing.Create(sellerId, "Reserved book", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        reserved.Publish();
        reserved.Reserve(buyerId, Now.AddHours(3));
        var repository = new FakeListingRepository();
        repository.Items[draft.Id] = draft;
        repository.Items[active.Id] = active;
        repository.Items[reserved.Id] = reserved;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(active.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Given_InvalidPage_When_Handling_Then_ReturnsValidationError(int page)
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Page: page);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listings.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        repository.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Given_InvalidPageSize_When_Handling_Then_ReturnsValidationError(int pageSize)
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(PageSize: pageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listings.InvalidPageSize");
        result.Error.Message.Should().Be("Page size must be between 1 and 100.");
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MinPriceGreaterThanMaxPrice_When_Handling_Then_ReturnsValidationError()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MinPrice: 50m, MaxPrice: 20m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listings.InvalidPriceRange");
        result.Error.Message.Should().Be("Minimum price cannot be greater than maximum price.");
        repository.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Given_PageSizeAtLimit_When_Handling_Then_Succeeds(int pageSize)
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(PageSize: pageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(pageSize);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MinPriceEqualToMaxPrice_When_Handling_Then_Succeeds()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MinPrice: 25m, MaxPrice: 25m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OnlyMinPriceSet_When_Handling_Then_Succeeds()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MinPrice: 10m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OnlyMaxPriceSet_When_Handling_Then_Succeeds()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MaxPrice: 100m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_CategoryFilter_When_Handling_Then_ReturnsOnlyThatCategory()
    {
        var textbook = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        textbook.Publish();
        var electronics = Listing.Create(Guid.NewGuid(), "Calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(1)).Value;
        electronics.Publish();
        var repository = new FakeListingRepository();
        repository.Items[textbook.Id] = textbook;
        repository.Items[electronics.Id] = electronics;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Category: Category.Textbook);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(textbook.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_MinPriceFilter_When_Handling_Then_IsInclusive()
    {
        var below = Listing.Create(Guid.NewGuid(), "Cheap book", Category.Textbook, Condition.Good, 9m, Now).Value;
        below.Publish();
        var atLimit = Listing.Create(Guid.NewGuid(), "Limit book", Category.Textbook, Condition.Good, 10m, Now.AddHours(1)).Value;
        atLimit.Publish();
        var repository = new FakeListingRepository();
        repository.Items[below.Id] = below;
        repository.Items[atLimit.Id] = atLimit;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MinPrice: 10m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(atLimit.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_MaxPriceFilter_When_Handling_Then_IsInclusive()
    {
        var atLimit = Listing.Create(Guid.NewGuid(), "Limit book", Category.Textbook, Condition.Good, 50m, Now).Value;
        atLimit.Publish();
        var above = Listing.Create(Guid.NewGuid(), "Pricey book", Category.Textbook, Condition.Good, 51m, Now.AddHours(1)).Value;
        above.Publish();
        var repository = new FakeListingRepository();
        repository.Items[atLimit.Id] = atLimit;
        repository.Items[above.Id] = above;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(MaxPrice: 50m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(atLimit.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SearchPartialAndDifferentCase_When_Handling_Then_Matches()
    {
        var listing = Listing.Create(Guid.NewGuid(), "OOP textbook", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        var other = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 15m, Now.AddHours(1)).Value;
        other.Publish();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        repository.Items[other.Id] = other;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Search: "oop");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(listing.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SearchWithSurroundingSpaces_When_Handling_Then_StillMatches()
    {
        var listing = Listing.Create(Guid.NewGuid(), "OOP textbook", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Search: "  oop  ");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(listing.Id);
        repository.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_SearchEmptyOrWhitespace_When_Handling_Then_DoesNotFilter(string search)
    {
        var first = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        first.Publish();
        var second = Listing.Create(Guid.NewGuid(), "Lab coat", Category.LabEquipment, Condition.New, 60m, Now.AddHours(1)).Value;
        second.Publish();
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Search: search);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SearchMatchesNothing_When_Handling_Then_ReturnsEmpty()
    {
        var listing = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Search: "zzz");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        repository.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_CategoryAndSearchTogether_When_Handling_Then_ReturnsOnlyMatchesBoth()
    {
        var textbookMatch = Listing.Create(Guid.NewGuid(), "OOP textbook", Category.Textbook, Condition.Good, 25m, Now).Value;
        textbookMatch.Publish();
        var textbookNoMatch = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        textbookNoMatch.Publish();
        var electronicsMatch = Listing.Create(Guid.NewGuid(), "OOP calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(2)).Value;
        electronicsMatch.Publish();
        var repository = new FakeListingRepository();
        repository.Items[textbookMatch.Id] = textbookMatch;
        repository.Items[textbookNoMatch.Id] = textbookNoMatch;
        repository.Items[electronicsMatch.Id] = electronicsMatch;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Category: Category.Textbook, Search: "oop");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(textbookMatch.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ListingsWithDifferentCreatedAt_When_Handling_Then_OrdersNewestFirst()
    {
        var oldest = Listing.Create(Guid.NewGuid(), "Oldest", Category.Textbook, Condition.Good, 10m, Now).Value;
        oldest.Publish();
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish();
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish();
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ListingsWithSameCreatedAt_When_Handling_Then_OrdersByIdAscending()
    {
        var first = Listing.Create(Guid.NewGuid(), "First", Category.Textbook, Condition.Good, 10m, Now).Value;
        first.Publish();
        var second = Listing.Create(Guid.NewGuid(), "Second", Category.Textbook, Condition.Good, 20m, Now).Value;
        second.Publish();
        var third = Listing.Create(Guid.NewGuid(), "Third", Category.Textbook, Condition.Good, 30m, Now).Value;
        third.Publish();
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        repository.Items[third.Id] = third;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().BeInAscendingOrder();
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_PageOneSizeTwo_Then_ReturnsTwoNewest()
    {
        var oldest = Listing.Create(Guid.NewGuid(), "Oldest", Category.Textbook, Condition.Good, 10m, Now).Value;
        oldest.Publish();
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish();
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish();
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Page: 1, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_PageTwoSizeTwo_Then_ReturnsOldest()
    {
        var oldest = Listing.Create(Guid.NewGuid(), "Oldest", Category.Textbook, Condition.Good, 10m, Now).Value;
        oldest.Publish();
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish();
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish();
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Page: 2, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items.Single().Id.Should().Be(oldest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ThreeListings_When_PageBeyondEnd_Then_ReturnsEmptyItemsButKeepsTotalCount()
    {
        var oldest = Listing.Create(Guid.NewGuid(), "Oldest", Category.Textbook, Condition.Good, 10m, Now).Value;
        oldest.Publish();
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish();
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish();
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Page: 5, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(3);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_DefaultQuery_When_Handling_Then_UsesDefaultPageAndPageSize()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_FilterAndPagingTogether_When_Handling_Then_TotalCountReflectsFilter()
    {
        var textbookOne = Listing.Create(Guid.NewGuid(), "Textbook one", Category.Textbook, Condition.Good, 10m, Now).Value;
        textbookOne.Publish();
        var textbookTwo = Listing.Create(Guid.NewGuid(), "Textbook two", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        textbookTwo.Publish();
        var textbookThree = Listing.Create(Guid.NewGuid(), "Textbook three", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        textbookThree.Publish();
        var electronics = Listing.Create(Guid.NewGuid(), "Calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(3)).Value;
        electronics.Publish();
        var repository = new FakeListingRepository();
        repository.Items[textbookOne.Id] = textbookOne;
        repository.Items[textbookTwo.Id] = textbookTwo;
        repository.Items[textbookThree.Id] = textbookThree;
        repository.Items[electronics.Id] = electronics;
        var handler = new GetListingsHandler(repository);
        var query = new GetListingsQuery(Category: Category.Textbook, Page: 1, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        repository.Items.Should().HaveCount(4);
    }
}
