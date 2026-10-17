namespace CampusMarket.Api.Features.Members.GetMemberById;

/// <summary>
/// The id comes from the route: GET /api/v1/members/{id}
/// </summary>
public sealed record GetMemberByIdRequest(Guid Id);

