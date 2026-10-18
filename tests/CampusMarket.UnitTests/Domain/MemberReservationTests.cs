
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using Xunit;

namespace CampusMarket.UnitTests.Domain;

public class MemberReservationTests
{
    [Fact]
    public void Email_ValidAddress_ShouldBeNormalized()
    {
        var result = Email.Create("Ana@Gmail.COM");

        Assert.True(result.IsSuccess);
        Assert.Equal("ana@gmail.com", result.Value.Value);
    }

    [Fact]
    public void Email_InvalidAddress_ShouldFail()
    {
        var result = Email.Create("anagmail.com");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Member_ValidData_ShouldBeCreated()
    {
        var now = DateTimeOffset.UtcNow;

        var result = Member.Create("Ana", "ana@gmail.com", now);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ana", result.Value.Name);
        Assert.Equal("ana@gmail.com", result.Value.Email.Value);
        Assert.Equal(now, result.Value.CreatedAt);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Fact]
    public void Member_InvalidName_ShouldFail()
    {
        var result = Member.Create(
            "A", "ana@gmail.com", DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Listing_ValidReservation_ShouldSucceed()
    {
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            sellerId, "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var oldVersion = listing.Version;
        var result = listing.Reserve(buyerId, now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ListingStatus.Reserved, listing.Status);
        Assert.Equal(oldVersion + 1, listing.Version);
        Assert.Single(listing.Reservations);
        Assert.Equal(buyerId, result.Value.MemberId);
        Assert.Equal(listing.Id, result.Value.ListingId);
        Assert.Equal(now.AddHours(48), result.Value.ExpiresAt);
    }

    [Fact]
    public void Listing_SellerCannotReserveOwnProduct()
    {
        var sellerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            sellerId, "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var result = listing.Reserve(sellerId, now);

        Assert.True(result.IsFailure);
        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Empty(listing.Reservations);
    }

    [Fact]
    public void Listing_AlreadyReserved_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(), "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();
        listing.Reserve(Guid.NewGuid(), now);

        var result = listing.Reserve(Guid.NewGuid(), now);

        Assert.True(result.IsFailure);
        Assert.Single(listing.Reservations);
    }

    [Fact]
    public void Reservation_CancelOnlyOnce()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(), "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var reservation = listing.Reserve(
            Guid.NewGuid(), now).Value;

        Assert.True(reservation.Cancel().IsSuccess);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.True(reservation.Cancel().IsFailure);
    }

    [Fact]
    public void Reservation_ShouldExpireAfter48Hours()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(), "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var reservation = listing.Reserve(
            Guid.NewGuid(), now).Value;

        Assert.False(reservation.IsExpired(now.AddHours(47)));
        Assert.True(reservation.IsExpired(now.AddHours(48)));
    }
    
    
    
    [Fact]
    public void Member_InvalidUpdate_ShouldNotChangeData()
    {
        var member = Member.Create(
            "Ana", "ana@gmail.com", DateTimeOffset.UtcNow).Value;

        var result = member.Update("A", "new@gmail.com");

        Assert.True(result.IsFailure);
        Assert.Equal("Ana", member.Name);
        Assert.Equal("ana@gmail.com", member.Email.Value);
    }

    [Fact]
    public void Listing_DraftCannotBeReserved()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(), "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        var result = listing.Reserve(Guid.NewGuid(), now);

        Assert.True(result.IsFailure);
        Assert.Equal(ListingStatus.Draft, listing.Status);
        Assert.Empty(listing.Reservations);
    }

    [Fact]
    public void Listing_FailedReservation_ShouldNotChangeVersion()
    {
        var sellerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            sellerId, "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var oldVersion = listing.Version;
        var result = listing.Reserve(sellerId, now);

        Assert.True(result.IsFailure);
        Assert.Equal(oldVersion, listing.Version);
        Assert.Empty(listing.Reservations);
    }

    
    
    [Fact]
    public void Listing_CancelReservation_ShouldReactivateListing()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(),
            "Java Book",
            Category.Textbook,
            Condition.Good,
            50,
            now).Value;

        listing.Publish();

        var reservation = listing.Reserve(Guid.NewGuid(), now).Value;
        var previousVersion = listing.Version;

        var result = listing.CancelReservation(reservation.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal(previousVersion + 1, listing.Version);
        Assert.Single(listing.Reservations);
    }
    
    
    [Fact]
    public void Listing_CancelReservationTwice_ShouldFail()
    {
        var now = DateTimeOffset.UtcNow;

        var listing = Listing.Create(
            Guid.NewGuid(), "Java Book",
            Category.Textbook, Condition.Good, 50, now).Value;

        listing.Publish();

        var reservation = listing.Reserve(Guid.NewGuid(), now).Value;

        var firstResult = listing.CancelReservation(reservation.Id);
        var versionAfterCancellation = listing.Version;

        var secondResult = listing.CancelReservation(reservation.Id);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsFailure);
        Assert.Equal("Reservation.NotActive", secondResult.Error.Code);
        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(versionAfterCancellation, listing.Version);
    }
    
    
    [Fact]
    public void Member_CreateWithInvalidEmail_ShouldFail()
    {
        var result = Member.Create(
            "Ana",
            "invalid-email",
            DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
    }



}
