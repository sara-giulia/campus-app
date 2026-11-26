using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Blocks.GetMyBlocked;

public sealed record GetMyBlockedQuery(Guid MemberId) : IQuery<GetMyBlockedResponse>;
