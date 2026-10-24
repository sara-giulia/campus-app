using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.DeleteMember;

public sealed record DeleteMemberCommand(Guid Id) : ICommand<Guid>;
