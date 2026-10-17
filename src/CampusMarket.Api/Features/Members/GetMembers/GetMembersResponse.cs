using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMembers;

public sealed record GetMembersResponse(IReadOnlyList<GetMembersItem> Items, int Count);

public sealed record GetMembersItem(Guid Id, string Name, string Email)
{
    public static GetMembersItem From(Member member) => new(
        member.Id,
        member.Name,
        member.Email.Value);
}


