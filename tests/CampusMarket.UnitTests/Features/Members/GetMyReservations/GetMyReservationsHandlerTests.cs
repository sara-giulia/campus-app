using CampusMarket.Api.Domain.Listings;
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
}
