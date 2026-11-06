using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.ReserveListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class ReserveListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_SeededBuyerAndActiveListing_When_Reserving_Then_Returns201AndSaves()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ReserveListingResponse>(Json);
        body!.ListingId.Should().Be(listing.Id);
        body.BuyerId.Should().Be(buyer.Id);
        body.Status.Should().Be(ReservationStatus.Active);
        (body.ExpiresAt - body.CreatedAt).Should().Be(TimeSpan.FromHours(48));
        response.Headers.Location!.ToString().Should().EndWith("/api/v1/members/me/reservations");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Should().HaveCount(1);
        saved.Reservations.Single().MemberId.Should().Be(buyer.Id);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_SellerReservesOwnListing_When_Reserving_Then_Returns400OwnListing()
    {
        // Arrange
        var seller = Member.Create("Mihai Seller", "mihai@student.ro", CreatedAt).Value;
        // Build the seller as a Member with the fixed SellerId by reflection-free workaround:
        // we need the Member in the database with Id == SellerId. Use a listing whose seller
        // is the seeded member instead.
        var listing = Listing.Create(seller.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(seller, listing);
        AuthenticateAs(seller.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.OwnListing");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Should().BeEmpty();
        saved.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_DraftListing_When_Reserving_Then_Returns400NotActive()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotActive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Should().BeEmpty();
        saved.Status.Should().Be(ListingStatus.Draft);
    }

    [Fact]
    public async Task Given_MemberNotInDatabase_When_Reserving_Then_Returns404MemberNotFound()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(listing);
        AuthenticateAs(Guid.Parse("44444444-4444-4444-4444-444444444444"));

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownListing_When_Reserving_Then_Returns404ListingNotFound()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(buyer);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{Guid.NewGuid()}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var count = await QueryDbAsync(db => db.Set<Reservation>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_BuyerAlreadyHas3ActiveReservations_When_Reserving_Then_Returns400LimitReached()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;

        var reserved1 = Listing.Create(SellerId, "Listing 1", Category.Textbook, Condition.Good, 10m, CreatedAt).Value;
        reserved1.Publish();
        reserved1.Reserve(buyer.Id, CreatedAt);

        var reserved2 = Listing.Create(SellerId, "Listing 2", Category.Textbook, Condition.Good, 20m, CreatedAt.AddHours(1)).Value;
        reserved2.Publish();
        reserved2.Reserve(buyer.Id, CreatedAt.AddHours(1));

        var reserved3 = Listing.Create(SellerId, "Listing 3", Category.Textbook, Condition.Good, 30m, CreatedAt.AddHours(2)).Value;
        reserved3.Publish();
        reserved3.Reserve(buyer.Id, CreatedAt.AddHours(2));

        var fourth = Listing.Create(SellerId, "Listing 4", Category.Textbook, Condition.Good, 40m, CreatedAt.AddHours(3)).Value;
        fourth.Publish();

        await SeedAsync(buyer, reserved1, reserved2, reserved3, fourth);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{fourth.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.LimitReached");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == fourth.Id));
        saved.Reservations.Should().BeEmpty();
        saved.Status.Should().Be(ListingStatus.Active);
    }

    [Fact]
    public async Task Given_NoToken_When_Reserving_Then_Returns401()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        await SeedAsync(listing);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/reservations");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Should().BeEmpty();
    }
}
