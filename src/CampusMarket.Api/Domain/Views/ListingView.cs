using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Views;

// One member (or one anonymous visitor) who opened a listing on one day. A viewer is counted once per
// listing and day (unique index in the database), however many times the page is opened.
// No personal data: the viewer is the member id, or a random id the browser made up for itself.
public sealed class ListingView
{
    public const int MaxViewerKeyLength = 64;

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    // "m:<member id>" for a logged-in member, "v:<visitor id>" for a visitor.
    public string ViewerKey { get; private set; } = string.Empty;
    // The UTC day of the view.
    public DateOnly Day { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ListingView() { }

    public static string KeyOf(Guid? memberId, Guid? visitorId) =>
        memberId is { } member ? $"m:{member:N}" : $"v:{visitorId:N}";

    public static ListingView Create(Guid listingId, string viewerKey, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listingId,
        ViewerKey = viewerKey,
        Day = DateOnly.FromDateTime(now.UtcDateTime),
        CreatedAt = now
    };

    public static Error ViewerMissingError() =>
        Error.Validation("View.ViewerMissing", "Log in or send a valid visitor id in the X-Visitor-Id header.");
}
