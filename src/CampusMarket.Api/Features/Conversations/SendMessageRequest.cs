namespace CampusMarket.Api.Features.Conversations;

// The body of both POST endpoints that send a message.
public sealed record SendMessageRequest(string? Text);
