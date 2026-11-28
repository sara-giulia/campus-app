using System.Globalization;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.Api.Features.Offers;

// The notification texts of the offer slices, in English like the other notifications; the frontend
// (features/notifications/messageText.ts) matches these exact shapes to show them in Romanian.
internal static class OfferNotifications
{
    public static string Received(string buyerName, decimal amount, string title) =>
        $"{buyerName} offered {Lei(amount)} lei for {title}";

    public static string Countered(string memberName, decimal amount, string title) =>
        $"{memberName} countered with {Lei(amount)} lei for {title}";

    public static string AcceptedByOther(string memberName, decimal amount, string title) =>
        $"{memberName} accepted your offer of {Lei(amount)} lei for {title}";

    public static string AcceptedByYou(decimal amount, string title) =>
        $"You accepted the offer of {Lei(amount)} lei for {title}";

    public static string Rejected(string memberName, decimal amount, string title) =>
        $"{memberName} rejected your offer of {Lei(amount)} lei for {title}";

    public static string ClosedByOtherReservation(decimal amount, string title) =>
        $"The offer of {Lei(amount)} lei for {title} was closed: the listing was reserved by another buyer";

    // The other side was suspended by an Admin. The text does not say "suspended": that stays between
    // the member and the Admins.
    public static string ClosedBecauseMemberUnavailable(decimal amount, string title) =>
        $"The offer of {Lei(amount)} lei for {title} was closed: the other member is no longer available";

    // Adds the notification to the unit of work; it is saved (and sent live) by the handler's CommitAsync.
    public static void Add(
        INotificationRepository notifications, Guid memberId, NotificationType type, string message, Guid listingId, DateTimeOffset now)
    {
        var notification = Notification.Create(memberId, type, message, listingId, now);
        if (notification.IsSuccess)
            notifications.Add(notification.Value);
    }

    // 120 → "120", 99.5 → "99.5", the same whatever the server culture.
    private static string Lei(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
