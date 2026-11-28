using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeMemberRepository : IMemberRepository
{
    public Dictionary<Guid, Member> Items { get; } = new();

    public bool RejectAdd { get; set; }

    public int UpdateCount { get; private set; }

    public Task<bool> AddAsync(Member member, CancellationToken ct = default)
    {
        if (RejectAdd)
            return Task.FromResult(false);

        if (Items.ContainsKey(member.Id))
            return Task.FromResult(false);

        Items[member.Id] = member;
        return Task.FromResult(true);
    }

    public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Items.TryGetValue(id, out var member);
        return Task.FromResult(member);
    }

    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Member> snapshot = Items.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public void Update(Member member)
    {
        Items[member.Id] = member;
        UpdateCount++;
    }

    public void Delete(Member member)
    {
        Items.Remove(member.Id);
    }

    // Like EfMemberRepository: the members suspended at this moment (Member.IsSuspended).
    public Task<IReadOnlySet<Guid>> GetSuspendedIdsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        IReadOnlySet<Guid> ids = Items.Values
            .Where(m => m.IsSuspended(now))
            .Select(m => m.Id)
            .ToHashSet();
        return Task.FromResult(ids);
    }
}
