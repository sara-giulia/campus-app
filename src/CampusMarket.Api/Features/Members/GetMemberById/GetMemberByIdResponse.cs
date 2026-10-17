using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMemberById;

public sealed record GetMemberByIdResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt)
{
    public static GetMemberByIdResponse From(Member member) => new(
        member.Id,
        member.Name,
        member.Email.Value,
        member.CreatedAt);
}

