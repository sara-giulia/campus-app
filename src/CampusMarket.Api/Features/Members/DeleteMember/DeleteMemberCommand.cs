using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.DeleteMember;

public sealed record DeleteMemberCommand(Guid Id, Guid RequesterId, bool IsAdmin) : ICommand<Guid>;
