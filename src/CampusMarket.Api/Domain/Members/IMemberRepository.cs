namespace CampusMarket.Api.Domain.Members;

public interface IMemberRepository
{
    /// <summary>
    /// Adds a member. Returns false if a member with the same Id already exists
    /// (nothing is added). The endpoint maps false to 409 Conflict.
    /// </summary>
    Task<bool> AddAsync(Member member, CancellationToken ct = default);

    /// <summary>
    /// Returns the member, or null if it does not exist (the endpoint answers 404).
    /// </summary>
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns a snapshot of all members. Changing the returned list
    /// does not change what is stored.
    /// </summary>
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the changes of a member. Usage: call GetByIdAsync first, answer 404
    /// if it returns null, then call Update with the loaded member.
    /// </summary>
    void Update(Member member);

    /// <summary>
    /// Removes a member. Usage: call GetByIdAsync first, answer 404
    /// if it returns null, then call Delete with the loaded member.
    /// </summary>
    void Delete(Member member);
}