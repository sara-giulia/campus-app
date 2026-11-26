using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.UpdateListing;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class ConfirmSaleTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_Seller_When_Confirming_Then_Returns204AndListingSold()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        var versionBefore = listing.Version;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Version.Should().Be(versionBefore + 1);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task Given_SoldListing_When_BuyerGetsMyReservations_Then_ReservationIsCompleted()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.GetAsync("/api/v1/members/me/reservations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetMyReservationsResponse>(Json);
        body!.Count.Should().Be(1);
        body.Items.Single().ReservationId.Should().Be(reservation.Id);
        body.Items.Single().Status.Should().Be(ReservationStatus.Completed);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task Given_AdminWhoIsNotSeller_When_Confirming_Then_Returns204()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task Given_Buyer_When_Confirming_Then_Returns403NotOwner()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_AnotherMember_When_Confirming_Then_Returns403NotOwner()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotOwner");
        error.Type.Should().Be(ErrorType.Forbidden);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_NoToken_When_Confirming_Then_Returns401()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_UnknownListingId_When_Confirming_Then_Returns404ListingNotFound()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{Guid.NewGuid()}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_UnknownReservationId_When_Confirming_Then_Returns404ReservationNotFound()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Reserve(buyer.Id, DateTimeOffset.UtcNow);
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{Guid.NewGuid()}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_ReservationAlreadyCompleted_When_ConfirmingAgain_Then_Returns400NotActive()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.NotActive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task Given_ReservationCreated49hAgo_When_Confirming_Then_Returns400Expired()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow.AddHours(-49)).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Reservation.Expired");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Reserved);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
    }

    [Fact]
    public async Task Given_SoldListing_When_Updating_Then_Returns400ListingSold()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);
        var request = new UpdateListingRequest("Notite POO", Category.Notes, Condition.Worn, 30m);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/v1/listings/{listing.Id}", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.Sold");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == listing.Id));
        saved.Title.Should().Be("Manual POO");
        saved.Category.Should().Be(Category.Textbook);
        saved.Condition.Should().Be(Condition.Good);
        saved.Price.Amount.Should().Be(50m);
        saved.Status.Should().Be(ListingStatus.Sold);
    }

    [Fact]
    public async Task Given_SoldListing_When_AnotherMemberReserves_Then_Returns400NotActive()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var otherMember = Member.Create("Ion Georgescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
        await SeedAsync(buyer, otherMember, listing);
        AuthenticateAs(otherMember.Id);

        // Act
        var response = await Client.PostAsync($"/api/v1/listings/{listing.Id}/reservations", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotActive");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Reservations.Should().HaveCount(1);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Completed);
    }

    [Fact]
    public async Task Given_ReservationWithAgreedPrice_When_SellerConfirms_Then_SaleRecordHasTheAgreedPrice()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow, Money.Create(40m).Value).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var record = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        record.ListingId.Should().Be(listing.Id);
        record.SoldPrice.Should().Be(40m);
        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking()
            .Include(l => l.Reservations).SingleAsync(l => l.Id == listing.Id));
        saved.Status.Should().Be(ListingStatus.Sold);
        saved.Reservations.Single().AgreedPrice.Should().Be(40m);
    }

    [Fact]
    public async Task Given_ReservationWithoutAgreedPrice_When_SellerConfirms_Then_SaleRecordHasTheListingPrice()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var reservation = listing.Reserve(buyer.Id, DateTimeOffset.UtcNow).Value;
        await SeedAsync(buyer, listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync(
            $"/api/v1/listings/{listing.Id}/reservations/{reservation.Id}/confirm", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var record = await QueryDbAsync(db => db.Set<SaleRecord>().AsNoTracking().SingleAsync());
        record.SoldPrice.Should().Be(50m);
    }
}
