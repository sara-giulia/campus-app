namespace CampusMarket.Api.Common.Events;

// Live events for logged-in members: the unit of work publishes after a successful save, the
// events endpoint subscribes for as long as the browser keeps the connection open.
public interface ILiveEvents
{
    // Sends the event to every open connection of the member; nothing happens if there is none.
    void Publish(Guid memberId, LiveEvent liveEvent);

    // The events of one member, until the token is cancelled (the browser closed the connection).
    // A ping comes every LiveEventsOptions.HeartbeatSeconds.
    IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid memberId, CancellationToken ct);

    // Ends every open connection of the member (the stream completes, so the response ends).
    // Used when an Admin suspends the member: a reconnect then gets 401.
    void Disconnect(Guid memberId);
}
