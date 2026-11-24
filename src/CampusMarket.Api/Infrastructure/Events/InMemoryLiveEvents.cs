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

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<LiveEvent>>> _connections = new();

    public void Publish(Guid memberId, LiveEvent liveEvent)
    {
        if (!_connections.TryGetValue(memberId, out var channels))
            return;

        foreach (var channel in channels.Values)
            channel.Writer.TryWrite(liveEvent);
    }

    public IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid memberId, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<LiveEvent>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var connectionId = Guid.NewGuid();
        var channels = _connections.GetOrAdd(memberId, _ => new ConcurrentDictionary<Guid, Channel<LiveEvent>>());
        channels[connectionId] = channel;

        // A first ping right away: the browser (and a test) knows the stream is open and registered,
        // and the response headers go out at once instead of with the first real event.
        channel.Writer.TryWrite(LiveEvent.Ping);

        var heartbeat = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
        var timer = timeProvider.CreateTimer(
            _ => channel.Writer.TryWrite(LiveEvent.Ping), null, heartbeat, heartbeat);

        // The connection only ends when the browser closes it (the request is aborted): then stop the
        // pings and forget the channel, so Publish no longer writes to it.
        ct.Register(() =>
        {
            timer.Dispose();
            channels.TryRemove(connectionId, out _);
        });

        return channel.Reader.ReadAllAsync(ct);
    }
}
