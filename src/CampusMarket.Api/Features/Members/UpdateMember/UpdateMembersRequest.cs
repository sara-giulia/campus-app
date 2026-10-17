namespace CampusMarket.Api.Features.Members.UpdateMember;

/// <summary>
/// The JSON body of PUT /api/v1/members/{id}. The id comes from the route.
/// </summary>
public sealed record UpdateMemberRequest(string Name, string Email);


