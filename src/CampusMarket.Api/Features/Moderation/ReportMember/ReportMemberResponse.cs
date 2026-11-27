using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.Api.Features.Moderation.ReportMember;

public sealed record ReportMemberResponse(Guid Id, Guid ReportedMemberId, DateTimeOffset CreatedAt)
{
    public static ReportMemberResponse From(MemberReport report) =>
        new(report.Id, report.ReportedMemberId, report.CreatedAt);
}
