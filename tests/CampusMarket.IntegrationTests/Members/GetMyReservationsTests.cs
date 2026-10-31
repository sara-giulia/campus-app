using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Members;

public sealed class GetMyReservationsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_NoToken_When_GettingMyReservations_Then_Returns401()
    {
        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_BuyerWithTwoReservations_When_Getting_Then_Returns200WithBothNewestFirst()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;

        var olderListing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        olderListing.Publish();
        var olderReservation = olderListing.Reserve(buyer.Id, CreatedAt).Value;

        var newerListing = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        newerListing.Publish();
        var newerReservation = newerListing.Reserve(buyer.Id, CreatedAt.AddHours(1)).Value;

        await SeedAsync(buyer, olderListing, newerListing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(2);
        body.Items.Select(i => i.ReservationId).Should().Equal(newerReservation.Id, olderReservation.Id);
        body.Items[0].ListingTitle.Should().Be("Calculator stiintific");
        body.Items[0].Price.Should().Be(120m);
        body.Items[1].ListingTitle.Should().Be("Manual POO");
        body.Items[1].Price.Should().Be(50m);
    }

    [Fact]
    public async Task Given_AnotherStudent_When_Getting_Then_Returns200WithEmptyList()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(buyer, listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(0);
        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ReservationCreated49hAgo_When_Getting_Then_StatusExpiredInResponseButActiveInDb()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow.AddHours(-49)).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Single().Status.Should().Be(ReservationStatus.Expired);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Reservations.Single().Id.Should().Be(reservation.Id);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }
}
