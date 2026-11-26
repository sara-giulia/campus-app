using CampusMarket.Api.Features.Conversations.GetConversation;
using CampusMarket.Api.Features.Conversations.GetMyConversations;
using CampusMarket.Api.Features.Conversations.GetUnreadMessages;
using CampusMarket.Api.Features.Conversations.MarkConversationRead;
using CampusMarket.Api.Features.Conversations.SendMessage;
using CampusMarket.Api.Features.Conversations.StartConversation;

namespace CampusMarket.Api.Features.Conversations;

public static class ConversationsEndpoints
{
    public static IEndpointRouteBuilder MapConversationsEndpoints(this IEndpointRouteBuilder app)
    {
        // The buyer writes to the seller about a listing: /api/v1/listings/{listingId}/conversations
        StartConversationEndpoint.Map(app.MapGroup("/listings"));

        // Reading and answering: /api/v1/conversations/{id}, /api/v1/conversations/{id}/messages,
        // /api/v1/conversations/{id}/read
        var conversations = app.MapGroup("/conversations");
        GetConversationEndpoint.Map(conversations);
        SendMessageEndpoint.Map(conversations);
        MarkConversationReadEndpoint.Map(conversations);

        // My conversations, as buyer or as seller: /api/v1/members/me/conversations,
        // and how many messages I have not read: /api/v1/members/me/conversations/unread
        var members = app.MapGroup("/members");
        GetMyConversationsEndpoint.Map(members);
        GetUnreadMessagesEndpoint.Map(members);

        return app;
    }
}
