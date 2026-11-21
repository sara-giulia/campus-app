using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMyProfile;

// The password hash is never part of the response.
public sealed record GetMyProfileResponse(Guid Id, string Name, string Email, string Role, DateTimeOffset CreatedAt)
{
    public static GetMyProfileResponse From(Member member) => new(
        member.Id, member.Name, member.Email.Value, member.Role.ToString(), member.CreatedAt);
}