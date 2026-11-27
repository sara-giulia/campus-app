using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.SuspendMember;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class SuspendMemberTests : IntegrationTestBase
{
    private const string Reason = "Inselatorie repetata";
    private const string SuspendedSubject =
        "Contul tău CampusMarket a fost suspendat / Your CampusMarket account was suspended";

    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static string SuspendUrl(Guid memberId) => $"/api/v1/moderation/members/{memberId}/suspend";

    private static Member NewMember(string name, string email) => Member.Create(name, email, CreatedAt).Value;

    private static Listing ActiveListing(Guid sellerId, string title, decimal price = 100m)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, price, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    // A copy of the API whose emails are recorded; its client is logged in as the Admin.
    private (WebApplicationFactory<Program> Host, HttpClient Client) WithRecordedEmails(RecordingEmailSender sender)
    {
        var host = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(sender);
        }));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(AdminId, "Admin"));
        return (host, client);
    }

    private Task<Member> MemberAsync(Guid id) =>
        QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == id));

    private async Task AssertNotSuspendedAsync(Guid id)
    {
        var member = await MemberAsync(id);
        member.SuspendedAt.Should().BeNull();
        member.SuspendedUntil.Should().BeNull();
        member.SuspensionReason.Should().BeNull();
        member.SuspendedById.Should().BeNull();
    }

    private async Task<Error> PostAndReadErrorAsync(Guid memberId, SuspendMemberRequest request, HttpStatusCode expected)
    {
        var response = await Client.PostAsJsonAsync(SuspendUrl(memberId), request, Json);
        response.StatusCode.Should().Be(expected);
        return (await response.Content.ReadFromJsonAsync<Error>(Json))!;
    }

    [Fact]
    public async Task Given_Member_When_AdminSuspendsPermanently_Then_Returns204AndSavesTheSuspension()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");
        var before = DateTimeOffset.UtcNow;

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest("  " + Reason + "  ", null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var saved = await MemberAsync(ion.Id);
        saved.SuspendedAt.Should().NotBeNull();
        saved.SuspendedAt!.Value.Should().BeOnOrAfter(before.ToMicroseconds()).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        saved.SuspendedUntil.Should().BeNull();
        saved.SuspensionReason.Should().Be(Reason);
        saved.SuspendedById.Should().Be(AdminId);
    }

    [Fact]
    public async Task Given_Member_When_AdminSuspendsUntilADate_Then_Returns204AndSavesTheEnd()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");
        var until = DateTimeOffset.UtcNow.AddDays(7).ToMicroseconds();

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, until), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var saved = await MemberAsync(ion.Id);
        saved.SuspendedUntil.Should().Be(until);
        saved.SuspendedById.Should().Be(AdminId);
    }

    [Fact]
    public async Task Given_ReservationsOffersTokensAndReports_When_AdminSuspends_Then_AllAreClosedInOneCommitAndTheOtherSideIsNotified()
    {
        // Arrange
        var ion = BuildMember("Ion Ionescu", "ion@student.ro");
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var dan = NewMember("Dan Pop", "dan@student.ro");

        var ionReservedByAna = ActiveListing(ion.Id, "Manual POO");
        ionReservedByAna.Reserve(ana.Id, DateTimeOffset.UtcNow);
        var danReservedByIon = ActiveListing(dan.Id, "Calculator stiintific");
        danReservedByIon.Reserve(ion.Id, DateTimeOffset.UtcNow);
        var ionOffered = ActiveListing(ion.Id, "Notite analiza");
        var anaOfferToIon = Offer.Make(ionOffered, ana.Id, 90m, CreatedAt.AddMinutes(1)).Value;
        var danListing = ActiveListing(dan.Id, "Halat laborator");
        var ionOfferToDan = Offer.Make(danListing, ion.Id, 40m, CreatedAt.AddMinutes(2)).Value;
        var anaOfferToDan = Offer.Make(danListing, ana.Id, 45m, CreatedAt.AddMinutes(3)).Value;
        var unrelated = ActiveListing(dan.Id, "Manual chimie");
        unrelated.Reserve(ana.Id, DateTimeOffset.UtcNow);

        var ionToken = RefreshToken.Create(ion.Id, "ion-refresh-token", DateTimeOffset.UtcNow);
        var anaToken = RefreshToken.Create(ana.Id, "ana-refresh-token", DateTimeOffset.UtcNow);
        var reportOnIon = MemberReport.Create(ana.Id, ion.Id, "Cere bani in avans", null, CreatedAt).Value;
        var reportByIon = MemberReport.Create(ion.Id, dan.Id, "Nu raspunde", null, CreatedAt).Value;

        await SeedAsync(ion, ana, dan, ionReservedByAna, danReservedByIon, ionOffered, danListing, unrelated,
            anaOfferToIon, ionOfferToDan, anaOfferToDan, ionToken, anaToken, reportOnIon, reportByIon);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listings = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().Include(l => l.Reservations).ToDictionaryAsync(l => l.Id));
        listings[ionReservedByAna.Id].Status.Should().Be(ListingStatus.Active);
        listings[ionReservedByAna.Id].Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
        listings[danReservedByIon.Id].Status.Should().Be(ListingStatus.Active);
        listings[danReservedByIon.Id].Reservations.Single().Status.Should().Be(ReservationStatus.Cancelled);
        listings[unrelated.Id].Status.Should().Be(ListingStatus.Reserved);
        listings[unrelated.Id].Reservations.Single().Status.Should().Be(ReservationStatus.Active);
        listings[ionOffered.Id].Status.Should().Be(ListingStatus.Active);
        listings[ionOffered.Id].Version.Should().Be(ionOffered.Version);

        var offers = await QueryDbAsync(db => db.Set<Offer>().AsNoTracking().ToDictionaryAsync(o => o.Id));
        offers[anaOfferToIon.Id].Status.Should().Be(OfferStatus.Rejected);
        offers[anaOfferToIon.Id].RespondedAt.Should().NotBeNull();
        offers[ionOfferToDan.Id].Status.Should().Be(OfferStatus.Rejected);
        offers[anaOfferToDan.Id].Status.Should().Be(OfferStatus.Pending);

        var notifications = await QueryDbAsync(db => db.Set<Notification>().AsNoTracking()
            .Select(n => new { n.MemberId, n.Type, n.Message, n.ListingId }).ToListAsync());
        notifications.Should().BeEquivalentTo(new[]
        {
            new { MemberId = ana.Id, Type = NotificationType.ReservationCancelled,
                Message = "Your reservation for Manual POO was cancelled: the seller is no longer available", ListingId = (Guid?)ionReservedByAna.Id },
            new { MemberId = dan.Id, Type = NotificationType.ReservationCancelled,
                Message = "The reservation of Calculator stiintific was cancelled: the buyer is no longer available", ListingId = (Guid?)danReservedByIon.Id },
            new { MemberId = ana.Id, Type = NotificationType.OfferRejected,
                Message = "The offer of 90 lei for Notite analiza was closed: the other member is no longer available", ListingId = (Guid?)ionOffered.Id },
            new { MemberId = dan.Id, Type = NotificationType.OfferRejected,
                Message = "The offer of 40 lei for Halat laborator was closed: the other member is no longer available", ListingId = (Guid?)danListing.Id },
        });

        var tokens = await QueryDbAsync(db => db.Set<RefreshToken>().AsNoTracking().ToDictionaryAsync(t => t.Id));
        tokens[ionToken.Id].RevokedAt.Should().NotBeNull();
        tokens[anaToken.Id].RevokedAt.Should().BeNull();

        var reports = await QueryDbAsync(db => db.Set<MemberReport>().AsNoTracking().ToDictionaryAsync(r => r.Id));
        reports[reportOnIon.Id].Resolution.Should().Be(ReportResolution.Confirmed);
        reports[reportOnIon.Id].ResolvedById.Should().Be(AdminId);
        reports[reportOnIon.Id].ResolvedAt.Should().NotBeNull();
        reports[reportByIon.Id].Resolution.Should().Be(ReportResolution.Pending);
    }

    [Fact]
    public async Task Given_PermanentSuspension_When_AdminSuspends_Then_TheBilingualEmailIsSentWithoutTheReport()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var report = MemberReport.Create(ana.Id, ion.Id, "Cere bani in avans", null, CreatedAt).Value;
        await SeedAsync(ion, ana, report);
        var sender = new RecordingEmailSender();
        var (host, client) = WithRecordedEmails(sender);
        await using var _ = host;
        using var __ = client;

        // Act
        var response = await client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var email = sender.Sent.Should().ContainSingle().Subject;
        email.To.Should().Be("ion@student.ro");
        email.Subject.Should().Be(SuspendedSubject);
        email.Body.Should().StartWith("Salut Ion Ionescu,");
        email.Body.Should().Contain("Contul tău CampusMarket a fost suspendat definitiv.");
        email.Body.Should().Contain("Motiv: " + Reason);
        email.Body.Should().Contain("Your CampusMarket account was suspended permanently.");
        email.Body.Should().Contain("Reason: " + Reason);
        email.Body.IndexOf("suspendat definitiv", StringComparison.Ordinal)
            .Should().BeLessThan(email.Body.IndexOf("----------", StringComparison.Ordinal));
        email.Body.IndexOf("----------", StringComparison.Ordinal)
            .Should().BeLessThan(email.Body.IndexOf("Hello Ion Ionescu,", StringComparison.Ordinal));
        email.Body.Should().NotContain("Ana Popescu").And.NotContain("Cere bani in avans");

        (await MemberAsync(ion.Id)).SuspendedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Given_TemporarySuspension_When_AdminSuspends_Then_TheEmailHasTheEndInRomaniaTime()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var now = DateTimeOffset.UtcNow;
        var until = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero).AddDays(3);
        var romania = TimeZoneInfo.ConvertTime(until, TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"))
            .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        var sender = new RecordingEmailSender();
        var (host, client) = WithRecordedEmails(sender);
        await using var _ = host;
        using var __ = client;

        // Act
        var response = await client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, until), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var email = sender.Sent.Should().ContainSingle().Subject;
        email.Subject.Should().Be(SuspendedSubject);
        email.Body.Should().Contain($"Contul tău CampusMarket a fost suspendat până la {romania} (ora României).");
        email.Body.Should().Contain($"Your CampusMarket account was suspended until {romania} (Romania time).");
        email.Body.IndexOf("ora României", StringComparison.Ordinal)
            .Should().BeLessThan(email.Body.IndexOf("Romania time", StringComparison.Ordinal));

        (await MemberAsync(ion.Id)).SuspendedUntil.Should().Be(until);
    }

    [Fact]
    public async Task Given_EmailSenderFails_When_AdminSuspends_Then_Returns204AndTheSuspensionIsKept()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        var sender = new RecordingEmailSender { Throws = true };
        var (host, client) = WithRecordedEmails(sender);
        await using var _ = host;
        using var __ = client;

        // Act
        var response = await client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        sender.Attempts.Should().Be(1);
        sender.Sent.Should().BeEmpty();
        (await MemberAsync(ion.Id)).SuspensionReason.Should().Be(Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Given_EmptyReason_When_AdminSuspends_Then_Returns400ReasonEmpty(string? reason)
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(ion.Id, new SuspendMemberRequest(reason, null), HttpStatusCode.BadRequest);

        // Assert
        error.Code.Should().Be("Suspension.ReasonEmpty");
        error.Type.Should().Be(ErrorType.Validation);
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_ReasonOf501Characters_When_AdminSuspends_Then_Returns400ReasonTooLong()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(ion.Id, new SuspendMemberRequest(new string('a', 501), null), HttpStatusCode.BadRequest);

        // Assert
        error.Code.Should().Be("Suspension.ReasonTooLong");
        error.Type.Should().Be(ErrorType.Validation);
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_UntilInThePast_When_AdminSuspends_Then_Returns400UntilInPast()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(
            ion.Id, new SuspendMemberRequest(Reason, DateTimeOffset.UtcNow.AddMinutes(-1)), HttpStatusCode.BadRequest);

        // Assert
        error.Code.Should().Be("Suspension.UntilInPast");
        error.Type.Should().Be(ErrorType.Validation);
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_UntilMoreThan365DaysAway_When_AdminSuspends_Then_Returns400TooLong()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(
            ion.Id, new SuspendMemberRequest(Reason, DateTimeOffset.UtcNow.AddDays(366)), HttpStatusCode.BadRequest);

        // Assert
        error.Code.Should().Be("Suspension.TooLong");
        error.Type.Should().Be(ErrorType.Validation);
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_AdminSuspendsThemself_When_Suspending_Then_Returns403Self()
    {
        // Arrange
        var admin = Member.Register("Admin", "admin@campus.test", new AspNetPasswordHasher().Hash("admin-secret12"), CreatedAt, MemberRole.Admin).Value;
        await SeedAsync(admin);
        AuthenticateAs(admin.Id, "Admin");

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(admin.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Suspension.Self");
        error.Type.Should().Be(ErrorType.Forbidden);
        await AssertNotSuspendedAsync(admin.Id);
    }

    [Fact]
    public async Task Given_AnotherAdmin_When_Suspending_Then_Returns403CannotSuspendAdmin()
    {
        // Arrange
        var otherAdmin = Member.Register("Admin Doi", "admin2@campus.test", new AspNetPasswordHasher().Hash("admin-secret12"), CreatedAt, MemberRole.Admin).Value;
        await SeedAsync(otherAdmin);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(otherAdmin.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.CannotSuspendAdmin");
        error.Type.Should().Be(ErrorType.Forbidden);
        await AssertNotSuspendedAsync(otherAdmin.Id);
    }

    [Fact]
    public async Task Given_AlreadySuspended_When_AdminSuspendsAgain_Then_Returns409AndTheFirstSuspensionStays()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var now = DateTimeOffset.UtcNow.ToMicroseconds();
        ion.Suspend(AdminId, "Prima suspendare", null, now);
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(ion.Id, new SuspendMemberRequest(Reason, null), HttpStatusCode.Conflict);

        // Assert
        error.Code.Should().Be("Member.AlreadySuspended");
        error.Type.Should().Be(ErrorType.Conflict);
        var saved = await MemberAsync(ion.Id);
        saved.SuspensionReason.Should().Be("Prima suspendare");
        saved.SuspendedAt.Should().Be(now);
    }

    [Fact]
    public async Task Given_UnknownMember_When_AdminSuspends_Then_Returns404()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var error = await PostAndReadErrorAsync(Guid.NewGuid(), new SuspendMemberRequest(Reason, null), HttpStatusCode.NotFound);

        // Assert
        error.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_Student_When_Suspending_Then_Returns403ProblemDetails()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        await SeedAsync(ion, ana);
        AuthenticateAs(ana.Id);

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Forbidden");
        await AssertNotSuspendedAsync(ion.Id);
    }

    [Fact]
    public async Task Given_NoToken_When_Suspending_Then_Returns401ProblemDetails()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);

        // Act
        var response = await Client.PostAsJsonAsync(SuspendUrl(ion.Id), new SuspendMemberRequest(Reason, null), Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        await AssertNotSuspendedAsync(ion.Id);
    }

    private static Member BuildMember(string name, string email) =>
        Member.Register(name, email, new AspNetPasswordHasher().Hash("secret12"), CreatedAt).Value;
}
