using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.GetReportedMembers;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Moderation;

public sealed class GetReportedMembersTests : IntegrationTestBase
{
    private const string Url = "/api/v1/moderation/members";
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Member NewMember(string name, string email) => Member.Create(name, email, CreatedAt).Value;

    private static MemberReport NewReport(Member reporter, Member reported, string reason, int hours) =>
        MemberReport.Create(reporter.Id, reported.Id, reason, null, CreatedAt.AddHours(hours)).Value;

    private Task<int> PendingCountAsync() =>
        QueryDbAsync(db => db.Set<MemberReport>().CountAsync(r => r.Resolution == ReportResolution.Pending));

    [Fact]
    public async Task Given_ReportedMembers_When_AdminGetsReportedMembers_Then_Returns200OrderedByCountThenLastReport()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var mihai = NewMember("Mihai Popa", "mihai@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var dan = NewMember("Dan Pop", "dan@student.ro");
        var elena = NewMember("Elena Rusu", "elena@student.ro");
        var ionFirst = NewReport(ana, ion, "Cere bani in avans", 1);
        var ionSecond = NewReport(mihai, ion, "Nu vine la intalnire", 3);
        var danOnly = NewReport(ana, dan, "Mesaje jignitoare", 5);
        var elenaOnly = NewReport(ana, elena, "Anunt fals", 2);
        await SeedAsync(ana, mihai, ion, dan, elena, ionFirst, ionSecond, danOnly, elenaOnly);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        body!.Items.Select(i => i.MemberId).Should().Equal(ion.Id, dan.Id, elena.Id);
        body.Count.Should().Be(3);
        body.TotalCount.Should().Be(3);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);

        var first = body.Items[0];
        first.Name.Should().Be("Ion Ionescu");
        first.Email.Should().Be("ion@student.ro");
        first.ReportCount.Should().Be(2);
        first.LastReportedAt.Should().Be(CreatedAt.AddHours(3));
        first.HiddenListingsCount.Should().Be(0);
        first.IsSuspended.Should().BeFalse();
        first.SuspendedUntil.Should().BeNull();
        first.Reports.Select(r => r.Id).Should().Equal(ionSecond.Id, ionFirst.Id);
        first.Reports[0].Should().Be(new ReportedMemberReport(ionSecond.Id, mihai.Id, "Nu vine la intalnire", null, CreatedAt.AddHours(3)));

        (await PendingCountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Given_TwoMembersWithTheSameCountAndTime_When_AdminGetsReportedMembers_Then_OrderedByMemberId()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var dan = NewMember("Dan Pop", "dan@student.ro");
        await SeedAsync(ana, ion, dan, NewReport(ana, ion, "Anunt fals", 1), NewReport(ana, dan, "Anunt fals", 1));
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        body!.Items.Select(i => i.MemberId).Should().Equal(new[] { ion.Id, dan.Id }.OrderBy(id => id));
        (await PendingCountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Given_ResolvedReports_When_AdminGetsReportedMembers_Then_OnlyPendingReportsCount()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var mihai = NewMember("Mihai Popa", "mihai@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var dan = NewMember("Dan Pop", "dan@student.ro");
        var dismissed = NewReport(ana, ion, "Anunt fals", 1);
        dismissed.Dismiss(AdminId, CreatedAt.AddHours(2));
        var confirmed = NewReport(mihai, ion, "Anunt fals", 1);
        confirmed.Confirm(AdminId, CreatedAt.AddHours(2));
        var pending = NewReport(ana, dan, "Mesaje jignitoare", 1);
        await SeedAsync(ana, mihai, ion, dan, dismissed, confirmed, pending);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.MemberId.Should().Be(dan.Id);
        item.ReportCount.Should().Be(1);
        body.TotalCount.Should().Be(1);
        (await PendingCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspendedMemberWithHiddenListing_When_AdminGetsReportedMembers_Then_ShowsSuspensionAndHiddenCount()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var now = DateTimeOffset.UtcNow.ToMicroseconds();
        var until = now.AddDays(7);
        ion.Suspend(AdminId, "Inselatorie", until, now);
        var hidden = Listing.Create(ion.Id, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        hidden.Publish(now.AddDays(30));
        hidden.Hide();
        var active = Listing.Create(ion.Id, "Calculator", Category.Electronics, Condition.Good, 80m, CreatedAt).Value;
        active.Publish(now.AddDays(30));
        await SeedAsync(ana, ion, hidden, active, NewReport(ana, ion, "Anunt fals", 1));
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.IsSuspended.Should().BeTrue();
        item.SuspendedUntil.Should().Be(until);
        item.HiddenListingsCount.Should().Be(1);
        (await PendingCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Given_TemporarySuspensionThatEnded_When_AdminGetsReportedMembers_Then_NotSuspended()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var now = DateTimeOffset.UtcNow;
        ion.Suspend(AdminId, "Inselatorie", now.AddHours(-1), now.AddHours(-2));
        await SeedAsync(ana, ion, NewReport(ana, ion, "Anunt fals", 1));
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        var item = body!.Items.Should().ContainSingle().Subject;
        item.IsSuspended.Should().BeFalse();
        item.SuspendedUntil.Should().BeNull();
        (await PendingCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Given_ThreeReportedMembers_When_AdminGetsPage2OfSize1_Then_ReturnsTheSecond()
    {
        // Arrange
        var ana = NewMember("Ana Popescu", "ana@student.ro");
        var ion = NewMember("Ion Ionescu", "ion@student.ro");
        var dan = NewMember("Dan Pop", "dan@student.ro");
        var elena = NewMember("Elena Rusu", "elena@student.ro");
        await SeedAsync(ana, ion, dan, elena,
            NewReport(ana, ion, "Anunt fals", 3), NewReport(ana, dan, "Anunt fals", 2), NewReport(ana, elena, "Anunt fals", 1));
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync($"{Url}?page=2&pageSize=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        body!.Items.Should().ContainSingle().Which.MemberId.Should().Be(dan.Id);
        body.Count.Should().Be(1);
        body.TotalCount.Should().Be(3);
        body.Page.Should().Be(2);
        body.PageSize.Should().Be(1);
        (await PendingCountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Given_NoReports_When_AdminGetsReportedMembers_Then_Returns200Empty()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetReportedMembersResponse>(Json);
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);
        (await PendingCountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("?page=0", "Moderation.InvalidPage")]
    [InlineData("?pageSize=0", "Moderation.InvalidPageSize")]
    [InlineData("?pageSize=101", "Moderation.InvalidPageSize")]
    public async Task Given_InvalidPaging_When_AdminGetsReportedMembers_Then_Returns400(string query, string code)
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url + query);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(code);
        error.Type.Should().Be(ErrorType.Validation);
        (await PendingCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Given_Student_When_GettingReportedMembers_Then_Returns403()
    {
        // Arrange
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Forbidden");
        (await PendingCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingReportedMembers_Then_Returns401()
    {
        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Code.Should().Be("Auth.Unauthorized");
        (await PendingCountAsync()).Should().Be(0);
    }
}
