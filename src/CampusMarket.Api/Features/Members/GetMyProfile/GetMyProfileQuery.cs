using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetMyProfile;

public sealed record GetMyProfileQuery(Guid MemberId) : IQuery<GetMyProfileResponse>;