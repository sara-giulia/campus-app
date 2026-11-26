using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Reports;

// A report says a listing should be checked by an Admin.
// A member reports a listing with a reason; the spam model reports it on its own when the
// listing is published (ReporterId is null and SpamProbability is set).
public sealed class Report
{
    public const int MaxReasonLength = 500;
    public const string AutomaticReason = "Flagged automatically as spam.";

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid? ReporterId { get; private set; }
    // Stryker disable once String : both factories always set Reason, so the default value is never observable
    public string Reason { get; private set; } = string.Empty;
    public double? SpamProbability { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    // Every report starts Pending; an Admin confirms it (hides the listing) or dismisses it, once.
    public ReportResolution Resolution { get; private set; } = ReportResolution.Pending;
    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsAutomatic => ReporterId is null;
    public bool IsPending => Resolution == ReportResolution.Pending;

    private Report() { }

    public static Result<Report> Create(Guid reporterId, Listing listing, string? reason, DateTimeOffset createdAt)
    {
        if (reporterId == Guid.Empty)
            return Result<Report>.Failure(
                Error.Validation("Report.ReporterEmpty", "Reporter id must not be empty."));

        if (listing.SellerId == reporterId)
            return Result<Report>.Failure(
                Error.Validation("Report.OwnListing", "A seller cannot report their own listing."));

        // Only what other members can see is reported: not a draft, not a sold or hidden listing.
        if (listing.Status is not (ListingStatus.Active or ListingStatus.Reserved))
            return Result<Report>.Failure(
                Error.Validation("Report.ListingNotAvailable", "Only an active or reserved listing can be reported."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result<Report>.Failure(
                Error.Validation("Report.ReasonEmpty", "Reason must not be empty."));
        if (reason.Trim().Length > MaxReasonLength)
            return Result<Report>.Failure(
                Error.Validation("Report.ReasonTooLong", $"Reason must have at most {MaxReasonLength} characters."));

        return new Report
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            ReporterId = reporterId,
            Reason = reason.Trim(),
            CreatedAt = createdAt
        };
    }

    public static Report CreateAutomatic(Guid listingId, double spamProbability, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listingId,
        Reason = AutomaticReason,
        SpamProbability = spamProbability,
        CreatedAt = createdAt
    };

    public Result<Report> Confirm(DateTimeOffset resolvedAt) => Resolve(ReportResolution.Confirmed, resolvedAt);

    public Result<Report> Dismiss(DateTimeOffset resolvedAt) => Resolve(ReportResolution.Dismissed, resolvedAt);

    private Result<Report> Resolve(ReportResolution resolution, DateTimeOffset resolvedAt)
    {
        if (!IsPending)
            return Result<Report>.Failure(
                Error.Validation("Report.AlreadyResolved", "The report has already been resolved."));

        Resolution = resolution;
        ResolvedAt = resolvedAt;
        return this;
    }
}
