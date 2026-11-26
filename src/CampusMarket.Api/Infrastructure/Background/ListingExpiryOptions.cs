namespace CampusMarket.Api.Infrastructure.Background;

// How often ListingExpiryService looks for listings to expire. How long a listing stays
// published is Listings:ExpiryDays (Common/Listings/ListingOptions).
public sealed class ListingExpiryOptions
{
    public const string SectionName = "ListingExpiry";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}
