using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.UpdateMember;

public sealed record UpdateMemberCommand(Guid Id, Guid RequesterId, bool IsAdmin, string Name, string Email) : ICommand<Guid>;
