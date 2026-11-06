using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews.GetMemberReviews;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Reviews.GetMemberReviews;

public class GetMemberReviewsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Member BuildSeller() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Listing BuildSoldListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    private static Review BuildReview(Guid sellerId, int rating, DateTimeOffset createdAt, string? comment = null) =>
        Review.Create(BuyerId, BuildSoldListing(sellerId), rating, comment, createdAt).Value;

    private static FakeMemberRepository BuildMembers(Member seller)
    {
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        return members;
    }

    [Fact]
    public async Task Given_MemberWithNoReviews_When_Handling_Then_ReturnsEmptyPageWithNullAverage()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MemberId.Should().Be(seller.Id);
        result.Value.AverageRating.Should().BeNull();
        result.Value.ReviewCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoReviews_When_Handling_Then_ReturnsNewestFirstWithAverage()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var older = BuildReview(seller.Id, 5, Now.AddHours(-2), "Great seller");
        var newer = BuildReview(seller.Id, 4, Now.AddHours(-1), "Good");
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(older);
        reviews.Items.Add(newer);
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().Be(4.5);
        result.Value.ReviewCount.Should().Be(2);
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().Equal(
            new MemberReviewItem(newer.Id, newer.ListingId, BuyerId, 4, "Good", Now.AddHours(-1)),
            new MemberReviewItem(older.Id, older.ListingId, BuyerId, 5, "Great seller", Now.AddHours(-2)));
        members.Items.Should().ContainSingle();
        reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_RatingsFiveFourFour_When_Handling_Then_RoundsAverageToTwoDecimals()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now.AddHours(-3)));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(-2)));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(-1)));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AverageRating.Should().Be(4.33);
        result.Value.ReviewCount.Should().Be(3);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_TwoReviewsWithSameCreatedAt_When_Handling_Then_OrdersById()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        reviews.Items.Add(BuildReview(seller.Id, 4, Now));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().BeInAscendingOrder(i => i.Id);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ReviewOfOtherSeller_When_Handling_Then_IsNotReturnedNorCounted()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var mine = BuildReview(seller.Id, 5, Now.AddHours(-2));
        var theirs = BuildReview(OtherMemberId, 1, Now.AddHours(-1));
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(mine);
        reviews.Items.Add(theirs);
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(mine.Id);
        result.Value.ReviewCount.Should().Be(1);
        result.Value.AverageRating.Should().Be(5);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ThreeReviews_When_RequestingSecondPageOfTwo_Then_ReturnsOldestWithTotalsOverAll()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var oldest = BuildReview(seller.Id, 5, Now.AddHours(-3));
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(oldest);
        reviews.Items.Add(BuildReview(seller.Id, 4, Now.AddHours(-2)));
        reviews.Items.Add(BuildReview(seller.Id, 3, Now.AddHours(-1)));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id, Page: 2, PageSize: 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(oldest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.ReviewCount.Should().Be(3);
        result.Value.AverageRating.Should().Be(4);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_MaxPageSize_When_Handling_Then_ReturnsSuccess()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id, PageSize: GetMemberReviewsQuery.MaxPageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
        result.Value.Items.Should().ContainSingle();
        members.Items.Should().ContainSingle();
        reviews.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_PageZero_When_Handling_Then_ReturnsInvalidPageError()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id, Page: 0);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reviews.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        members.Items.Should().ContainSingle();
        reviews.Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Given_PageSizeOutsideLimits_When_Handling_Then_ReturnsInvalidPageSizeError(int pageSize)
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(BuildReview(seller.Id, 5, Now));
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id, PageSize: pageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reviews.InvalidPageSize");
        result.Error.Message.Should().Be($"Page size must be between 1 and {GetMemberReviewsQuery.MaxPageSize}.");
        members.Items.Should().ContainSingle();
        reviews.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsMemberNotFound()
    {
        var members = new FakeMemberRepository();
        var reviews = new FakeReviewRepository();
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(SellerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{SellerId}' was not found.");
        members.Items.Should().BeEmpty();
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PageZeroForUnknownMember_When_Handling_Then_ReturnsInvalidPageError()
    {
        var members = new FakeMemberRepository();
        var reviews = new FakeReviewRepository();
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(SellerId, Page: 0);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reviews.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        members.Items.Should().BeEmpty();
        reviews.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_DefaultQuery_When_Handling_Then_UsesPageOneAndPageSizeTwenty()
    {
        var seller = BuildSeller();
        var members = BuildMembers(seller);
        var reviews = new FakeReviewRepository();
        var handler = new GetMemberReviewsHandler(members, reviews);
        var query = new GetMemberReviewsQuery(seller.Id);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        members.Items.Should().ContainSingle();
        reviews.Items.Should().BeEmpty();
    }
}
