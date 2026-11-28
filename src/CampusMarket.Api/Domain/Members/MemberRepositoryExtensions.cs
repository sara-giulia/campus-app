namespace CampusMarket.Api.Domain.Members;

public static class MemberRepositoryExtensions
{
    // For a handler that checks one member (the seller of one listing, the other side of a conversation).
    public static async Task<bool> IsSuspendedAsync(
        this IMemberRepository members, Guid memberId, DateTimeOffset now, CancellationToken ct = default) =>
        (await members.GetSuspendedIdsAsync(now, ct)).Contains(memberId);
}
