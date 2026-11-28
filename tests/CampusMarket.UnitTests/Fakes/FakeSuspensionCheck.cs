using CampusMarket.Api.Common.Auth;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeSuspensionCheck : ISuspensionCheck
{
    public HashSet<Guid> SuspendedIds { get; } = new();

    public int InvalidateCount { get; private set; }

    public Task<bool> IsSuspendedAsync(Guid memberId, CancellationToken ct = default) =>
        Task.FromResult(SuspendedIds.Contains(memberId));

    public void Invalidate() => InvalidateCount++;
}
