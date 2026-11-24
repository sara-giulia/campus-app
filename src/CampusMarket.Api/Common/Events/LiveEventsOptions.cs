namespace CampusMarket.Api.Common.Events;

// "LiveEvents" section of the configuration.
public sealed class LiveEventsOptions
{
    public const string SectionName = "LiveEvents";

    // How often an idle connection gets a ping. Render closes a connection that sends nothing for
    // a few minutes, so this stays well under that. Between 1 and 300 (checked at start).
    public int HeartbeatSeconds { get; set; } = 25;
}
