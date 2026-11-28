using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.DismissMemberReports;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.DismissMemberReports;

public class DismissMemberReportsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherAdminId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ReporterId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherReporterId = new("22222222-2222-2222-2222-222222222222");

    private readonly Member _member = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _other = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeMemberRepository _members = new();
    private readonly FakeMemberReportRepository _reports = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DismissMemberReportsHandler _handler;

    public DismissMemberReportsHandlerTests()
    {
        _members.Items[_member.Id] = _member;
        _members.Items[_other.Id] = _other;
        _handler = new DismissMemberReportsHandler(_members, _reports, _unitOfWork, new FixedTimeProvider(Now));
    }

    private MemberReport SeedReport(Guid reporterId, Guid reportedId)
    {
        var report = MemberReport.Create(reporterId, reportedId, "Rude messages", null, Now.AddHours(-1)).Value;
        _reports.Add(report);
        return report;
    }

    private Task<Result<Guid>> DismissAsync(Guid memberId) =>
        _handler.HandleAsync(new DismissMemberReportsCommand(memberId, AdminId), CancellationToken.None);

    [Fact]
    public async Task Given_PendingReports_When_Dismissing_Then_DismissesEveryOneAndSavesOnce()
    {
        var first = SeedReport(ReporterId, _member.Id);
        var second = SeedReport(OtherReporterId, _member.Id);

        var result = await DismissAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_member.Id);
        first.Resolution.Should().Be(ReportResolution.Dismissed);
        first.ResolvedAt.Should().Be(Now);
        first.ResolvedById.Should().Be(AdminId);
        second.Resolution.Should().Be(ReportResolution.Dismissed);
        second.ResolvedAt.Should().Be(Now);
        second.ResolvedById.Should().Be(AdminId);
        _unitOfWork.SaveCount.Should().Be(1);
        _members.UpdateCount.Should().Be(0);
        _member.IsSuspended(Now).Should().BeFalse();
    }

    [Fact]
    public async Task Given_ReportsOfAnotherMember_When_Dismissing_Then_TheyStayPending()
    {
        SeedReport(ReporterId, _member.Id);
        var otherReport = SeedReport(ReporterId, _other.Id);

        await DismissAsync(_member.Id);

        otherReport.IsPending.Should().BeTrue();
        otherReport.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_ConfirmedReportAndPendingReport_When_Dismissing_Then_OnlyPendingChanges()
    {
        var confirmed = SeedReport(ReporterId, _member.Id);
        confirmed.Confirm(OtherAdminId, Now.AddMinutes(-10));
        var pending = SeedReport(OtherReporterId, _member.Id);

        var result = await DismissAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        confirmed.Resolution.Should().Be(ReportResolution.Confirmed);
        confirmed.ResolvedAt.Should().Be(Now.AddMinutes(-10));
        confirmed.ResolvedById.Should().Be(OtherAdminId);
        pending.Resolution.Should().Be(ReportResolution.Dismissed);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Dismissing_Then_ReturnsNotFoundAndSavesNothing()
    {
        var unknownId = Guid.NewGuid();

        var result = await DismissAsync(unknownId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoReports_When_Dismissing_Then_ReturnsNoPendingReports()
    {
        var result = await DismissAsync(_member.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.NoPendingReports");
        result.Error.Message.Should().Be("The member has no pending reports.");
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_OnlyResolvedReports_When_Dismissing_Then_ReturnsNoPendingReportsAndKeepsThem()
    {
        var confirmed = SeedReport(ReporterId, _member.Id);
        confirmed.Confirm(OtherAdminId, Now.AddMinutes(-10));

        var result = await DismissAsync(_member.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Report.NoPendingReports");
        confirmed.Resolution.Should().Be(ReportResolution.Confirmed);
        confirmed.ResolvedById.Should().Be(OtherAdminId);
        _unitOfWork.SaveCount.Should().Be(0);
    }
}
