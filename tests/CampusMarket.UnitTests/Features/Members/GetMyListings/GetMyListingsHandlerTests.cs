using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Members.GetMyListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetMyListings;

public class GetMyListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BuyerId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraft(Guid sellerId, string title, DateTimeOffset createdAt, decimal price = 25m) =>
        Listing.Create(sellerId, title, Category.Textbook, Condition.Good, price, createdAt).Value;

    private static Listing BuildActive(Guid sellerId, string title, DateTimeOffset createdAt)
    {
        var listing = BuildDraft(sellerId, title, createdAt);
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static Listing BuildReserved(Guid sellerId, string title, DateTimeOffset createdAt)
    {
        var listing = BuildActive(sellerId, title, createdAt);
        listing.Reserve(BuyerId, createdAt);
        return listing;
    }

    private static Listing BuildSold(Guid sellerId, string title, DateTimeOffset createdAt)
    {
        var listing = BuildActive(sellerId, title, createdAt);
        var reservation = listing.Reserve(BuyerId, createdAt).Value;
        listing.ConfirmSale(reservation.Id, createdAt);
        return listing;
    }

    private static Listing BuildHidden(Guid sellerId, string title, DateTimeOffset createdAt)
    {
        var listing = BuildActive(sellerId, title, createdAt);
        listing.Hide();
        return listing;
    }

    private static FakeListingRepository BuildRepository(params Listing[] listings)
    {
        var repository = new FakeListingRepository();

        foreach (var listing in listings)
            repository.Items[listing.Id] = listing;

        return repository;
    }

    [Fact]
    public async Task Given_ListingsOfTwoMembers_When_Handling_Then_ReturnsOnlyTheMembersListings()
    {
        var first = BuildDraft(MemberId, "Linear algebra", Now);
        var second = BuildActive(MemberId, "Calculus", Now.AddHours(1));
        var other = BuildActive(OtherMemberId, "Physics", Now.AddHours(2));
        var repository = BuildRepository(first, second, other);
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        result.Value.Items.Should().OnlyContain(i => i.SellerId == MemberId);
        repository.Items.Should().HaveCount(3);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingsInEveryStatus_When_Handling_Then_ReturnsAllOfThem()
    {
        var draft = BuildDraft(MemberId, "Draft book", Now);
        var active = BuildActive(MemberId, "Active book", Now.AddHours(1));
        var reserved = BuildReserved(MemberId, "Reserved book", Now.AddHours(2));
        var sold = BuildSold(MemberId, "Sold book", Now.AddHours(3));
        var hidden = BuildHidden(MemberId, "Hidden book", Now.AddHours(4));
        var repository = BuildRepository(draft, active, reserved, sold, hidden);
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(5);
        result.Value.Items.Select(i => (i.Id, i.Status)).Should().BeEquivalentTo(new[]
        {
            (draft.Id, ListingStatus.Draft),
            (active.Id, ListingStatus.Active),
            (reserved.Id, ListingStatus.Reserved),
            (sold.Id, ListingStatus.Sold),
            (hidden.Id, ListingStatus.Hidden)
        });
        repository.Items.Should().HaveCount(5);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingsWithDifferentCreatedAt_When_Handling_Then_ReturnsNewestFirst()
    {
        var oldest = BuildDraft(MemberId, "Oldest", Now);
        var middle = BuildDraft(MemberId, "Middle", Now.AddHours(1));
        var newest = BuildDraft(MemberId, "Newest", Now.AddHours(2));
        var repository = BuildRepository(oldest, middle, newest);
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_ListingsWithSameCreatedAt_When_Handling_Then_OrdersThemById()
    {
        var first = BuildDraft(MemberId, "First", Now);
        var second = BuildDraft(MemberId, "Second", Now);
        var third = BuildDraft(MemberId, "Third", Now);
        var repository = BuildRepository(first, second, third);
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items.Should().BeInAscendingOrder(i => i.Id);
        repository.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_MemberWithNoListings_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = BuildRepository(BuildActive(OtherMemberId, "Physics", Now));
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_EmptyRepository_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = new FakeListingRepository();
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OneListing_When_Handling_Then_ItemMatchesTheListing()
    {
        var listing = BuildDraft(MemberId, "Linear algebra", Now, 30m);
        var repository = BuildRepository(listing);
        var handler = new GetMyListingsHandler(repository);

        var result = await handler.HandleAsync(new GetMyListingsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(listing.Id);
        item.SellerId.Should().Be(MemberId);
        item.Title.Should().Be("Linear algebra");
        item.Category.Should().Be(Category.Textbook);
        item.Condition.Should().Be(Condition.Good);
        item.Price.Should().Be(30m);
        item.Status.Should().Be(ListingStatus.Draft);
        item.CreatedAt.Should().Be(Now);
        repository.Items.Should().HaveCount(1);
    }
}
