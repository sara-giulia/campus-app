using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.MarkConversationRead;

// The reader comes from the token. The result is how many messages were unread.
public sealed record MarkConversationReadCommand(Guid MemberId, Guid ConversationId) : ICommand<int>;
