using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.GetReportedMembers;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.GetReportedMembers;

public class GetReportedMembersHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ReporterId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherReporterId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdReporterId = new("33333333-3333-3333-3333-333333333333");

    private readonly FakeMemberReportRepository _reports = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeListingRepository _listings = new();
    private readonly GetReportedMembersHandler _handler;

    public GetReportedMembersHandlerTests()
    {
        _handler = new GetReportedMembersHandler(_reports, _members, _listings, new FixedTimeProvider(Now));
    }

    private Member SeedMember(string name, string email)
    {
        var member = Member.Create(name, email, Now.AddDays(-10)).Value;
        _members.Items[member.Id] = member;
        return member;
    }

    private MemberReport SeedReport(Guid reporterId, Guid reportedId, string reason, DateTimeOffset createdAt, Conversation? conversation = null)
    {
        var report = MemberReport.Create(reporterId, reportedId, reason, conversation, createdAt).Value;
        _reports.Add(report);
        return report;
    }

    private Listing SeedListing(Guid sellerId, bool hidden)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now.AddDays(-5)).Value;
        listing.Publish(Now.AddDays(30));
        if (hidden)
            listing.Hide();
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    private Task<Result<GetReportedMembersResponse>> QueryAsync(GetReportedMembersQuery query) =>
        _handler.HandleAsync(query, CancellationToken.None);

    [Fact]
    public async Task Given_NoReports_When_Handling_Then_ReturnsEmptyPage()
    {
        var result = await QueryAsync(new GetReportedMembersQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Given_MemberWithTwoReports_When_Handling_Then_ReturnsOneItemWithEveryField()
    {
        var member = SeedMember("Ion Ionescu", "Ion@Campus.ro");
        var listing = Listing.Create(member.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var conversation = Conversation.Start(ReporterId, listing, "Salut", Now).Value;
        var older = SeedReport(ReporterId, member.Id, "Rude messages", Now.AddHours(-2), conversation);
        var newer = SeedReport(OtherReporterId, member.Id, "Asked for money", Now.AddHours(-1));
        SeedListing(member.Id, hidden: true);
        SeedListing(member.Id, hidden: false);

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.MemberId.Should().Be(member.Id);
        item.Name.Should().Be("Ion Ionescu");
        item.Email.Should().Be("ion@campus.ro");
        item.ReportCount.Should().Be(2);
        item.LastReportedAt.Should().Be(Now.AddHours(-1));
        item.HiddenListingsCount.Should().Be(1);
        item.IsSuspended.Should().BeFalse();
        item.SuspendedUntil.Should().BeNull();
        item.Reports.Select(r => r.Id).Should().Equal(newer.Id, older.Id);
        item.Reports[0].ReporterId.Should().Be(OtherReporterId);
        item.Reports[0].Reason.Should().Be("Asked for money");
        item.Reports[0].ConversationId.Should().BeNull();
        item.Reports[0].CreatedAt.Should().Be(Now.AddHours(-1));
        item.Reports[1].ReporterId.Should().Be(ReporterId);
        item.Reports[1].Reason.Should().Be("Rude messages");
        item.Reports[1].ConversationId.Should().Be(conversation.Id);
        item.Reports[1].CreatedAt.Should().Be(Now.AddHours(-2));
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        _reports.Items.Should().HaveCount(2);
        _members.Items.Should().HaveCount(1);
        _members.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReportsWithSameCreatedAt_When_Handling_Then_ReportsOrderedById()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        var first = SeedReport(ReporterId, member.Id, "One", Now.AddHours(-1));
        var second = SeedReport(OtherReporterId, member.Id, "Two", Now.AddHours(-1));
        var expected = new[] { first.Id, second.Id }.Order().ToList();

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items[0].Reports.Select(r => r.Id).Should().Equal(expected);
    }

    [Fact]
    public async Task Given_TemporarilySuspendedMember_When_Handling_Then_ShowsSuspendedUntil()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        member.Suspend(AdminId, "Scam", Now.AddDays(3), Now.AddHours(-1));
        SeedReport(ReporterId, member.Id, "Rude messages", Now.AddHours(-2));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items[0].IsSuspended.Should().BeTrue();
        result.Value.Items[0].SuspendedUntil.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public async Task Given_EndedSuspension_When_Handling_Then_NotSuspendedAndNoEnd()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        member.Suspend(AdminId, "Scam", Now, Now.AddDays(-1));
        SeedReport(ReporterId, member.Id, "Rude messages", Now.AddHours(-2));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items[0].IsSuspended.Should().BeFalse();
        result.Value.Items[0].SuspendedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Given_PendingAndResolvedReports_When_Handling_Then_CountsOnlyPending()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        var pending = SeedReport(ReporterId, member.Id, "Pending one", Now.AddHours(-3));
        var dismissed = SeedReport(OtherReporterId, member.Id, "Dismissed one", Now.AddHours(-1));
        dismissed.Dismiss(AdminId, Now.AddMinutes(-30));

        var result = await QueryAsync(new GetReportedMembersQuery());

        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ReportCount.Should().Be(1);
        item.LastReportedAt.Should().Be(Now.AddHours(-3));
        item.Reports.Select(r => r.Id).Should().Equal(pending.Id);
    }

    [Fact]
    public async Task Given_MemberWithOnlyResolvedReports_When_Handling_Then_IsNotReturned()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        var report = SeedReport(ReporterId, member.Id, "Rude", Now.AddHours(-1));
        report.Confirm(AdminId, Now.AddMinutes(-30));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReportOfMemberNotInRepository_When_Handling_Then_IsNotReturnedOrCounted()
    {
        var member = SeedMember("Ion Ionescu", "ion@campus.ro");
        SeedReport(ReporterId, member.Id, "Rude", Now.AddHours(-1));
        SeedReport(ReporterId, Guid.NewGuid(), "Rude", Now.AddHours(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Should().ContainSingle().Which.MemberId.Should().Be(member.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_MembersWithDifferentReportCounts_When_Handling_Then_MostReportedFirst()
    {
        var once = SeedMember("Once Reported", "once@campus.ro");
        var twice = SeedMember("Twice Reported", "twice@campus.ro");
        SeedReport(ReporterId, once.Id, "Rude", Now.AddHours(-1));
        SeedReport(ReporterId, twice.Id, "Rude", Now.AddHours(-5));
        SeedReport(OtherReporterId, twice.Id, "Rude", Now.AddHours(-4));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(twice.Id, once.Id);
    }

    [Fact]
    public async Task Given_SameReportCount_When_Handling_Then_MostRecentlyReportedFirst()
    {
        var older = SeedMember("Older Report", "older@campus.ro");
        var newer = SeedMember("Newer Report", "newer@campus.ro");
        SeedReport(ReporterId, older.Id, "Rude", Now.AddHours(-3));
        SeedReport(ReporterId, newer.Id, "Rude", Now.AddHours(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task Given_SameCountAndSameTime_When_Handling_Then_OrderedByMemberId()
    {
        var first = SeedMember("First Member", "first@campus.ro");
        var second = SeedMember("Second Member", "second@campus.ro");
        SeedReport(ReporterId, first.Id, "Rude", Now.AddHours(-1));
        SeedReport(ReporterId, second.Id, "Rude", Now.AddHours(-1));
        var expected = new[] { first.Id, second.Id }.Order().ToList();

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(expected);
    }

    [Fact]
    public async Task Given_ThreeReportedMembers_When_RequestingSecondPageOfTwo_Then_ReturnsTheLastOne()
    {
        var a = SeedMember("Member A", "a@campus.ro");
        var b = SeedMember("Member B", "b@campus.ro");
        var c = SeedMember("Member C", "c@campus.ro");
        SeedReport(ReporterId, a.Id, "Rude", Now.AddHours(-1));
        SeedReport(ReporterId, b.Id, "Rude", Now.AddHours(-2));
        SeedReport(ThirdReporterId, c.Id, "Rude", Now.AddHours(-3));

        var result = await QueryAsync(new GetReportedMembersQuery(2, 2));

        result.Value.Items.Select(i => i.MemberId).Should().Equal(c.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Given_TwoReportedMembers_When_RequestingPageSizeOne_Then_ReturnsOnlyTheFirst()
    {
        var a = SeedMember("Member A", "a@campus.ro");
        var b = SeedMember("Member B", "b@campus.ro");
        SeedReport(ReporterId, a.Id, "Rude", Now.AddHours(-1));
        SeedReport(ReporterId, b.Id, "Rude", Now.AddHours(-2));

        var result = await QueryAsync(new GetReportedMembersQuery(1, 1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.MemberId).Should().Equal(a.Id);
        result.Value.PageSize.Should().Be(1);
        result.Value.TotalCount.Should().Be(2);
    }

    private Member SeedSuspended(string name, string email, DateTimeOffset suspendedAt, DateTimeOffset? until = null)
    {
        var member = SeedMember(name, email);
        member.Suspend(AdminId, "Scam", until, suspendedAt);
        return member;
    }

    [Fact]
    public async Task Given_SuspendedMemberWithoutPendingReports_When_Handling_Then_IsListedWithNoReports()
    {
        var member = SeedSuspended("Ion Ionescu", "Ion@Campus.ro", Now.AddHours(-1), Now.AddDays(3));
        SeedListing(member.Id, hidden: true);

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.MemberId.Should().Be(member.Id);
        item.Name.Should().Be("Ion Ionescu");
        item.Email.Should().Be("ion@campus.ro");
        item.ReportCount.Should().Be(0);
        item.Reports.Should().BeEmpty();
        item.LastReportedAt.Should().BeNull();
        item.HiddenListingsCount.Should().Be(1);
        item.IsSuspended.Should().BeTrue();
        item.SuspendedUntil.Should().Be(Now.AddDays(3));
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        _members.UpdateCount.Should().Be(0);
        _reports.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PermanentlySuspendedMemberWithoutReports_When_Handling_Then_IsListedWithoutEnd()
    {
        var member = SeedSuspended("Ion Ionescu", "ion@campus.ro", Now.AddHours(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.MemberId.Should().Be(member.Id);
        item.IsSuspended.Should().BeTrue();
        item.SuspendedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Given_SuspendedMemberWithOnlyResolvedReports_When_Handling_Then_IsListedWithNoReports()
    {
        var member = SeedSuspended("Ion Ionescu", "ion@campus.ro", Now.AddHours(-1));
        var report = SeedReport(ReporterId, member.Id, "Rude", Now.AddHours(-2));
        report.Confirm(AdminId, Now.AddHours(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.MemberId.Should().Be(member.Id);
        item.ReportCount.Should().Be(0);
        item.Reports.Should().BeEmpty();
        item.LastReportedAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_EndedTemporarySuspensionAndNoReports_When_Handling_Then_IsNotListed()
    {
        SeedSuspended("Ion Ionescu", "ion@campus.ro", Now.AddDays(-1), Now);

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberNeitherSuspendedNorReported_When_Handling_Then_IsNotListed()
    {
        SeedMember("Ion Ionescu", "ion@campus.ro");

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedMemberWithPendingReports_When_Handling_Then_AppearsOnceInFirstGroup()
    {
        var reported = SeedMember("Reported Once", "reported@campus.ro");
        var suspended = SeedSuspended("Suspended Reported", "suspended@campus.ro", Now.AddHours(-1));
        SeedReport(ReporterId, reported.Id, "Rude", Now.AddHours(-3));
        SeedReport(ReporterId, suspended.Id, "Rude", Now.AddHours(-5));
        SeedReport(OtherReporterId, suspended.Id, "Scam", Now.AddHours(-4));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(suspended.Id, reported.Id);
        result.Value.Items[0].ReportCount.Should().Be(2);
        result.Value.Items[0].IsSuspended.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_ReportedAndSuspendedOnlyMembers_When_Handling_Then_ReportedComeFirst()
    {
        var reported = SeedMember("Reported Member", "reported@campus.ro");
        SeedReport(ReporterId, reported.Id, "Rude", Now.AddDays(-5));
        var suspended = SeedSuspended("Suspended Member", "suspended@campus.ro", Now.AddMinutes(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(reported.Id, suspended.Id);
    }

    [Fact]
    public async Task Given_TwoSuspendedOnlyMembers_When_Handling_Then_MostRecentlySuspendedFirst()
    {
        var older = SeedSuspended("Older Suspension", "older@campus.ro", Now.AddHours(-3));
        var newer = SeedSuspended("Newer Suspension", "newer@campus.ro", Now.AddHours(-1));

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(newer.Id, older.Id);
    }

    [Fact]
    public async Task Given_TwoSuspendedOnlyMembersAtSameTime_When_Handling_Then_OrderedByMemberId()
    {
        var first = SeedSuspended("First Member", "first@campus.ro", Now.AddHours(-1));
        var second = SeedSuspended("Second Member", "second@campus.ro", Now.AddHours(-1));
        var expected = new[] { first.Id, second.Id }.Order().ToList();

        var result = await QueryAsync(new GetReportedMembersQuery());

        result.Value.Items.Select(i => i.MemberId).Should().Equal(expected);
    }

    [Fact]
    public async Task Given_OneReportedAndTwoSuspendedOnly_When_RequestingFirstPageOfTwo_Then_PageCrossesBothGroups()
    {
        var reported = SeedMember("Reported Member", "reported@campus.ro");
        SeedReport(ReporterId, reported.Id, "Rude", Now.AddHours(-1));
        var newer = SeedSuspended("Newer Suspension", "newer@campus.ro", Now.AddHours(-1));
        SeedSuspended("Older Suspension", "older@campus.ro", Now.AddHours(-3));

        var result = await QueryAsync(new GetReportedMembersQuery(1, 2));

        result.Value.Items.Select(i => i.MemberId).Should().Equal(reported.Id, newer.Id);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Given_OneReportedAndTwoSuspendedOnly_When_RequestingSecondPageOfTwo_Then_ReturnsOldestSuspension()
    {
        var reported = SeedMember("Reported Member", "reported@campus.ro");
        SeedReport(ReporterId, reported.Id, "Rude", Now.AddHours(-1));
        SeedSuspended("Newer Suspension", "newer@campus.ro", Now.AddHours(-1));
        var older = SeedSuspended("Older Suspension", "older@campus.ro", Now.AddHours(-3));

        var result = await QueryAsync(new GetReportedMembersQuery(2, 2));

        result.Value.Items.Select(i => i.MemberId).Should().Equal(older.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
    }

    [Fact]
    public async Task Given_PageSizeAtMax_When_Handling_Then_Succeeds()
    {
        var result = await QueryAsync(new GetReportedMembersQuery(1, GetReportedMembersQuery.MaxPageSize));

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Given_InvalidPage_When_Handling_Then_ReturnsInvalidPage(int page)
    {
        var result = await QueryAsync(new GetReportedMembersQuery(page, 20));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Moderation.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Given_InvalidPageSize_When_Handling_Then_ReturnsInvalidPageSize(int pageSize)
    {
        var result = await QueryAsync(new GetReportedMembersQuery(1, pageSize));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Moderation.InvalidPageSize");
        result.Error.Message.Should().Be($"Page size must be between 1 and {GetReportedMembersQuery.MaxPageSize}.");
    }
}
