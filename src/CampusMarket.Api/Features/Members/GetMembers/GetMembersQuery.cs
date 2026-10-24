using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetMembers;

public sealed record GetMembersQuery() : IQuery<GetMembersResponse>;
