using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.IntegrationTests.Infrastructure;

namespace CampusMarket.IntegrationTests.Offers;

// Builders shared by the offer tests. Everything goes through the domain; every member is a real Member
// that the test seeds (offers, conversations and notifications have foreign keys to Members).
internal static class OfferTestData
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public static Member NewMember(string name, string email) => Member.Create(name, email, CreatedAt).Value;

    public static Listing ActiveListing(Guid sellerId, string title = "Manual POO", decimal price = 100m)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, price, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    // The buyer's offer on the listing, Pending, made at a fixed time.
    public static Offer BuyerOffer(Listing listing, Guid buyerId, decimal amount, int minutes = 1) =>
        Offer.Make(listing, buyerId, amount, CreatedAt.AddMinutes(minutes)).Value;
}
