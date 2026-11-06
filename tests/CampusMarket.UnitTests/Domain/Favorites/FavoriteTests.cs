using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Favorites;

public class FavoriteTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildPublishedListing()
    {
        var listing = BuildDraftListing();
        listing.Publish();
        return listing;
    }

    [Fact]
    public void Given_PublishedListingOfSomeoneElse_When_Creating_Then_ReturnsFavoriteWithMemberListingAndCreatedAt()
    {
        var listing = BuildPublishedListing();
        var createdAt = Now.AddHours(1);

        var result = Favorite.Create(OtherMemberId, listing, createdAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.MemberId.Should().Be(OtherMemberId);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Given_EmptyMemberId_When_Creating_Then_ReturnsMemberEmptyError()
    {
        var listing = BuildPublishedListing();

        var result = Favorite.Create(Guid.Empty, listing, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.MemberEmpty");
        result.Error.Message.Should().Be("Member id must not be empty.");
    }

    [Fact]
    public void Given_SellerOfListing_When_Creating_Then_ReturnsOwnListingError()
    {
        var listing = BuildPublishedListing();

        var result = Favorite.Create(SellerId, listing, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.OwnListing");
        result.Error.Message.Should().Be("A seller cannot add their own listing to favorites.");
    }

    [Fact]
    public void Given_DraftListing_When_Creating_Then_ReturnsListingNotAvailableError()
    {
        var listing = BuildDraftListing();

        var result = Favorite.Create(OtherMemberId, listing, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.ListingNotAvailable");
        result.Error.Message.Should().Be("Only a published listing can be added to favorites.");
    }

    [Fact]
    public void Given_ReservedListing_When_Creating_Then_ReturnsFavorite()
    {
        var listing = BuildPublishedListing();
        listing.Reserve(OtherMemberId, Now);

        var result = Favorite.Create(OtherMemberId, listing, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public void Given_SoldListing_When_Creating_Then_ReturnsFavorite()
    {
        var listing = BuildPublishedListing();
        var reservation = listing.Reserve(OtherMemberId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now.AddHours(1));

        var result = Favorite.Create(OtherMemberId, listing, Now.AddHours(2));

        listing.Status.Should().Be(ListingStatus.Sold);
        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public void Given_PublishedListing_When_CreatingTwice_Then_ReturnsTwoDifferentIds()
    {
        var listing = BuildPublishedListing();

        var first = Favorite.Create(OtherMemberId, listing, Now);
        var second = Favorite.Create(OtherMemberId, listing, Now);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().NotBe(second.Value.Id);
    }

    [Fact]
    public void Given_HiddenListing_When_Creating_Then_ReturnsListingNotAvailableError()
    {
        var listing = BuildPublishedListing();
        listing.Hide();

        var result = Favorite.Create(OtherMemberId, listing, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Favorite.ListingNotAvailable");
        result.Error.Message.Should().Be("Only a published listing can be added to favorites.");
    }
}
