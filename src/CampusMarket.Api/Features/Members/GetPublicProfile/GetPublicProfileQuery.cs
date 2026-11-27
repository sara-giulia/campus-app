using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetPublicProfile;

// IsAdmin: an Admin still sees the profile of a suspended member (everyone else gets 404).
public sealed record GetPublicProfileQuery(Guid MemberId, bool IsAdmin = false) : IQuery<GetPublicProfileResponse>;