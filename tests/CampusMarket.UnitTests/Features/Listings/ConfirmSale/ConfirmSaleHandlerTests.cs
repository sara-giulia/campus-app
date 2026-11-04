using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.ConfirmSale;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.ConfirmSale;

public class ConfirmSaleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AdminId = new("44444444-4444-4444-4444-444444444444");

    private static (Listing Listing, Reservation Reservation) BuildReservedListing()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        var reservation = listing.Reserve(BuyerId, Now).Value;
        return (listing, reservation);
    }

    [Fact]
    public async Task Given_ReservedListing_When_SellerConfirms_Then_ListingSoldAndSavesOnce()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, reservation.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reservation.Id);
        listing.Status.Should().Be(ListingStatus.Sold);
        reservation.Status.Should().Be(ReservationStatus.Completed);
        unitOfWork.SaveCount.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_AdminConfirms_Then_ListingSoldAndSavesOnce()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, reservation.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reservation.Id);
        listing.Status.Should().Be(ListingStatus.Sold);
        reservation.Status.Should().Be(ReservationStatus.Completed);
        unitOfWork.SaveCount.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_BuyerConfirms_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, reservation.Id, BuyerId, false);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can confirm this sale.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
        reservation.Status.Should().Be(ReservationStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReservedListing_When_OtherMemberConfirms_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, reservation.Id, OtherMemberId, false);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can confirm this sale.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
        reservation.Status.Should().Be(ReservationStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_SellerConfirms_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var unknownListingId = Guid.NewGuid();
        var command = new ConfirmSaleCommand(unknownListingId, Guid.NewGuid(), SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_OtherMemberConfirms_Then_ReturnsNotFoundNotForbidden()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var unknownListingId = Guid.NewGuid();
        var command = new ConfirmSaleCommand(unknownListingId, Guid.NewGuid(), OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReservedListing_When_SellerConfirmsUnknownReservationId_Then_ReturnsReservationNotFoundAndDoesNotSave()
    {
        var (listing, _) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, Guid.NewGuid(), SellerId, false);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Reservation.NotFound");
        result.Error.Message.Should().Be("Reservation not found.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReservedListing_When_BuyerConfirmsUnknownReservationId_Then_ReturnsForbiddenBeforeReservationCheck()
    {
        var (listing, _) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(1)));
        var command = new ConfirmSaleCommand(listing.Id, Guid.NewGuid(), BuyerId, false);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Listing.NotOwner");
        result.Error.Message.Should().Be("Only the seller or an Admin can confirm this sale.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReservedListing_When_SellerConfirmsAtExactExpiry_Then_ReturnsReservationExpiredAndDoesNotSave()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmSaleHandler(repository, unitOfWork, new FixedTimeProvider(Now.AddHours(48)));
        var command = new ConfirmSaleCommand(listing.Id, reservation.Id, SellerId, false);
        var statusBefore = listing.Status;
        var versionBefore = listing.Version;

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.Expired");
        result.Error.Message.Should().Be("The reservation has expired.");
        listing.Status.Should().Be(statusBefore);
        listing.Version.Should().Be(versionBefore);
        reservation.Status.Should().Be(ReservationStatus.Active);
        unitOfWork.SaveCount.Should().Be(0);
        repository.UpdateCount.Should().Be(0);
    }
}
