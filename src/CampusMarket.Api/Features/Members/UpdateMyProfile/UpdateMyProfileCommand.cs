using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(Guid MemberId, string Name) : ICommand<Guid>;