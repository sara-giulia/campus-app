using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.GetReportedListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class GetReportedListingsTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_UserAndAutomaticReports_When_AdminGetsReportedListings_Then_Returns200WithBoth()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var ion = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var textbook = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        textbook.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var spam = Listing.Create(SellerId, "BANI RAPIZI", Category.Other, Condition.Good, 1m, CreatedAt).Value;
        spam.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var olderReport = Report.Create(ana.Id, textbook, "Fake listing", CreatedAt.AddHours(1)).Value;
        var newerReport = Report.Create(ion.Id, textbook, "Wrong price", CreatedAt.AddHours(2)).Value;
        var automatic = Report.CreateAutomatic(spam.Id, 0.9, CreatedAt.AddHours(3));
        await SeedAsync(ana, ion, textbook, spam, olderReport, newerReport, automatic);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetReportedListingsResponse>(Json);
        body!.Count.Should().Be(2);
        body.TotalCount.Should().Be(2);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
        body.Items.Select(i => i.ListingId).Should().Equal(textbook.Id, spam.Id);

        var first = body.Items[0];
        first.Title.Should().Be("Manual POO");
        first.SellerId.Should().Be(SellerId);
        first.Status.Should().Be(ListingStatus.Active);
        first.ReportCount.Should().Be(2);
        first.FlaggedAsSpam.Should().BeFalse();
        first.SpamProbability.Should().BeNull();
        first.LastReportedAt.Should().Be(newerReport.CreatedAt);
        first.Reasons.Should().Equal("Wrong price", "Fake listing");

        var second = body.Items[1];
        second.ReportCount.Should().Be(1);
        second.FlaggedAsSpam.Should().BeTrue();
        second.SpamProbability.Should().Be(0.9);
        second.LastReportedAt.Should().Be(automatic.CreatedAt);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(3);
    }

    [Fact]
    public async Task Given_HiddenReportedListing_When_AdminGetsReportedListings_Then_ItIsNotReturned()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var visible = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        visible.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var hidden = Listing.Create(SellerId, "BANI RAPIZI", Category.Other, Condition.Good, 1m, CreatedAt).Value;
        hidden.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var visibleReport = Report.Create(ana.Id, visible, "Fake listing", CreatedAt.AddHours(1)).Value;
        var hiddenReport = Report.Create(ana.Id, hidden, "Spam", CreatedAt.AddHours(2)).Value;
        hidden.Hide();
        await SeedAsync(ana, visible, hidden, visibleReport, hiddenReport);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetReportedListingsResponse>(Json);
        body!.Items.Select(i => i.ListingId).Should().Equal(visible.Id);
        body.Count.Should().Be(1);
        body.TotalCount.Should().Be(1);

        var count = await QueryDbAsync(db => db.Set<Report>().CountAsync());
        count.Should().Be(2);
    }

    [Theory]
    [InlineData("?page=0", "Moderation.InvalidPage")]
    [InlineData("?pageSize=101", "Moderation.InvalidPageSize")]
    public async Task Given_InvalidPaging_When_AdminGetsReportedListings_Then_Returns400(string query, string expectedCode)
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var report = Report.Create(ana.Id, listing, "Fake listing", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync($"/api/v1/moderation/listings{query}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(expectedCode);
        error.Type.Should().Be(ErrorType.Validation);

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task Given_Student_When_GettingReportedListings_Then_Returns403()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var report = Report.Create(ana.Id, listing, "Fake listing", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Code.Should().Be("Auth.Forbidden");
        problem.Instance.Should().Be("/api/v1/moderation/listings");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingReportedListings_Then_Returns401()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        var report = Report.Create(ana.Id, listing, "Fake listing", CreatedAt.AddHours(1)).Value;
        await SeedAsync(ana, listing, report);

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be("/api/v1/moderation/listings");
        problem.TraceId.Should().NotBeEmpty();

        var saved = await QueryDbAsync(db => db.Set<Report>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(report);
    }
}
