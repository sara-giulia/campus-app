using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.GetReportedMembers;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class DismissMemberReportsTests : IntegrationTestBase
{
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static string DismissUrl(Guid memberId) => $"/api/v1/moderation/members/{memberId}/dismiss-reports";

    private static Member NewMember(string name, string email) => Member.Create(name, email, CreatedAt).Value;

    private static MemberReport NewReport(Member reporter, Member reported, int hours) =>
        MemberReport.Create(reporter.Id, reported.Id, "Anunt fals", null, CreatedAt.AddHours(hours)).Value;

    private Task<List<MemberReport>> ReportsAsync() =>
        QueryDbAsync(db => db.Set<MemberReport>().AsNoTracking().ToListAsync());

    [Fact]
    public async Task Given_PendingReports_When_AdminDismisses_Then_Returns204AndOnlyThatMembersPendingReportsAreDismissed()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var mihai = NewMember("Mihai Popa", "mihai@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var first = NewReport(ana, ion, 1);
        var second = NewReport(mihai, ion, 2);
        var confirmedBefore = NewReport(ana, ion, 0);
        confirmedBefore.Confirm(OtherStudentId, CreatedAt.AddMinutes(30));
        var otherMember = NewReport(ion, ana, 3);
        await SeedAsync(ana, mihai, ion, confirmedBefore, first, second, otherMember);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var reports = (await ReportsAsync()).ToDictionary(r => r.Id);
        reports[first.Id].Resolution.Should().Be(ReportResolution.Dismissed);
        reports[first.Id].ResolvedAt.Should().NotBeNull();
        reports[first.Id].ResolvedById.Should().Be(AdminId);
        reports[second.Id].Resolution.Should().Be(ReportResolution.Dismissed);
        reports[second.Id].ResolvedAt.Should().Be(reports[first.Id].ResolvedAt);
        reports[second.Id].ResolvedById.Should().Be(AdminId);

        reports[confirmedBefore.Id].Resolution.Should().Be(ReportResolution.Confirmed);
        reports[confirmedBefore.Id].ResolvedById.Should().Be(OtherStudentId);
        reports[confirmedBefore.Id].ResolvedAt.Should().Be(CreatedAt.AddMinutes(30));
        reports[otherMember.Id].Resolution.Should().Be(ReportResolution.Pending);

        var member = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().SingleAsync(m => m.Id == ion.Id));
        member.SuspendedAt.Should().BeNull();
        member.Name.Should().Be("Ion Ionescu");
    }

    [Fact]
    public async Task Given_DismissedReports_When_AdminGetsReportedMembers_Then_MemberIsNotListed()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ana, ion, NewReport(ana, ion, 1));
        AuthenticateAs(AdminId, "Admin");
        var dismiss = await Client.PostAsync(DismissUrl(ion.Id), content: null);
        dismiss.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await Client.GetAsync("/api/v1/moderation/members");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);
        (await ReportsAsync()).Should().ContainSingle().Which.Resolution.Should().Be(ReportResolution.Dismissed);
    }

    [Fact]
    public async Task Given_MemberWithoutReports_When_AdminDismisses_Then_Returns400NoPendingReports()
    {
        // Arrange
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ion);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.NoPendingReports");
        error.Type.Should().Be(ErrorType.Validation);
        (await ReportsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OnlyResolvedReports_When_AdminDismisses_Then_Returns400AndReportUnchanged()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var dismissed = NewReport(ana, ion, 1);
        dismissed.Dismiss(OtherStudentId, CreatedAt.AddHours(2));
        await SeedAsync(ana, ion, dismissed);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Report.NoPendingReports");
        var saved = (await ReportsAsync()).Should().ContainSingle().Subject;
        saved.ResolvedById.Should().Be(OtherStudentId);
        saved.ResolvedAt.Should().Be(CreatedAt.AddHours(2));
    }

    [Fact]
    public async Task Given_UnknownMember_When_AdminDismisses_Then_Returns404()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ana, ion, NewReport(ana, ion, 1));
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.PostAsync(DismissUrl(Guid.NewGuid()), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Member.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        (await ReportsAsync()).Should().ContainSingle().Which.Resolution.Should().Be(ReportResolution.Pending);
    }

    [Fact]
    public async Task Given_Student_When_Dismissing_Then_Returns403AndReportsUnchanged()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ana, ion, NewReport(ana, ion, 1));
        AuthenticateAs(ana.Id);

        // Act
        var response = await Client.PostAsync(DismissUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Forbidden");
        (await ReportsAsync()).Should().ContainSingle().Which.Resolution.Should().Be(ReportResolution.Pending);
    }

    [Fact]
    public async Task Given_NoToken_When_Dismissing_Then_Returns401AndReportsUnchanged()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        await SeedAsync(ana, ion, NewReport(ana, ion, 1));

        // Act
        var response = await Client.PostAsync(DismissUrl(ion.Id), content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        (await ReportsAsync()).Should().ContainSingle().Which.Resolution.Should().Be(ReportResolution.Pending);
    }
}
