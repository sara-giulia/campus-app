using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.GetReportedMembers;

public sealed record GetReportedMembersResponse(
    IReadOnlyList<ReportedMemberItem> Items, int Count, int TotalCount, int Page, int PageSize);

// One row per reported or suspended member. HiddenListingsCount: their listings an Admin already hid (a hint for the
// decision). Reports: the pending reports, newest first; empty (ReportCount 0, LastReportedAt null) for a
// member who is only suspended. Only Admins see this, so the email is included.
public sealed record ReportedMemberItem(
    Guid MemberId,
    string Name,
    string Email,
    int ReportCount,
    DateTimeOffset? LastReportedAt,
    int HiddenListingsCount,
    bool IsSuspended,
    DateTimeOffset? SuspendedUntil,
    IReadOnlyList<ReportedMemberReport> Reports)
{
    // reports is empty for a suspended member with no pending report.
    public static ReportedMemberItem From(
        Member member, IReadOnlyList<MemberReport> reports, IEnumerable<Listing> memberListings, DateTimeOffset now)
    {
        var newestFirst = reports
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToList();
        var suspended = member.IsSuspended(now);

        return new ReportedMemberItem(
            member.Id,
            member.Name,
            member.Email.Value,
            reports.Count,
            newestFirst.Count > 0 ? newestFirst[0].CreatedAt : null,
            memberListings.Count(l => l.Status == ListingStatus.Hidden),
            suspended,
            suspended ? member.SuspendedUntil : null,
            newestFirst.Select(ReportedMemberReport.From).ToList());
    }
}

public sealed record ReportedMemberReport(Guid Id, Guid ReporterId, string Reason, Guid? ConversationId, DateTimeOffset CreatedAt)
{
    public static ReportedMemberReport From(MemberReport report) =>
        new(report.Id, report.ReporterId, report.Reason, report.ConversationId, report.CreatedAt);
}
