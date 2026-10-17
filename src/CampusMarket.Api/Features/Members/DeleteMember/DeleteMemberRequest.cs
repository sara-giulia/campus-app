namespace CampusMarket.Api.Features.Members.DeleteMember;

/// <summary>
/// The id comes from the route: DELETE /api/v1/members/{id}
/// </summary>
public sealed record DeleteMemberRequest(Guid Id);
