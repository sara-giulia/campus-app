using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews.AddReview;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Reviews.AddReview;

public class AddReviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Member BuildBuyer() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Member BuildOtherMember() =>
        Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;

    private static Listing BuildPublishedListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        return listing;
    }

    private static Listing BuildReservedListing(Guid buyerId)
    {
        var listing = BuildPublishedListing();
        listing.Reserve(buyerId, Now);
        return listing;
    }

    private static Listing BuildSoldListing(Guid buyerId)
    {
        var listing = BuildPublishedListing();
        var reservation = listing.Reserve(buyerId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    private static AddReviewHandler BuildHandler(
        FakeListingRepository listings,
        FakeMemberRepository members,
        FakeReviewRepository reviews,
        FakeUnitOfWork unitOfWork) =>
        new(listings, members, reviews, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_BuyerOfSoldListing_When_Handling_Then_StoresReviewAndSavesOnce()
    {
        var buyer = BuildBuyer();
        var listing = BuildSoldListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listing.Id, 5, "  Great seller  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reviews.Items.Should().ContainSingle();
        result.Value.Id.Should().Be(reviews.Items[0].Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.SellerId.Should().Be(SellerId);
        result.Value.Rating.Should().Be(5);
        result.Value.Comment.Should().Be("Great seller");
        result.Value.CreatedAt.Should().Be(Now);
        reviews.Items[0].ReviewerId.Should().Be(buyer.Id);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsListingNotFoundAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listingId, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsMemberNotFoundAndDoesNotSave()
    {
        var listing = BuildSoldListing(BuyerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(BuyerId, listing.Id, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{BuyerId}' was not found.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListingAndUnknownMember_When_Handling_Then_ReturnsListingNotFound()
    {
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(BuyerId, listingId, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingNotSold_When_Handling_Then_ReturnsSaleNotCompletedAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listing = BuildReservedListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listing.Id, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.SaleNotCompleted");
        result.Error.Message.Should().Be("Only a sold listing can be reviewed.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_OtherMember_When_Handling_Then_ReturnsNotBuyerForbiddenAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var other = BuildOtherMember();
        var listing = BuildSoldListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        members.Items[other.Id] = other;
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(other.Id, listing.Id, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Review.NotBuyer");
        result.Error.Message.Should().Be("Only the buyer of this listing can review the seller.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RatingAboveMax_When_Handling_Then_ReturnsInvalidRatingAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listing = BuildSoldListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listing.Id, 6, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.InvalidRating");
        result.Error.Message.Should().Be($"Rating must be between {Review.MinRating} and {Review.MaxRating}.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingAlreadyReviewed_When_Handling_Then_ReturnsAlreadyExistsAndKeepsOneReview()
    {
        var buyer = BuildBuyer();
        var listing = BuildSoldListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository();
        var existing = Review.Create(buyer.Id, listing, 4, null, Now.AddHours(-2)).Value;
        reviews.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listing.Id, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Review.AlreadyExists");
        result.Error.Message.Should().Be("This sale has already been reviewed.");
        reviews.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsDuplicateAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listing = BuildSoldListing(buyer.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var reviews = new FakeReviewRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reviews, unitOfWork);
        var command = new AddReviewCommand(buyer.Id, listing.Id, 5, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Review.Duplicate");
        result.Error.Message.Should().Match("A review with id '*' already exists.");
        reviews.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
