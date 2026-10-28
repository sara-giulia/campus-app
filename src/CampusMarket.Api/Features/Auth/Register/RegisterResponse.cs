using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Auth.Register;

public sealed record RegisterResponse(Guid Id, string Name, string Email, MemberRole Role, DateTimeOffset CreatedAt)
{
    public static RegisterResponse From(Member member) => new(
        member.Id,
        member.Name,
        member.Email.Value,
        member.Role,
        member.CreatedAt);
}

