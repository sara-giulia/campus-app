using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.ReportMember;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class ReportMemberTests : IntegrationTestBase
{
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static string ReportUrl(Guid memberId) => $"/api/v1/members/{memberId}/reports";

    private static Member NewMember(string name, string email) => Member.Create(name, email, CreatedAt).Value;

    private static Listing PublishedListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    private Task<List<MemberReport>> ReportsAsync() =>
        QueryDbAsync(db => db.Set<MemberReport>().AsNoTracking().ToListAsync());

    [Fact]
    public async Task Given_ValidReason_When_ReportingMember_Then_Returns201AndSavesTrimmedReason()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reporter, reported);
        AuthenticateAs(reporter.Id);
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(reported.Id), new ReportMemberRequest("  Cere bani in avans  ", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().BeNull();

        var body = await response.Content.ReadFromJsonAsync<ReportMemberResponse>(Json);
        body!.ReportedMemberId.Should().Be(reported.Id);
        body.CreatedAt.Should().BeOnOrAfter(before.ToMicroseconds()).And.BeOnOrBefore(DateTimeOffset.UtcNow);

        var saved = (await ReportsAsync()).Should().ContainSingle().Subject;
        saved.Id.Should().Be(body.Id);
        saved.ReporterId.Should().Be(reporter.Id);
        saved.ReportedMemberId.Should().Be(reported.Id);
        saved.Reason.Should().Be("Cere bani in avans");
        saved.ConversationId.Should().BeNull();
        saved.Resolution.Should().Be(ReportResolution.Pending);
        saved.ResolvedAt.Should().BeNull();
        saved.CreatedAt.Should().Be(body.CreatedAt.ToMicroseconds());
    }

    [Fact]
    public async Task Given_ConversationBetweenTheTwo_When_ReportingMember_Then_Returns201AndSavesTheConversation()
    {
        // Arrange
        var seller = NewMember("Ion Ionescu", "ion@student.ro");
        var buyer = NewMember("Ana Popescu", "ana@student.ro");
        var listing = PublishedListing(seller.Id);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, listing, conversation);
        AuthenticateAs(buyer.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(seller.Id), new ReportMemberRequest("Mesaje jignitoare", conversation.Id), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ReportMemberResponse>(Json);
        body!.ReportedMemberId.Should().Be(seller.Id);

        var saved = (await ReportsAsync()).Should().ContainSingle().Subject;
        saved.ConversationId.Should().Be(conversation.Id);
        saved.ReporterId.Should().Be(buyer.Id);
    }

    [Theory]
    [InlineData("", "MemberReport.ReasonEmpty")]
    [InlineData("   ", "MemberReport.ReasonEmpty")]
    [InlineData(null, "MemberReport.ReasonEmpty")]
    public async Task Given_EmptyReason_When_ReportingMember_Then_Returns400AndSavesNothing(string? reason, string code)
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reporter, reported);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reported.Id), new ReportMemberRequest(reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(ErrorType.Validation);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ReasonOf501Characters_When_ReportingMember_Then_Returns400ReasonTooLong()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reporter, reported);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(reported.Id), new ReportMemberRequest(new string('a', 501), null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("MemberReport.ReasonTooLong");
        error.Type.Should().Be(ErrorType.Validation);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ReasonOf500Characters_When_ReportingMember_Then_Returns201()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reporter, reported);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(reported.Id), new ReportMemberRequest(new string('a', 500), null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReportsAsync()).Should().ContainSingle().Which.Reason.Should().HaveLength(500);
    }

    [Fact]
    public async Task Given_OwnId_When_ReportingMember_Then_Returns400Self()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        await SeedAsync(reporter);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reporter.Id), new ReportMemberRequest("Test", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("MemberReport.Self");
        error.Type.Should().Be(ErrorType.Validation);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownConversation_When_ReportingMember_Then_Returns400ConversationInvalid()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reporter, reported);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(reported.Id), new ReportMemberRequest("Mesaje jignitoare", Guid.NewGuid()), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("MemberReport.ConversationInvalid");
        error.Type.Should().Be(ErrorType.Validation);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ConversationOfOtherMembers_When_ReportingMember_Then_Returns400ConversationInvalid()
    {
        // Arrange
        var seller = NewMember("Ion Ionescu", "ion@student.ro");
        var buyer = NewMember("Ana Popescu", "ana@student.ro");
        var reporter = NewMember("Mihai Popa", "mihai@student.ro");
        var listing = PublishedListing(seller.Id);
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", CreatedAt).Value;
        await SeedAsync(seller, buyer, reporter, listing, conversation);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(
            ReportUrl(seller.Id), new ReportMemberRequest("Mesaje jignitoare", conversation.Id), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("MemberReport.ConversationInvalid");
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownMember_When_ReportingMember_Then_Returns404()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        await SeedAsync(reporter);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(Guid.NewGuid()), new ReportMemberRequest("Test", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ReporterNotInDatabase_When_ReportingMember_Then_Returns404()
    {
        // Arrange
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reported);
        AuthenticateAs(Guid.Parse("44444444-4444-4444-4444-444444444444"));

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reported.Id), new ReportMemberRequest("Test", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PendingReportOfSamePair_When_ReportingAgain_Then_Returns409AndStillOneReport()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        var existing = MemberReport.Create(reporter.Id, reported.Id, "Prima data", null, CreatedAt).Value;
        await SeedAsync(reporter, reported, existing);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reported.Id), new ReportMemberRequest("A doua oara", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("MemberReport.AlreadyExists");
        error.Type.Should().Be(ErrorType.Conflict);
        var saved = (await ReportsAsync()).Should().ContainSingle().Subject;
        saved.Id.Should().Be(existing.Id);
        saved.Reason.Should().Be("Prima data");
    }

    [Fact]
    public async Task Given_PreviousReportDismissed_When_ReportingAgain_Then_Returns201AndSavesANewPendingReport()
    {
        // Arrange
        var reporter = NewMember("Ana Popescu", "ana@student.ro");
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        var dismissed = MemberReport.Create(reporter.Id, reported.Id, "Prima data", null, CreatedAt).Value;
        dismissed.Dismiss(AdminId, CreatedAt.AddHours(1));
        await SeedAsync(reporter, reported, dismissed);
        AuthenticateAs(reporter.Id);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reported.Id), new ReportMemberRequest("A doua oara", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reports = await ReportsAsync();
        reports.Should().HaveCount(2);
        reports.Single(r => r.Id == dismissed.Id).Resolution.Should().Be(ReportResolution.Dismissed);
        reports.Single(r => r.Id != dismissed.Id).Resolution.Should().Be(ReportResolution.Pending);
    }

    [Fact]
    public async Task Given_NoToken_When_ReportingMember_Then_Returns401()
    {
        // Arrange
        var reported = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(reported);

        // Act
        var response = await Client.PostAsJsonAsync(ReportUrl(reported.Id), new ReportMemberRequest("Test", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        (await ReportsAsync()).Should().BeEmpty();
    }
}
