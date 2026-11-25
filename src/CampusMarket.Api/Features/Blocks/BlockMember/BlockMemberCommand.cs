using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Blocks.BlockMember;

// The blocker comes from the token, the blocked member from the route.
public sealed record BlockMemberCommand(Guid BlockerId, Guid BlockedId) : ICommand<Guid>;
