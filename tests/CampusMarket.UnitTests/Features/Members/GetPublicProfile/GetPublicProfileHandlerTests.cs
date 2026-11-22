using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Members.GetPublicProfile;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetPublicProfile;

public class GetPublicProfileHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherSellerId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UnknownId = new("44444444-4444-4444-4444-444444444444");

    private static Member BuildSeller() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static FakeMemberRepository BuildMembers(Member seller)
    {
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        return members;
    }

    private static Listing BuildDraftListing(Guid sellerId, string title, decimal price, DateTimeOffset createdAt) =>
        Listing.Create(sellerId, title, Category.Textbook, Condition.Good, price, createdAt).Value;

    private static Listing BuildActiveListing(Guid sellerId, string title, decimal price, DateTimeOffset createdAt)
    {
        var listing = BuildDraftListing(sellerId, title, price, createdAt);
        listing.Publish();
        return listing;
    }

    private static Listing BuildSoldListing(Guid sellerId, string title, decimal price, DateTimeOffset createdAt)
    {
        var listing = BuildActiveListing(sellerId, title, price, createdAt);
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    private static Review BuildReview(Guid sellerId, int rating, DateTimeOffset createdAt) =>
        Review.Create(BuyerId, BuildSoldListing(sellerId, "Sold book", 10m, createdAt), rating, null, Now).Value;

    [Fact]
    public async Task Given_MemberWithActiveDraftAndSoldListings_When_Handling_Then_ReturnsOnlyActiveNewestFirst()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var older = BuildActiveListing(seller.Id, "Algebra book", 25m, Now);
        var newer = BuildActiveListing(seller.Id, "OOP textbook", 40m, Now.AddHours(1));
        var draft = BuildDraftListing(seller.Id, "Draft notes", 5m, Now.AddHours(2));
        var sold = BuildSoldListing(seller.Id, "Sold calculator", 60m, Now.AddHours(3));
        var listings = new FakeListingRepository();
        listings.Items[older.Id] = older;
        listings.Items[newer.Id] = newer;
        listings.Items[draft.Id] = draft;
        listings.Items[sold.Id] = sold;
        var reviews = new FakeReviewRepository();
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(seller.Id);
        result.Value.Name.Should().Be("Ana Pop");
        result.Value.MemberSince.Should().Be(Now);
        result.Value.ActiveListings.Should().Equal(
            new PublicListingItem(newer.Id, "OOP textbook", 40m),
            new PublicListingItem(older.Id, "Algebra book", 25m));
        members.Items.Should().ContainSingle();
        listings.Items.Should().HaveCount(4);
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ActiveListingOfOtherSeller_When_Handling_Then_IsNotIncluded()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var mine = BuildActiveListing(seller.Id, "Algebra book", 25m, Now);
        var theirs = BuildActiveListing(OtherSellerId, "Physics book", 30m, Now.AddHours(1));
        var listings = new FakeListingRepository();
        listings.Items[mine.Id] = mine;
        listings.Items[theirs.Id] = theirs;
        var reviews = new FakeReviewRepository();
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveListings.Select(l => l.Id).Should().Equal(mine.Id);
        members.Items.Should().ContainSingle();
        listings.Items.Should().HaveCount(2);
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NoReviews_When_Handling_Then_ReturnsNullAverageAndZeroCount()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var listings = new FakeListingRepository();
        var reviews = new FakeReviewRepository();
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().BeNull();
        result.Value.ReviewCount.Should().Be(0);
        result.Value.ActiveListings.Should().BeEmpty();
        members.Items.Should().ContainSingle();
        listings.Items.Should().BeEmpty();
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_RatingsFiveAndFour_When_Handling_Then_ReturnsAverageAndCount()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var listings = new FakeListingRepository();
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(1)));
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().Be(4.5);
        result.Value.ReviewCount.Should().Be(2);
        members.Items.Should().ContainSingle();
        listings.Items.Should().BeEmpty();
        reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_RatingsFiveFourFour_When_Handling_Then_RoundsAverageToTwoDecimals()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var listings = new FakeListingRepository();
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(1)));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(2)));
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().Be(4.33);
        result.Value.ReviewCount.Should().Be(3);
        members.Items.Should().ContainSingle();
        listings.Items.Should().BeEmpty();
        reviews.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_UnknownMemberId_When_Handling_Then_ReturnsNotFound()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var listing = BuildActiveListing(seller.Id, "Algebra book", 25m, Now);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reviews = new FakeReviewRepository();
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(UnknownId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{UnknownId}' was not found.");
        members.Items.Should().ContainSingle();
        listings.Items.Should().ContainSingle();
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoActiveListingsWithSameCreatedAt_When_Handling_Then_AreOrderedByIdAscending()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var first = BuildActiveListing(seller.Id, "Algebra book", 25m, Now);
        var second = BuildActiveListing(seller.Id, "OOP textbook", 40m, Now);
        var listings = new FakeListingRepository();
        listings.Items[first.Id] = first;
        listings.Items[second.Id] = second;
        var reviews = new FakeReviewRepository();
        var handler = new GetPublicProfileHandler(members, listings, reviews);
        var query = new GetPublicProfileQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveListings.Should().HaveCount(2);
        result.Value.ActiveListings.Should().BeInAscendingOrder(i => i.Id);
        members.Items.Should().ContainSingle();
        listings.Items.Should().HaveCount(2);
        reviews.Items.Should().BeEmpty();
    }
}
