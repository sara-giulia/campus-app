using System.Collections.Concurrent;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Infrastructure;

/// <summary>
/// Keeps members in memory. Registered as a singleton, so the data lives as long as the app.
/// </summary>
public sealed class InMemoryMemberRepository : IMemberRepository
{
    private readonly ConcurrentDictionary<Guid, Member> _members = new();

    public Task<bool> AddAsync(Member member, CancellationToken ct = default)
    {
        // TryAdd returns false if the Id already exists, and then nothing is added.
        var added = _members.TryAdd(member.Id, member);
        return Task.FromResult(added);
    }

    public Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // TryGetValue does not crash when the Id is missing; member stays null.
        _members.TryGetValue(id, out var member);
        return Task.FromResult<Member?>(member);
    }

    public Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default)
    {
        // ToList() makes a copy, so whoever receives the list cannot change the store.
        IReadOnlyList<Member> snapshot = _members.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public void Update(Member member)
    {
        // Only replaces an existing member; it never adds a new one by accident.
        if (_members.TryGetValue(member.Id, out var existing))
        {
            _members.TryUpdate(member.Id, member, existing);
        }
    }

    public void Delete(Member member)
    {
        _members.TryRemove(member.Id, out _);
    }
}

