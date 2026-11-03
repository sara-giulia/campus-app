using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.CancelReservation;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.CancelReservation;

public class CancelReservationHandlerTests
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
    public async Task Given_ReservedListing_When_BuyerCancels_Then_CancelsReservationAndSavesOnce()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var command = new CancelReservationCommand(listing.Id, reservation.Id, BuyerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reservation.Id);
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(1);
        repository.UpdateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_SellerCancels_Then_CancelsReservationAndSavesOnce()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var command = new CancelReservationCommand(listing.Id, reservation.Id, SellerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reservation.Id);
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_AdminCancels_Then_CancelsReservationAndSavesOnce()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var command = new CancelReservationCommand(listing.Id, reservation.Id, AdminId, true);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(reservation.Id);
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        listing.Status.Should().Be(ListingStatus.Active);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ReservedListing_When_OtherMemberCancels_Then_ReturnsForbiddenAndDoesNotSave()
    {
        var (listing, reservation) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var command = new CancelReservationCommand(listing.Id, reservation.Id, OtherMemberId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Reservation.NotAllowed");
        result.Error.Message.Should().Be("Only the buyer, the seller or an Admin can cancel this reservation.");
        reservation.Status.Should().Be(ReservationStatus.Active);
        listing.Status.Should().Be(ListingStatus.Reserved);
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Cancelling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var unknownListingId = Guid.NewGuid();
        var command = new CancelReservationCommand(unknownListingId, Guid.NewGuid(), BuyerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownReservationId_When_Cancelling_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var (listing, _) = BuildReservedListing();
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var unknownReservationId = Guid.NewGuid();
        var command = new CancelReservationCommand(listing.Id, unknownReservationId, BuyerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Reservation.NotFound");
        result.Error.Message.Should().Be($"Reservation '{unknownReservationId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AlreadyCancelledReservation_When_BuyerCancelsAgain_Then_ReturnsValidationErrorAndDoesNotSave()
    {
        var (listing, reservation) = BuildReservedListing();
        listing.CancelReservation(reservation.Id);
        var repository = new FakeListingRepository();
        repository.Items[listing.Id] = listing;
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CancelReservationHandler(repository, unitOfWork);
        var command = new CancelReservationCommand(listing.Id, reservation.Id, BuyerId, false);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be cancelled.");
        unitOfWork.SaveCount.Should().Be(0);
    }
}
