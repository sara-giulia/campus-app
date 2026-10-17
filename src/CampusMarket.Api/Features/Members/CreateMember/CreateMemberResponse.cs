using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.CreateMember;

public sealed record CreateMemberResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt)
{
    public static CreateMemberResponse From(Member member) => new(
        member.Id,
        member.Name,
        member.Email.Value,
        member.CreatedAt);
}

