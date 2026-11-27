using CampusMarket.Api.Common.Events;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeLiveEvents : ILiveEvents
{
    public List<(Guid MemberId, LiveEvent Event)> Published { get; } = new();

    public List<Guid> Disconnected { get; } = new();

    public void Publish(Guid memberId, LiveEvent liveEvent) => Published.Add((memberId, liveEvent));

    public IAsyncEnumerable<LiveEvent> SubscribeAsync(Guid memberId, CancellationToken ct) =>
        AsyncEnumerable.Empty<LiveEvent>();

    public void Disconnect(Guid memberId) => Disconnected.Add(memberId);
}
