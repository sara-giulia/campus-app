using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.CreateMember;

public sealed record CreateMemberCommand(string Name, string Email) : ICommand<CreateMemberResponse>;
