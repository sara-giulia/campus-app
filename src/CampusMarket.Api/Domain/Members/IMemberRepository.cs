namespace CampusMarket.Api.Domain.Members;

public interface IMemberRepository
{
    Task AddAsync(Member member, CancellationToken ct = default);
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default);
    void Update(Member member);
    void Delete(Member member);
}