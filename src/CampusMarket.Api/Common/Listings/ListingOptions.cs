namespace CampusMarket.Api.Common.Listings;

// Bound from the "Listings" configuration section. A published or renewed listing expires
// ExpiryDays after that moment (see Listing.Publish, Listing.Renew and ListingExpiryService).
public sealed class ListingOptions
{
    public const string SectionName = "Listings";
    public const int DefaultExpiryDays = 30;

    public int ExpiryDays { get; set; } = DefaultExpiryDays;
}
