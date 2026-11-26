using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Conversations.GetMyConversations;

public sealed class GetMyConversationsHandler(
    IConversationRepository conversations,
    IListingRepository listings,
    IMemberRepository members)
    : IQueryHandler<GetMyConversationsQuery, GetMyConversationsResponse>
{
    public async Task<Result<GetMyConversationsResponse>> HandleAsync(GetMyConversationsQuery query, CancellationToken ct)
    {
        var mine = await conversations.GetForMemberAsync(query.MemberId, ct);
        if (mine.Count == 0)
            return new GetMyConversationsResponse([], 0);

        // The listings and the members exist: deleting one of them deletes its conversations (cascade).
        var titles = (await listings.GetAllAsync(ct)).ToDictionary(l => l.Id, l => l.Title);
        var names = (await members.GetAllAsync(ct)).ToDictionary(m => m.Id, m => m.Name);

        var items = mine
            .Select(c =>
            {
                // A conversation always has at least the first message.
                var last = c.Messages.MaxBy(m => m.Number)!;
                var other = c.OtherParticipant(query.MemberId);
                return new MyConversationItem(
                    c.Id,
                    c.ListingId,
                    titles[c.ListingId],
                    other,
                    names[other],
                    last.Text,
                    last.SenderId,
                    last.IsSuspicious,
                    c.LastMessageAt,
                    c.Messages.Count,
                    c.UnreadCountFor(query.MemberId));
            })
            .OrderByDescending(i => i.LastMessageAt)
            .ThenBy(i => i.Id)
            .ToList();

        return new GetMyConversationsResponse(items, items.Count);
    }
}
