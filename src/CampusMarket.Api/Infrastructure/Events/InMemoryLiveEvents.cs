using System.Collections.Concurrent;
using System.Threading.Channels;
using CampusMarket.Api.Common.Events;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Events;

// Keeps the open connections in memory: one channel per connection, grouped by member, so a member
// with two tabs open gets the event in both. It works because the API runs as a single instance
// (Render); with several instances the events would need a shared broker (Redis, Postgres LISTEN).
public sealed class InMemoryLiveEvents(IOptions<LiveEventsOptions> options, TimeProvider timeProvider) : ILiveEvents
{
    // A connection that does not read (a slow network) keeps at most this many events; the oldest go.
    // Losing one is harmless: every event only tells the page to load again.
    private const int Capacity = 50;

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Connection>> _connections = new();

    // The channel of one open connection and its heartbeat timer, so Disconnect can stop both.
    private sealed record Connection(Channel<LiveEvent> Channel, ITimer Heartbeat);

    public void Publish(Guid memberId, LiveEvent liveEvent)
    {
        if (!_connections.TryGetValue(memberId, out var channels))
            return;

        foreach (var connection in channels.Values)
            connection.Channel.Writer.TryWrite(liveEvent);
    }

    public void Disconnect(Guid memberId)
    {
        if (!_connections.TryGetValue(memberId, out var channels))
            return;

        // Dispose of the timer and TryComplete of the channel do nothing the second time, so a connection
        // that its browser (or another Disconnect) removes at the same moment can be stopped again safely.
        foreach (var (connectionId, connection) in channels)
        {
            channels.TryRemove(connectionId, out _);
            connection.Heartbeat.Dispose();
            connection.Channel.Writer.TryComplete();
        }
    }

    public IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid memberId, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<LiveEvent>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var connectionId = Guid.NewGuid();
        var channels = _connections.GetOrAdd(memberId, _ => new ConcurrentDictionary<Guid, Connection>());

        var heartbeat = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
        var timer = timeProvider.CreateTimer(
            _ => channel.Writer.TryWrite(LiveEvent.Ping), null, heartbeat, heartbeat);
        channels[connectionId] = new Connection(channel, timer);

        // A first ping right away: the browser (and a test) knows the stream is open and registered,
        // and the response headers go out at once instead of with the first real event.
        channel.Writer.TryWrite(LiveEvent.Ping);

        // The connection ends when the browser closes it (the request is aborted) or on Disconnect: then
        // stop the pings and forget the channel, so Publish no longer writes to it.
        ct.Register(() =>
        {
            timer.Dispose();
            channels.TryRemove(connectionId, out _);
        });

        return channel.Reader.ReadAllAsync(ct);
    }
}
