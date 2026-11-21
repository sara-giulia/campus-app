using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class ReservationExpiryTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Starts a copy of the API on the same in-memory database with the expiry job turned on.
    private WebApplicationFactory<Program> WithExpiryJob() =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ReservationExpiry:Enabled", "true");
            builder.UseSetting("ReservationExpiry:Interval", "00:00:00.200");
        });

    private async Task<ReservationStatus> WaitForReservationStatusAsync(
        Guid reservationId, ReservationStatus expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        ReservationStatus current = default;
        while (DateTime.UtcNow < deadline)
        {
            current = await QueryDbAsync(db => db.Set<Reservation>().AsNoTracking()
                .Where(r => r.Id == reservationId)
                .Select(r => r.Status)
                .SingleAsync());
            if (current == expected)
                return current;
            await Task.Delay(100);
        }
        return current;
    }

    [Fact]
    public async Task Given_JobOnWithExpiredAndValidReservations_When_JobRuns_Then_ExpiresOnlyTheExpiredOne()
    {
        // Arrange
        var expiredBuyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var validBuyer = Member.Create("Mihai Ionescu", "mihai@student.ro", CreatedAt).Value;

        var expiredListing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        expiredListing.Publish();
        var expiredReservation = expiredListing.Reserve(expiredBuyer.Id, DateTimeOffset.UtcNow.AddHours(-49)).Value;
        var expiredVersionBefore = expiredListing.Version;

        var validListing = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        validListing.Publish();
        var validReservation = validListing.Reserve(validBuyer.Id, DateTimeOffset.UtcNow.AddHours(-1)).Value;

        await SeedAsync(expiredBuyer, validBuyer, expiredListing, validListing);

        // Act: start the job, wait until it has expired the stale reservation,
        // then stop the host. The shared in-memory SQLite connection would
        // otherwise be contended between further background ticks and the
        // assertion-time reads below ("SQLite Error 5: database is locked").
        ReservationStatus finalStatus;
        await using (var seeded = WithExpiryJob())
        {
            seeded.CreateClient();
            finalStatus = await WaitForReservationStatusAsync(
                expiredReservation.Id, ReservationStatus.Expired, TimeSpan.FromSeconds(5));
        }

        // Assert
        finalStatus.Should().Be(
            ReservationStatus.Expired,
            "the background job must expire the stale reservation within 5 seconds");

        var expiredSaved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations)
            .SingleAsync(l => l.Id == expiredListing.Id));
        expiredSaved.Status.Should().Be(ListingStatus.Active);
        expiredSaved.Version.Should().Be(expiredVersionBefore + 1);

        var validSaved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations)
            .SingleAsync(l => l.Id == validListing.Id));
        validSaved.Status.Should().Be(ListingStatus.Reserved);
        validSaved.Reservations.Single().Id.Should().Be(validReservation.Id);
        validSaved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_JobExpiredTheReservation_When_AnotherMemberReserves_Then_Returns201()
    {
        // Arrange
        var firstBuyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var secondBuyer = Member.Create("Mihai Ionescu", "mihai@student.ro", CreatedAt).Value;

        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish();
        var staleReservation = listing.Reserve(firstBuyer.Id, DateTimeOffset.UtcNow.AddHours(-49)).Value;

        await SeedAsync(firstBuyer, secondBuyer, listing);

        // Start the job and wait for the expiry. Leaving the block stops the job,
        // so no background tick runs on the shared in-memory SQLite connection
        // during the Act and the assertions ("database is locked").
        ReservationStatus finalStatus;
        await using (var withJob = WithExpiryJob())
        {
            withJob.CreateClient();
            finalStatus = await WaitForReservationStatusAsync(
                staleReservation.Id, ReservationStatus.Expired, TimeSpan.FromSeconds(5));
        }

        finalStatus.Should().Be(
            ReservationStatus.Expired,
            "the background job must expire the stale reservation within 5 seconds");

        AuthenticateAs(secondBuyer.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations)
            .SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Should().HaveCount(2);
        saved.Reservations.Should().ContainSingle(r => r.MemberId == secondBuyer.Id && r.Status == ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_JobOff_When_ReservationIsOlderThan48h_Then_StaysActiveInDbButResponseShowsExpired()
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

        var saved = await QueryDbAsync(db => db.Set<Reservation>().AsNoTracking()
            .SingleAsync(r => r.Id == reservation.Id));
        saved.Status.Should().Be(ReservationStatus.Active);
    }
}
