using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Listings;

public class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Reservation ActiveReservation()
    {
        var sellerId = Guid.NewGuid();
        var listingResult = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now);
        var listing = listingResult.Value;
        listing.Publish(Now.AddDays(30));

        var reservationResult = listing.Reserve(Guid.NewGuid(), Now);
        return reservationResult.Value;
    }

    [Fact]
    public void Given_ActiveReservation_When_Cancelling_Then_ReturnsSuccessWithCancelledStatus()
    {
        var reservation = ActiveReservation();

        var result = reservation.Cancel();

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public void Given_CancelledReservation_When_CancellingAgain_Then_ReturnsValidationError()
    {
        var reservation = ActiveReservation();
        reservation.Cancel();

        var result = reservation.Cancel();

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be cancelled.");
    }

    [Fact]
    public void Given_ActiveReservation_When_CheckingExpirationBeforeExpiry_Then_ReturnsFalse()
    {
        var reservation = ActiveReservation();

        var expired = reservation.IsExpired(Now.AddHours(47));

        expired.Should().BeFalse();
    }

    [Fact]
    public void Given_ActiveReservation_When_CheckingExpirationAtExpiry_Then_ReturnsTrue()
    {
        var reservation = ActiveReservation();

        var expired = reservation.IsExpired(Now.AddHours(48));

        expired.Should().BeTrue();
    }

    [Fact]
    public void Given_CancelledReservation_When_CheckingExpirationAtExpiry_Then_ReturnsFalse()
    {
        var reservation = ActiveReservation();
        reservation.Cancel();

        var expired = reservation.IsExpired(Now.AddHours(48));

        expired.Should().BeFalse();
    }

    [Fact]
    public void Given_ValidData_When_Creating_Then_StampsCreatedAtAndExpiresAt48HoursLater()
    {
        var reservation = ActiveReservation();

        reservation.CreatedAt.Should().Be(Now);
        reservation.ExpiresAt.Should().Be(Now.AddHours(48));
        reservation.Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public void Given_ActiveReservation_When_Completing_Then_ReturnsSuccessWithCompletedStatus()
    {
        var reservation = ActiveReservation();

        var result = reservation.Complete();

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public void Given_CancelledReservation_When_Completing_Then_ReturnsValidationErrorAndStatusUnchanged()
    {
        var reservation = ActiveReservation();
        reservation.Cancel();

        var result = reservation.Complete();

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can be completed.");
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public void Given_ActiveReservation_When_Expiring_Then_ReturnsSuccessWithExpiredStatus()
    {
        var reservation = ActiveReservation();

        var result = reservation.Expire();

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Expired);
    }

    [Fact]
    public void Given_CancelledReservation_When_Expiring_Then_ReturnsValidationErrorAndStatusUnchanged()
    {
        var reservation = ActiveReservation();
        reservation.Cancel();

        var result = reservation.Expire();

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Reservation.NotActive");
        result.Error.Message.Should().Be("Only an active reservation can expire.");
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public void Given_ReservationWithAgreedPrice_When_AskingPriceFor_Then_ReturnsAgreedPrice()
    {
        var listing = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var reservation = listing.Reserve(Guid.NewGuid(), Now, Money.Create(80m).Value).Value;

        reservation.AgreedPrice.Should().Be(80m);
        reservation.PriceFor(listing).Should().Be(80m);
    }

    [Fact]
    public void Given_ReservationWithoutAgreedPrice_When_AskingPriceFor_Then_ReturnsListingPrice()
    {
        var listing = Listing.Create(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;

        reservation.AgreedPrice.Should().BeNull();
        reservation.PriceFor(listing).Should().Be(100m);
    }
}
