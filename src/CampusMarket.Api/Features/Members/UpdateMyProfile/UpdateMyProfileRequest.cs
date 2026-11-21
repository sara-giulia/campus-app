namespace CampusMarket.Api.Features.Members.UpdateMyProfile;

/// <summary>
/// The JSON body of PUT /api/v1/members/me
/// </summary>
public sealed record UpdateMyProfileRequest(string Name);