using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class HideListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_PublishedListing_When_AdminHidesListing_Then_Returns204AndSavesHidden()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Hidden);
        saved.Version.Should().Be(3);
    }

    [Fact]
    public async Task Given_ReservedListing_When_AdminHidesListing_Then_Returns204AndCancelsReservation()
    {
        // Arrange
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Reserve(buyer.Id, CreatedAt);
        await SeedAsync(buyer, listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).SingleAsync());
        saved.Status.Should().Be(ListingStatus.Hidden);
        saved.Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Given_HiddenListing_When_AdminHidesListing_Then_Returns400AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        listing.Hide();
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.AlreadyHidden");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Hidden);
        saved.Version.Should().Be(3);
    }

    [Fact]
    public async Task Given_DraftListing_When_AdminHidesListing_Then_Returns400AndKeepsDraft()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.CannotHide");
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Draft);
        saved.Version.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_AdminHidesListing_Then_Returns404AndKeepsSeededListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");
        var unknownId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{unknownId}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_SellerAsStudent_When_HidingListing_Then_Returns403AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        AuthenticateAs(SellerId);

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Code.Should().Be("Auth.Forbidden");
        problem.Instance.Should().Be($"/api/v1/moderation/listings/{listing.Id}/hide");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_NoToken_When_HidingListing_Then_Returns401AndKeepsListing()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be($"/api/v1/moderation/listings/{listing.Id}/hide");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Active);
        saved.Version.Should().Be(2);
    }

    [Fact]
    public async Task Given_HiddenListing_When_GettingListings_Then_ItIsNotReturned()
    {
        // Arrange
        var visible = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        visible.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var hidden = Listing.Create(SellerId, "Calculator stiintific", Category.Electronics, Condition.New, 120m, CreatedAt.AddHours(1)).Value;
        hidden.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        hidden.Hide();
        await SeedAsync(visible, hidden);

        // Act
        var response = await Client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.Items.Select(i => i.Id).Should().Equal(visible.Id);
        body.TotalCount.Should().Be(1);

        var savedHidden = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync(l => l.Id == hidden.Id));
        savedHidden.Status.Should().Be(ListingStatus.Hidden);
    }

    [Fact]
    public async Task Given_ReportedListing_When_AdminHidesListing_Then_ConfirmsReportsAndRecordsSpamExample()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "BANI RAPIZI", Category.Other, Condition.Good, 1m, CreatedAt, "Suna acum!").Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var userReport = Report.Create(ana.Id, listing, "Spam", CreatedAt.AddHours(1)).Value;
        var automatic = Report.CreateAutomatic(listing.Id, 0.9, CreatedAt.AddHours(2));
        await SeedAsync(ana, listing, userReport, automatic);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Hidden);

        var reports = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().ToListAsync());
        reports.Should().HaveCount(2);
        reports.Should().OnlyContain(r => r.Resolution == ReportResolution.Confirmed && r.ResolvedAt != null);

        var example = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Spam);
        example.Label.Should().Be(TrainingExample.SpamLabel);
        example.Source.Should().Be(TrainingExampleSource.Report);
        example.Title.Should().Be("BANI RAPIZI");
        example.Description.Should().Be("Suna acum!");
        example.CreatedBy.Should().Be(AdminId);
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_ListingWithoutReports_When_AdminHidesListing_Then_Returns204AndRecordsNoExample()
    {
        // Arrange
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        await SeedAsync(listing);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Status.Should().Be(ListingStatus.Hidden);
        var examples = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        examples.Should().Be(0);
    }

    [Fact]
    public async Task Given_ListingDismissedBefore_When_AdminHidesListingAfterNewReport_Then_RelabelsTheSameExample()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var oldReport = Report.Create(ana.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        oldReport.Dismiss(CreatedAt.AddHours(2));
        var notSpam = TrainingExample.ForSpamDecision(listing, isSpam: false, AdminId, CreatedAt.AddHours(2)).Value;
        var newReport = Report.Create(ion.Id, listing, "Vinde altceva", CreatedAt.AddHours(3)).Value;
        await SeedAsync(ana, ion, listing, oldReport, notSpam, newReport);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync($"/api/v1/moderation/listings/{listing.Id}/hide", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var example = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        example.Id.Should().Be(notSpam.Id);
        example.Label.Should().Be(TrainingExample.SpamLabel);
        example.CreatedAt.Should().Be(CreatedAt.AddHours(2));
        example.UpdatedAt.Should().NotBeNull();

        var reports = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().ToListAsync());
        reports.Single(r => r.Id == oldReport.Id).Resolution.Should().Be(ReportResolution.Dismissed);
        reports.Single(r => r.Id == newReport.Id).Resolution.Should().Be(ReportResolution.Confirmed);
    }
}
