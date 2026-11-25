using CampusMarket.Api.Domain.Listings;
using FluentAssertions;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Builds listings for the listing expiry tests, with real dates (the API reads the real clock).
public static class ExpiryListings
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    // Published a while ago, ExpiresAt in the past, then expired: Status Expired, Version 4.
    public static Listing Expired(
        Guid sellerId, string title = "Manual POO", Category category = Category.Textbook, DateTimeOffset? createdAt = null)
    {
        var listing = Listing.Create(sellerId, title, category, Condition.Good, 50m, createdAt ?? CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(-1).ToMicroseconds());
        listing.Expire(DateTimeOffset.UtcNow).Should().BeTrue();
        return listing;
    }

    // Active with ExpiresAt in the past: the job expires it.
    public static Listing ActivePastExpiry(
        Guid sellerId, string title = "Manual POO", Category category = Category.Textbook, DateTimeOffset? createdAt = null)
    {
        var listing = Listing.Create(sellerId, title, category, Condition.Good, 50m, createdAt ?? CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(-1).ToMicroseconds());
        return listing;
    }

    public static Listing Active(
        Guid sellerId, string title = "Manual POO", Category category = Category.Textbook, DateTimeOffset? createdAt = null)
    {
        var listing = Listing.Create(sellerId, title, category, Condition.Good, 50m, createdAt ?? CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    // One listing in each status that is not Expired (the Theory data of the "not expired" cases).
    public static Listing InStatus(Guid sellerId, ListingStatus status, Guid buyerId)
    {
        var listing = Listing.Create(sellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        switch (status)
        {
            case ListingStatus.Draft:
                break;
            case ListingStatus.Active:
                listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
                break;
            case ListingStatus.Reserved:
                listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
                listing.Reserve(buyerId, DateTimeOffset.UtcNow);
                break;
            case ListingStatus.Sold:
                listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
                var reservation = listing.Reserve(buyerId, DateTimeOffset.UtcNow).Value;
                listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
                break;
            case ListingStatus.Hidden:
                listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
                listing.Hide();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return listing;
    }
}
