using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.GetMyConversations;

// The member comes from the token.
public sealed record GetMyConversationsQuery(Guid MemberId) : IQuery<GetMyConversationsResponse>;
