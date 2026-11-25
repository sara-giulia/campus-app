using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class CancelReservationTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_Buyer_When_Cancelling_Then_Returns204AndCancels()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_Seller_When_Cancelling_Then_Returns204()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_Admin_When_Cancelling_Then_Returns204()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_AnotherStudent_When_Cancelling_Then_Returns403NotAllowed()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.NotAllowed");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_UnknownReservationId_When_Cancelling_Then_Returns404ReservationNotFound()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_ReservationAlreadyCancelled_When_Cancelling_Then_Returns400NotActive()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.CancelReservation(reservation.Id);
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.NotActive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_NoToken_When_Cancelling_Then_Returns401()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        await SeedAsync(buyer, listing);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }
}
