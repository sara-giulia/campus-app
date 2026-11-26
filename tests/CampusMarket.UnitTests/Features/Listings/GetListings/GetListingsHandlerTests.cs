using CampusMarket.Api.Common.Search;
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        first.Publish(Now.AddDays(30));
        second.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        active.Publish(Now.AddDays(30));
        var reserved = Listing.Create(sellerId, "Reserved book", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        reserved.Publish(Now.AddDays(30));
        reserved.Reserve(buyerId, Now.AddHours(3));
        var repository = new FakeListingRepository();
        repository.Items[draft.Id] = draft;
        repository.Items[active.Id] = active;
        repository.Items[reserved.Id] = reserved;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(MinPrice: 25m, MaxPrice: 25m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OnlyMinPriceSet_When_Handling_Then_Succeeds()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(MinPrice: 10m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OnlyMaxPriceSet_When_Handling_Then_Succeeds()
    {
        var repository = new FakeListingRepository();
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(MaxPrice: 100m);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_CategoryFilter_When_Handling_Then_ReturnsOnlyThatCategory()
    {
        var textbook = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        textbook.Publish(Now.AddDays(30));
        var electronics = Listing.Create(Guid.NewGuid(), "Calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(1)).Value;
        electronics.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[textbook.Id] = textbook;
        repository.Items[electronics.Id] = electronics;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        below.Publish(Now.AddDays(30));
        var atLimit = Listing.Create(Guid.NewGuid(), "Limit book", Category.Textbook, Condition.Good, 10m, Now.AddHours(1)).Value;
        atLimit.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[below.Id] = below;
        repository.Items[atLimit.Id] = atLimit;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        atLimit.Publish(Now.AddDays(30));
        var above = Listing.Create(Guid.NewGuid(), "Pricey book", Category.Textbook, Condition.Good, 51m, Now.AddHours(1)).Value;
        above.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[atLimit.Id] = atLimit;
        repository.Items[above.Id] = above;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        listing.Publish(Now.AddDays(30));
        var other = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 15m, Now.AddHours(1)).Value;
        other.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        repository.Items[other.Id] = other;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        listing.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        first.Publish(Now.AddDays(30));
        var second = Listing.Create(Guid.NewGuid(), "Lab coat", Category.LabEquipment, Condition.New, 60m, Now.AddHours(1)).Value;
        second.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        listing.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        textbookMatch.Publish(Now.AddDays(30));
        var textbookNoMatch = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        textbookNoMatch.Publish(Now.AddDays(30));
        var electronicsMatch = Listing.Create(Guid.NewGuid(), "OOP calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(2)).Value;
        electronicsMatch.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[textbookMatch.Id] = textbookMatch;
        repository.Items[textbookNoMatch.Id] = textbookNoMatch;
        repository.Items[electronicsMatch.Id] = electronicsMatch;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        oldest.Publish(Now.AddDays(30));
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish(Now.AddDays(30));
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        first.Publish(Now.AddDays(30));
        var second = Listing.Create(Guid.NewGuid(), "Second", Category.Textbook, Condition.Good, 20m, Now).Value;
        second.Publish(Now.AddDays(30));
        var third = Listing.Create(Guid.NewGuid(), "Third", Category.Textbook, Condition.Good, 30m, Now).Value;
        third.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[first.Id] = first;
        repository.Items[second.Id] = second;
        repository.Items[third.Id] = third;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        oldest.Publish(Now.AddDays(30));
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish(Now.AddDays(30));
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        oldest.Publish(Now.AddDays(30));
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish(Now.AddDays(30));
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        oldest.Publish(Now.AddDays(30));
        var middle = Listing.Create(Guid.NewGuid(), "Middle", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        middle.Publish(Now.AddDays(30));
        var newest = Listing.Create(Guid.NewGuid(), "Newest", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        newest.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[oldest.Id] = oldest;
        repository.Items[middle.Id] = middle;
        repository.Items[newest.Id] = newest;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
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
        textbookOne.Publish(Now.AddDays(30));
        var textbookTwo = Listing.Create(Guid.NewGuid(), "Textbook two", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        textbookTwo.Publish(Now.AddDays(30));
        var textbookThree = Listing.Create(Guid.NewGuid(), "Textbook three", Category.Textbook, Condition.Good, 30m, Now.AddHours(2)).Value;
        textbookThree.Publish(Now.AddDays(30));
        var electronics = Listing.Create(Guid.NewGuid(), "Calculator", Category.Electronics, Condition.New, 40m, Now.AddHours(3)).Value;
        electronics.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[textbookOne.Id] = textbookOne;
        repository.Items[textbookTwo.Id] = textbookTwo;
        repository.Items[textbookThree.Id] = textbookThree;
        repository.Items[electronics.Id] = electronics;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Category: Category.Textbook, Page: 1, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        repository.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task Given_ListingsWithDifferentConditions_When_FilteringByCondition_Then_ReturnsOnlyThatCondition()
    {
        var newBook = Listing.Create(Guid.NewGuid(), "New book", Category.Textbook, Condition.New, 10m, Now).Value;
        newBook.Publish(Now.AddDays(30));
        var goodBook = Listing.Create(Guid.NewGuid(), "Good book", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        goodBook.Publish(Now.AddDays(30));
        var wornBook = Listing.Create(Guid.NewGuid(), "Worn book", Category.Textbook, Condition.Worn, 30m, Now.AddHours(2)).Value;
        wornBook.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[newBook.Id] = newBook;
        repository.Items[goodBook.Id] = goodBook;
        repository.Items[wornBook.Id] = wornBook;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Condition: Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(goodBook.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_CategoryAndConditionTogether_When_Handling_Then_ReturnsOnlyMatchesBoth()
    {
        var goodTextbook = Listing.Create(Guid.NewGuid(), "Good textbook", Category.Textbook, Condition.Good, 10m, Now).Value;
        goodTextbook.Publish(Now.AddDays(30));
        var wornTextbook = Listing.Create(Guid.NewGuid(), "Worn textbook", Category.Textbook, Condition.Worn, 20m, Now.AddHours(1)).Value;
        wornTextbook.Publish(Now.AddDays(30));
        var goodCalculator = Listing.Create(Guid.NewGuid(), "Good calculator", Category.Electronics, Condition.Good, 30m, Now.AddHours(2)).Value;
        goodCalculator.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[goodTextbook.Id] = goodTextbook;
        repository.Items[wornTextbook.Id] = wornTextbook;
        repository.Items[goodCalculator.Id] = goodCalculator;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Category: Category.Textbook, Condition: Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(goodTextbook.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_NoConditionFilter_When_Handling_Then_ReturnsEveryActiveListing()
    {
        var newBook = Listing.Create(Guid.NewGuid(), "New book", Category.Textbook, Condition.New, 10m, Now).Value;
        newBook.Publish(Now.AddDays(30));
        var goodBook = Listing.Create(Guid.NewGuid(), "Good book", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value;
        goodBook.Publish(Now.AddDays(30));
        var wornBook = Listing.Create(Guid.NewGuid(), "Worn book", Category.Textbook, Condition.Worn, 30m, Now.AddHours(2)).Value;
        wornBook.Publish(Now.AddDays(30));
        var repository = new FakeListingRepository();
        repository.Items[newBook.Id] = newBook;
        repository.Items[goodBook.Id] = goodBook;
        repository.Items[wornBook.Id] = wornBook;
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(wornBook.Id, goodBook.Id, newBook.Id);
        result.Value.TotalCount.Should().Be(3);
        repository.Items.Should().HaveCount(3);
    }

    private static FakeListingRepository RepositoryWithActiveListings(params Listing[] listings)
    {
        var repository = new FakeListingRepository();
        foreach (var listing in listings)
        {
            listing.Publish(Now.AddDays(30));
            repository.Items[listing.Id] = listing;
        }

        return repository;
    }

    [Fact]
    public async Task Given_ListingsPriced30_10_20_When_SortingByPriceAscending_Then_ReturnsCheapestFirst()
    {
        var repository = RepositoryWithActiveListings(
            Listing.Create(Guid.NewGuid(), "Book A", Category.Textbook, Condition.Good, 30m, Now).Value,
            Listing.Create(Guid.NewGuid(), "Book B", Category.Textbook, Condition.Good, 10m, Now.AddHours(1)).Value,
            Listing.Create(Guid.NewGuid(), "Book C", Category.Textbook, Condition.Good, 20m, Now.AddHours(2)).Value);
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Sort: "price_asc");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Price).Should().Equal(10m, 20m, 30m);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ListingsPriced30_10_20_When_SortingByPriceDescending_Then_ReturnsMostExpensiveFirst()
    {
        var repository = RepositoryWithActiveListings(
            Listing.Create(Guid.NewGuid(), "Book A", Category.Textbook, Condition.Good, 30m, Now).Value,
            Listing.Create(Guid.NewGuid(), "Book B", Category.Textbook, Condition.Good, 10m, Now.AddHours(1)).Value,
            Listing.Create(Guid.NewGuid(), "Book C", Category.Textbook, Condition.Good, 20m, Now.AddHours(2)).Value);
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Sort: "price_desc");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Price).Should().Equal(30m, 20m, 10m);
        repository.Items.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("newest")]
    [InlineData(null)]
    public async Task Given_NewestOrNoSort_When_Handling_Then_ReturnsNewestCreatedAtFirst(string? sort)
    {
        var oldest = Listing.Create(Guid.NewGuid(), "Book A", Category.Textbook, Condition.Good, 10m, Now).Value;
        var middle = Listing.Create(Guid.NewGuid(), "Book B", Category.Textbook, Condition.Good, 30m, Now.AddHours(1)).Value;
        var newest = Listing.Create(Guid.NewGuid(), "Book C", Category.Textbook, Condition.Good, 20m, Now.AddHours(2)).Value;
        var repository = RepositoryWithActiveListings(oldest, middle, newest);
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Sort: sort);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    public async Task Given_TwoListingsWithSamePrice_When_SortingByPrice_Then_OrdersByIdAscending(string sort)
    {
        var repository = RepositoryWithActiveListings(
            Listing.Create(Guid.NewGuid(), "Book A", Category.Textbook, Condition.Good, 20m, Now).Value,
            Listing.Create(Guid.NewGuid(), "Book B", Category.Textbook, Condition.Good, 20m, Now.AddHours(1)).Value);
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Sort: sort);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.Id).Should().BeInAscendingOrder();
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_UnknownSort_When_Handling_Then_ReturnsValidationErrorAndLeavesRepositoryUnchanged()
    {
        var listing = Listing.Create(Guid.NewGuid(), "Book A", Category.Textbook, Condition.Good, 20m, Now).Value;
        var repository = RepositoryWithActiveListings(listing);
        var handler = new GetListingsHandler(repository, new FakeSemanticSearch());
        var query = new GetListingsQuery(Sort: "abc");

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Listings.InvalidSort");
        result.Error.Message.Should().Be("Sort must be relevance, newest, price_asc or price_desc.");
        repository.Items.Should().HaveCount(1);
    }

    private static readonly Guid SearchSellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SearchBuyerId = new("22222222-2222-2222-2222-222222222222");

    private static Listing ActiveListing(
        string title, DateTimeOffset createdAt, decimal price = 50m,
        Category category = Category.Electronics, Condition condition = Condition.Good)
    {
        var listing = Listing.Create(SearchSellerId, title, category, condition, price, createdAt).Value;
        listing.Publish(createdAt.AddDays(30));
        return listing;
    }

    private static FakeListingRepository RepositoryWith(params Listing[] listings)
    {
        var repository = new FakeListingRepository();
        foreach (var listing in listings)
            repository.Items[listing.Id] = listing;

        return repository;
    }

    [Fact]
    public async Task Given_SearchOfTwoCharacters_When_Handling_Then_DoesNotAskTheSemanticSearchButFindsByTitle()
    {
        var pc = ActiveListing("PC gaming", Now);
        var chair = ActiveListing("Scaun birou", Now);
        var repository = RepositoryWith(pc, chair);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.Add(new SemanticMatch(chair.Id, 0.9));
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "pc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(pc.Id);
        result.Value.Items[0].Match.Should().Be(SearchMatch.Text);
        semanticSearch.Queries.Should().BeEmpty();
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_SearchOfThreeCharactersWithSpaces_When_Handling_Then_AsksTheSemanticSearchOnceWithTheTrimmedSearch()
    {
        var repository = RepositoryWith(ActiveListing("Laptop Dell", Now));
        var semanticSearch = new FakeSemanticSearch();
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "  pc1  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        semanticSearch.Queries.Should().Equal("pc1");
        repository.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ListingFoundByMeaning_When_Handling_Then_ComesAfterTheTitleMatchesWithMatchMeaning()
    {
        var laptop = ActiveListing("Laptop Dell", Now);
        var calculator = ActiveListing("Calculator portabil", Now.AddHours(5));
        var repository = RepositoryWith(calculator, laptop);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.Add(new SemanticMatch(calculator.Id, 0.8));
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(laptop.Id, calculator.Id);
        result.Value.Items.Select(i => i.Match).Should().Equal(SearchMatch.Text, SearchMatch.Meaning);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_MeaningMatchesThatAreNotActive_When_Handling_Then_ReturnsNoneOfThem()
    {
        var draft = Listing.Create(SearchSellerId, "Calculator portabil", Category.Electronics, Condition.Good, 50m, Now).Value;
        var hidden = ActiveListing("Notebook Asus", Now);
        hidden.Hide();
        var reserved = ActiveListing("Ultrabook HP", Now);
        reserved.Reserve(SearchBuyerId, Now);
        var repository = RepositoryWith(draft, hidden, reserved);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange([new(draft.Id, 0.9), new(hidden.Id, 0.8), new(reserved.Id, 0.7)]);
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_MeaningMatchesOutsideTheFilters_When_Handling_Then_ReturnsOnlyTheOneInsideThem()
    {
        var inside = ActiveListing("Calculator portabil", Now, 30m, Category.Electronics, Condition.Good);
        var otherCategory = ActiveListing("Manual de programare", Now, 30m, Category.Textbook, Condition.Good);
        var otherCondition = ActiveListing("Notebook Asus", Now, 30m, Category.Electronics, Condition.New);
        var tooCheap = ActiveListing("Tableta veche", Now, 5m, Category.Electronics, Condition.Good);
        var tooExpensive = ActiveListing("Ultrabook HP", Now, 100m, Category.Electronics, Condition.Good);
        var repository = RepositoryWith(inside, otherCategory, otherCondition, tooCheap, tooExpensive);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange(
        [
            new(otherCategory.Id, 0.9), new(otherCondition.Id, 0.85), new(tooCheap.Id, 0.8),
            new(tooExpensive.Id, 0.75), new(inside.Id, 0.7)
        ]);
        var handler = new GetListingsHandler(repository, semanticSearch);
        var query = new GetListingsQuery(
            Category: Category.Electronics, MinPrice: 10m, MaxPrice: 50m, Search: "laptop", Condition: Condition.Good);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(inside.Id);
        result.Value.TotalCount.Should().Be(1);
        repository.Items.Should().HaveCount(5);
    }

    [Theory]
    [InlineData("relevance")]
    [InlineData(null)]
    public async Task Given_RelevanceOrNoSort_When_Searching_Then_KeepsTextMatchesNewestFirstThenMeaningInGivenOrder(string? sort)
    {
        var olderText = ActiveListing("Laptop Dell", Now);
        var newerText = ActiveListing("Laptop HP", Now.AddHours(1));
        var firstMeaning = ActiveListing("Calculator portabil", Now.AddHours(2));
        var secondMeaning = ActiveListing("Notebook Asus", Now.AddHours(3));
        var repository = RepositoryWith(olderText, secondMeaning, newerText, firstMeaning);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange([new(firstMeaning.Id, 0.8), new(secondMeaning.Id, 0.7)]);
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop", Sort: sort), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newerText.Id, olderText.Id, firstMeaning.Id, secondMeaning.Id);
        repository.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task Given_SortNewest_When_Searching_Then_OrdersTextAndMeaningMatchesTogetherByCreatedAt()
    {
        var olderText = ActiveListing("Laptop Dell", Now);
        var newestMeaning = ActiveListing("Calculator portabil", Now.AddHours(2));
        var newerText = ActiveListing("Laptop HP", Now.AddHours(1));
        var repository = RepositoryWith(olderText, newestMeaning, newerText);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.Add(new SemanticMatch(newestMeaning.Id, 0.8));
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop", Sort: "newest"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newestMeaning.Id, newerText.Id, olderText.Id);
        result.Value.Items.Select(i => i.Match).Should().Equal(SearchMatch.Meaning, SearchMatch.Text, SearchMatch.Text);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_SortPriceAscending_When_Searching_Then_OrdersTextAndMeaningMatchesTogetherByPrice()
    {
        var text = ActiveListing("Laptop Dell", Now, 40m);
        var cheapMeaning = ActiveListing("Calculator portabil", Now, 10m);
        var expensiveMeaning = ActiveListing("Notebook Asus", Now, 70m);
        var repository = RepositoryWith(text, cheapMeaning, expensiveMeaning);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange([new(expensiveMeaning.Id, 0.8), new(cheapMeaning.Id, 0.7)]);
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop", Sort: "price_asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(cheapMeaning.Id, text.Id, expensiveMeaning.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_SortPriceDescending_When_Searching_Then_OrdersTextAndMeaningMatchesTogetherByPrice()
    {
        var text = ActiveListing("Laptop Dell", Now, 40m);
        var cheapMeaning = ActiveListing("Calculator portabil", Now, 10m);
        var expensiveMeaning = ActiveListing("Notebook Asus", Now, 70m);
        var repository = RepositoryWith(text, cheapMeaning, expensiveMeaning);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange([new(cheapMeaning.Id, 0.8), new(expensiveMeaning.Id, 0.7)]);
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: "laptop", Sort: "price_desc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(expensiveMeaning.Id, text.Id, cheapMeaning.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_TextAndMeaningMatches_When_RequestingSecondPage_Then_TotalCountCountsBothAndPagingComesAfterTheOrder()
    {
        var olderText = ActiveListing("Laptop Dell", Now);
        var newerText = ActiveListing("Laptop HP", Now.AddHours(1));
        var firstMeaning = ActiveListing("Calculator portabil", Now);
        var secondMeaning = ActiveListing("Notebook Asus", Now);
        var repository = RepositoryWith(olderText, newerText, firstMeaning, secondMeaning);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.AddRange([new(firstMeaning.Id, 0.8), new(secondMeaning.Id, 0.7)]);
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(
            new GetListingsQuery(Search: "laptop", Page: 2, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(firstMeaning.Id, secondMeaning.Id);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(4);
        repository.Items.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_NoSearch_When_Handling_Then_DoesNotAskTheSemanticSearchAndEveryMatchIsNull(string? search)
    {
        var older = ActiveListing("Laptop Dell", Now);
        var newer = ActiveListing("Scaun birou", Now.AddHours(1));
        var repository = RepositoryWith(older, newer);
        var semanticSearch = new FakeSemanticSearch();
        semanticSearch.Matches.Add(new SemanticMatch(older.Id, 0.9));
        var handler = new GetListingsHandler(repository, semanticSearch);

        var result = await handler.HandleAsync(new GetListingsQuery(Search: search), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
        result.Value.Items.Should().OnlyContain(i => i.Match == null);
        semanticSearch.Queries.Should().BeEmpty();
        repository.Items.Should().HaveCount(2);
    }
}
