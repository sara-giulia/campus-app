using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetPublicProfile;

public sealed record GetPublicProfileQuery(Guid MemberId) : IQuery<GetPublicProfileResponse>;