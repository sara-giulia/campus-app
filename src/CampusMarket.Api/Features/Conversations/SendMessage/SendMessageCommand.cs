using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.SendMessage;

// The sender comes from the token.
public sealed record SendMessageCommand(Guid SenderId, Guid ConversationId, string? Text) : ICommand<MessageResponse>;
