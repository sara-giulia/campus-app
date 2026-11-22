using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Conversations.GetConversation;

// Only the buyer and the seller can read the conversation; anyone else (an Admin too) gets 403.
public sealed class GetConversationHandler(
    IConversationRepository conversations,
    IListingRepository listings,
    IMemberRepository members)
    : IQueryHandler<GetConversationQuery, GetConversationResponse>
{
    public async Task<Result<GetConversationResponse>> HandleAsync(GetConversationQuery query, CancellationToken ct)
    {
        var conversation = await conversations.GetByIdAsync(query.ConversationId, ct);
        if (conversation is null)
            return Result<GetConversationResponse>.Failure(
                Error.NotFound("Conversation.NotFound", $"Conversation '{query.ConversationId}' was not found."));

        if (!conversation.IsParticipant(query.MemberId))
            return Result<GetConversationResponse>.Failure(
                Error.Forbidden("Conversation.NotParticipant", "Only the buyer and the seller can read this conversation."));

        // The listing and the two members exist: deleting one of them deletes the conversation (cascade).
        var listing = (await listings.GetByIdAsync(conversation.ListingId, ct))!;
        var seller = (await members.GetByIdAsync(conversation.SellerId, ct))!;
        var buyer = (await members.GetByIdAsync(conversation.BuyerId, ct))!;

        var messages = conversation.Messages
            .OrderBy(m => m.Number)
            .Select(ConversationMessage.From)
            .ToList();

        return new GetConversationResponse(
            conversation.Id,
            listing.Id,
            listing.Title,
            seller.Id,
            seller.Name,
            buyer.Id,
            buyer.Name,
            conversation.CreatedAt,
            messages);
    }
}
