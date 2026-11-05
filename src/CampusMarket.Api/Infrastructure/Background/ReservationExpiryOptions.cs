namespace CampusMarket.Api.Infrastructure.Background;

public sealed class ReservationExpiryOptions
{
    public const string SectionName = "ReservationExpiry";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
