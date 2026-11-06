using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.ExpireReservations;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.ExpireReservations;

public class ExpireReservationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherBuyerId = new("33333333-3333-3333-3333-333333333333");

    private static Listing DraftListing(string title = "Algebra book")
    {
        return Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
    }

    private static Listing ReservedListing(Guid buyerId, string title = "Algebra book")
    {
        return ReservedListing(buyerId, Now, title);
    }

    private static Listing ReservedListing(Guid buyerId, DateTimeOffset reservedAt, string title = "Algebra book")
    {
        var listing = DraftListing(title);
        listing.Publish();
        listing.Reserve(buyerId, reservedAt);
        return listing;
    }

    private static Listing ActiveListing(string title = "Algebra book")
    {
        var listing = DraftListing(title);
        listing.Publish();
        return listing;
    }

    [Fact]
    public async Task Given_NoListings_When_Handling_Then_ReturnsZeroAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ExpireReservationsHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(48)));

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_OneExpiredOneValidAndOneDraft_When_Handling_Then_ReturnsOneAndSavesOnce()
    {
        var expired = ReservedListing(BuyerId, Now, "Expired");
        var stillValid = ReservedListing(OtherBuyerId, Now.AddHours(1), "Still valid");
        var draft = DraftListing("Draft");
        var repository = new FakeListingRepository();
        repository.Items[expired.Id] = expired;
        repository.Items[stillValid.Id] = stillValid;
        repository.Items[draft.Id] = draft;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ExpireReservationsHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(48)));
        var stillValidVersionBefore = stillValid.Version;
        var draftVersionBefore = draft.Version;

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
        unitOfWork.SaveCount.Should().Be(1);
        expired.Status.Should().Be(ListingStatus.Active);
        expired.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Expired);
        stillValid.Status.Should().Be(ListingStatus.Reserved);
        stillValid.Version.Should().Be(stillValidVersionBefore);
        draft.Status.Should().Be(ListingStatus.Draft);
        draft.Version.Should().Be(draftVersionBefore);
    }

    [Fact]
    public async Task Given_TwoExpiredListings_When_Handling_Then_ReturnsTwoAndSavesOnce()
    {
        var firstExpired = ReservedListing(BuyerId, "First");
        var secondExpired = ReservedListing(OtherBuyerId, "Second");
        var repository = new FakeListingRepository();
        repository.Items[firstExpired.Id] = firstExpired;
        repository.Items[secondExpired.Id] = secondExpired;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ExpireReservationsHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(48)));

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        repository.UpdateCount.Should().Be(2);
        unitOfWork.SaveCount.Should().Be(1);
        firstExpired.Status.Should().Be(ListingStatus.Active);
        firstExpired.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Expired);
        secondExpired.Status.Should().Be(ListingStatus.Active);
        secondExpired.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Expired);
    }

    [Fact]
    public async Task Given_NothingExpired_When_Handling_Then_ReturnsZeroAndDoesNotSave()
    {
        var stillValid = ReservedListing(BuyerId, "Still valid");
        var draft = DraftListing("Draft");
        var active = ActiveListing("Active no reservations");
        var repository = new FakeListingRepository();
        repository.Items[stillValid.Id] = stillValid;
        repository.Items[draft.Id] = draft;
        repository.Items[active.Id] = active;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ExpireReservationsHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(48).AddSeconds(-1)));
        var stillValidVersionBefore = stillValid.Version;

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
        unitOfWork.SaveCount.Should().Be(0);
        stillValid.Status.Should().Be(ListingStatus.Reserved);
        stillValid.Version.Should().Be(stillValidVersionBefore);
    }
}
