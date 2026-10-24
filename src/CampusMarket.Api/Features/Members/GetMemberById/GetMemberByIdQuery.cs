using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetMemberById;

public sealed record GetMemberByIdQuery(Guid Id) : IQuery<GetMemberByIdResponse>;
