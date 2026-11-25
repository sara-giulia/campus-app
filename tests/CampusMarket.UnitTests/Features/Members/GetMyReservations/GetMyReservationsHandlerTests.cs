using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetMyReservations;

public class GetMyReservationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildActiveListing(string title, decimal price, DateTimeOffset createdAt)
    {
        var listing = Listing.Create(SellerId, title, Category.Textbook, Condition.Good, price, createdAt).Value;
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public async Task Given_MemberWithNoReservations_When_Handling_Then_ReturnsEmptyList()
    {
        var repository = new FakeListingRepository();
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoReservationsOnTwoListings_When_Handling_Then_ReturnsBothNewestFirstWithTitleAndPrice()
    {
        var newerCreatedAt = Now.AddHours(-1);
        var olderCreatedAt = Now.AddHours(-2);
        var newerListing = BuildActiveListing("Linear algebra", 30m, newerCreatedAt);
        var newerReservation = newerListing.Reserve(BuyerId, newerCreatedAt).Value;
        var olderListing = BuildActiveListing("Calculus", 20m, olderCreatedAt);
        var olderReservation = olderListing.Reserve(BuyerId, olderCreatedAt).Value;
        var repository = new FakeListingRepository();
        repository.Items[newerListing.Id] = newerListing;
        repository.Items[olderListing.Id] = olderListing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].ReservationId.Should().Be(newerReservation.Id);
        result.Value.Items[0].ListingId.Should().Be(newerListing.Id);
        result.Value.Items[0].ListingTitle.Should().Be("Linear algebra");
        result.Value.Items[0].Price.Should().Be(30m);
        result.Value.Items[0].Status.Should().Be(ReservationStatus.Active);
        result.Value.Items[1].ReservationId.Should().Be(olderReservation.Id);
        result.Value.Items[1].ListingId.Should().Be(olderListing.Id);
        result.Value.Items[1].ListingTitle.Should().Be("Calculus");
        result.Value.Items[1].Price.Should().Be(20m);
        result.Value.Items[1].Status.Should().Be(ReservationStatus.Active);
        repository.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ReservationOfAnotherMember_When_Handling_Then_DoesNotReturnIt()
    {
        var listing = BuildActiveListing("Algebra book", 25m, Now);
        listing.Reserve(OtherMemberId, Now);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        repository.Items[listing.Id].Reservations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_CancelledReservation_When_Handling_Then_ReturnsItWithCancelledStatus()
    {
        var listing = BuildActiveListing("Algebra book", 25m, Now);
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.CancelReservation(reservation.Id);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].ReservationId.Should().Be(reservation.Id);
        result.Value.Items[0].Status.Should().Be(ReservationStatus.Cancelled);
        repository.Items[listing.Id].Reservations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ReservationOlderThan48Hours_When_Handling_Then_ReturnsExpiredAndRepositoryStaysActive()
    {
        var createdAt = Now.AddHours(-72);
        var listing = BuildActiveListing("Algebra book", 25m, createdAt);
        var reservation = listing.Reserve(BuyerId, createdAt).Value;
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].ReservationId.Should().Be(reservation.Id);
        result.Value.Items[0].Status.Should().Be(ReservationStatus.Expired);
        repository.Items[listing.Id].Reservations[0].Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_TwoReservationsAtSameCreatedAt_When_Handling_Then_OrderedByReservationIdAscending()
    {
        var firstListing = BuildActiveListing("Linear algebra", 30m, Now);
        var firstReservation = firstListing.Reserve(BuyerId, Now).Value;
        var secondListing = BuildActiveListing("Calculus", 20m, Now);
        var secondReservation = secondListing.Reserve(BuyerId, Now).Value;
        var repository = new FakeListingRepository();
        repository.Items[firstListing.Id] = firstListing;
        repository.Items[secondListing.Id] = secondListing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));
        var query = new GetMyReservationsQuery(BuyerId);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().BeInAscendingOrder(i => i.ReservationId);
        result.Value.Items.Select(i => i.ReservationId).Should()
            .Contain(new[] { firstReservation.Id, secondReservation.Id });
    }

    private static Listing BuildSoldListing(string title, Guid buyerId, DateTimeOffset createdAt)
    {
        var listing = BuildActiveListing(title, 25m, createdAt);
        var reservation = listing.Reserve(buyerId, createdAt).Value;
        listing.ConfirmSale(reservation.Id, createdAt);
        return listing;
    }

    [Fact]
    public async Task Given_SoldListingWithoutReview_When_Handling_Then_ReviewedIsFalse()
    {
        var listing = BuildSoldListing("Algebra book", BuyerId, Now);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reviews = new FakeReviewRepository();
        var handler = new GetMyReservationsHandler(repository, reviews, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.Reviewed.Should().BeFalse();
        reviews.Items.Should().BeEmpty();
        repository.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_SoldListingReviewedByMember_When_Handling_Then_ReviewedIsTrue()
    {
        var listing = BuildSoldListing("Algebra book", BuyerId, Now);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(Review.Create(BuyerId, listing, 5, null, Now).Value);
        var handler = new GetMyReservationsHandler(repository, reviews, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ListingId.Should().Be(listing.Id);
        item.ListingTitle.Should().Be("Algebra book");
        item.Price.Should().Be(25m);
        item.Reviewed.Should().BeTrue();
        reviews.Items.Should().HaveCount(1);
        repository.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_Handling_Then_ReviewedIsFalse()
    {
        var listing = BuildActiveListing("Algebra book", 25m, Now);
        listing.Reserve(BuyerId, Now);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.Reviewed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_ExpiredReservation_When_Handling_Then_ReviewedIsFalse()
    {
        var createdAt = Now.AddHours(-72);
        var listing = BuildActiveListing("Algebra book", 25m, createdAt);
        listing.Reserve(BuyerId, createdAt);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(ReservationStatus.Expired);
        item.Reviewed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_CancelledReservation_When_Handling_Then_ReviewedIsFalse()
    {
        var listing = BuildActiveListing("Algebra book", 25m, Now);
        var reservation = listing.Reserve(BuyerId, Now).Value;
        listing.CancelReservation(reservation.Id);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var handler = new GetMyReservationsHandler(repository, new FakeReviewRepository(), new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(ReservationStatus.Cancelled);
        item.Reviewed.Should().BeFalse();
    }

    [Fact]
    public async Task Given_ReviewWrittenByAnotherMember_When_Handling_Then_ReviewedIsFalseForThisMember()
    {
        // The member reserved and cancelled; somebody else bought the listing and reviewed it.
        var listing = BuildActiveListing("Algebra book", 25m, Now);
        var cancelled = listing.Reserve(BuyerId, Now).Value;
        listing.CancelReservation(cancelled.Id);
        var otherReservation = listing.Reserve(OtherMemberId, Now).Value;
        listing.ConfirmSale(otherReservation.Id, Now);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(Review.Create(OtherMemberId, listing, 5, null, Now).Value);
        var handler = new GetMyReservationsHandler(repository, reviews, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ReservationId.Should().Be(cancelled.Id);
        item.Reviewed.Should().BeFalse();
        reviews.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_OneReviewedAndOneNotReviewedSale_When_Handling_Then_EachItemHasItsOwnFlag()
    {
        var reviewedListing = BuildSoldListing("Linear algebra", BuyerId, Now.AddHours(-1));
        var plainListing = BuildSoldListing("Calculus", BuyerId, Now.AddHours(-2));
        var repository = new FakeListingRepository();
        repository.Items[reviewedListing.Id] = reviewedListing;
        repository.Items[plainListing.Id] = plainListing;
        var reviews = new FakeReviewRepository();
        reviews.Items.Add(Review.Create(BuyerId, reviewedListing, 4, null, Now).Value);
        var handler = new GetMyReservationsHandler(repository, reviews, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new GetMyReservationsQuery(BuyerId), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Single(i => i.ListingId == reviewedListing.Id).Reviewed.Should().BeTrue();
        result.Value.Items.Single(i => i.ListingId == plainListing.Id).Reviewed.Should().BeFalse();
    }
}
