using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Blocks.UnblockMember;

// The blocker comes from the token, the member to unblock from the route.
public sealed record UnblockMemberCommand(Guid BlockerId, Guid BlockedId) : ICommand<Guid>;
