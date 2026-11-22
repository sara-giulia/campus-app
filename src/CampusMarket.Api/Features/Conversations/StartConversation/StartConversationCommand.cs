using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.StartConversation;

// The buyer comes from the token.
public sealed record StartConversationCommand(Guid BuyerId, Guid ListingId, string? Text) : ICommand<MessageResponse>;
