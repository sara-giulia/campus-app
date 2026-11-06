using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.UnitTests.Domain.Reviews;

public class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildActiveListing()
    {
        var listing = BuildDraftListing();
        listing.Publish();
        return listing;
    }

    private static Listing BuildReservedListing()
    {
        var listing = BuildActiveListing();
        listing.Reserve(BuyerId, Now);
        return listing;
    }

    private static Listing BuildSoldListing()
    {
        var listing = BuildActiveListing();
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    [Fact]
    public void Given_BuyerOfSoldListing_When_Creating_Then_ReturnsReviewWithTrimmedComment()
    {
        var listing = BuildSoldListing();
        var createdAt = Now.AddHours(1);

        var result = Review.Create(BuyerId, listing, 5, "  Great seller  ", createdAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.SellerId.Should().Be(listing.SellerId);
        result.Value.ReviewerId.Should().Be(BuyerId);
        result.Value.Rating.Should().Be(5);
        result.Value.Comment.Should().Be("Great seller");
        result.Value.CreatedAt.Should().Be(createdAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_NoComment_When_Creating_Then_ReturnsReviewWithNullComment(string? comment)
    {
        var listing = BuildSoldListing();

        var result = Review.Create(BuyerId, listing, 4, comment, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Given_RatingAtLimit_When_Creating_Then_ReturnsReview(int rating)
    {
        var listing = BuildSoldListing();

        var result = Review.Create(BuyerId, listing, rating, null, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(rating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Given_RatingOutsideLimits_When_Creating_Then_ReturnsInvalidRatingError(int rating)
    {
        var listing = BuildSoldListing();

        var result = Review.Create(BuyerId, listing, rating, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.InvalidRating");
        result.Error.Message.Should().Be($"Rating must be between {Review.MinRating} and {Review.MaxRating}.");
    }

    [Fact]
    public void Given_CommentOfMaxLength_When_Creating_Then_ReturnsReview()
    {
        var listing = BuildSoldListing();
        var comment = new string('a', Review.MaxCommentLength);

        var result = Review.Create(BuyerId, listing, 5, comment, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Should().Be(comment);
    }

    [Fact]
    public void Given_CommentLongerThanMax_When_Creating_Then_ReturnsCommentTooLongError()
    {
        var listing = BuildSoldListing();
        var comment = new string('a', Review.MaxCommentLength + 1);

        var result = Review.Create(BuyerId, listing, 5, comment, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.CommentTooLong");
        result.Error.Message.Should().Be($"Comment must have at most {Review.MaxCommentLength} characters.");
    }

    [Fact]
    public void Given_CommentOfMaxLengthWithSpacesAround_When_Creating_Then_ReturnsReviewWithTrimmedComment()
    {
        var listing = BuildSoldListing();
        var text = new string('a', Review.MaxCommentLength);

        var result = Review.Create(BuyerId, listing, 5, "  " + text + "  ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Should().Be(text);
    }

    [Fact]
    public void Given_EmptyReviewerId_When_Creating_Then_ReturnsReviewerEmptyError()
    {
        var listing = BuildSoldListing();

        var result = Review.Create(Guid.Empty, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.ReviewerEmpty");
        result.Error.Message.Should().Be("Reviewer id must not be empty.");
    }

    [Fact]
    public void Given_DraftListing_When_Creating_Then_ReturnsSaleNotCompletedError()
    {
        var listing = BuildDraftListing();

        var result = Review.Create(BuyerId, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.SaleNotCompleted");
        result.Error.Message.Should().Be("Only a sold listing can be reviewed.");
    }

    [Fact]
    public void Given_ActiveListing_When_Creating_Then_ReturnsSaleNotCompletedError()
    {
        var listing = BuildActiveListing();

        var result = Review.Create(BuyerId, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.SaleNotCompleted");
        result.Error.Message.Should().Be("Only a sold listing can be reviewed.");
    }

    [Fact]
    public void Given_ReservedListing_When_Creating_Then_ReturnsSaleNotCompletedError()
    {
        var listing = BuildReservedListing();

        var result = Review.Create(BuyerId, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.SaleNotCompleted");
        result.Error.Message.Should().Be("Only a sold listing can be reviewed.");
    }

    [Fact]
    public void Given_OtherMember_When_Creating_Then_ReturnsNotBuyerForbiddenError()
    {
        var listing = BuildSoldListing();

        var result = Review.Create(OtherMemberId, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Review.NotBuyer");
        result.Error.Message.Should().Be("Only the buyer of this listing can review the seller.");
    }

    [Fact]
    public void Given_Seller_When_Creating_Then_ReturnsNotBuyerForbiddenError()
    {
        var listing = BuildSoldListing();

        var result = Review.Create(SellerId, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Review.NotBuyer");
        result.Error.Message.Should().Be("Only the buyer of this listing can review the seller.");
    }

    [Fact]
    public void Given_EmptyReviewerIdOnDraftListing_When_Creating_Then_ReturnsReviewerEmptyError()
    {
        var listing = BuildDraftListing();

        var result = Review.Create(Guid.Empty, listing, 5, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.ReviewerEmpty");
        result.Error.Message.Should().Be("Reviewer id must not be empty.");
    }

    [Fact]
    public void Given_OtherMemberWithInvalidRating_When_Creating_Then_ReturnsNotBuyerError()
    {
        var listing = BuildSoldListing();

        var result = Review.Create(OtherMemberId, listing, 0, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Review.NotBuyer");
        result.Error.Message.Should().Be("Only the buyer of this listing can review the seller.");
    }

    [Fact]
    public void Given_BuyerWithInvalidRatingOnReservedListing_When_Creating_Then_ReturnsSaleNotCompletedError()
    {
        var listing = BuildReservedListing();

        var result = Review.Create(BuyerId, listing, 0, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Review.SaleNotCompleted");
        result.Error.Message.Should().Be("Only a sold listing can be reviewed.");
    }

    [Fact]
    public void Given_SoldListing_When_CreatingTwice_Then_ReturnsTwoDifferentIds()
    {
        var listing = BuildSoldListing();

        var first = Review.Create(BuyerId, listing, 5, null, Now);
        var second = Review.Create(BuyerId, listing, 5, null, Now);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().NotBe(second.Value.Id);
    }
}
