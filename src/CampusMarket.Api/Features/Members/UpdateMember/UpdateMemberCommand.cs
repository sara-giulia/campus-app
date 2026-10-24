using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.UpdateMember;

public sealed record UpdateMemberCommand(Guid Id, string Name, string Email) : ICommand<Guid>;
