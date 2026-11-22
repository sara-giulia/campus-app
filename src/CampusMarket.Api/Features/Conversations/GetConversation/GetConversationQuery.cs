using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.GetConversation;

// The reader comes from the token.
public sealed record GetConversationQuery(Guid MemberId, Guid ConversationId) : IQuery<GetConversationResponse>;
