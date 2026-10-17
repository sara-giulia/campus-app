namespace CampusMarket.Api.Features.Members.CreateMember;

/// <summary>
/// The JSON body of POST /api/v1/members
/// </summary>
public sealed record CreateMemberRequest(string Name, string Email);