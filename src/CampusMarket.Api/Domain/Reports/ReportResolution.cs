namespace CampusMarket.Api.Domain.Reports;

// Pending: waiting for an Admin. Confirmed: the Admin hid the listing (Report) or suspended the member (MemberReport).
// Dismissed: the Admin found nothing wrong.
public enum ReportResolution { Pending, Confirmed, Dismissed }
