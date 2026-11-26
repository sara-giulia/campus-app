using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.Api.Features.Conversations.GetUnreadMessages;

public sealed class GetUnreadMessagesHandler(IConversationRepository conversations)
    : IQueryHandler<GetUnreadMessagesQuery, GetUnreadMessagesResponse>
{
    public async Task<Result<GetUnreadMessagesResponse>> HandleAsync(GetUnreadMessagesQuery query, CancellationToken ct)
    {
        var unread = (await conversations.GetForMemberAsync(query.MemberId, ct))
            .Select(c => c.UnreadCountFor(query.MemberId))
            .Where(count => count > 0)
            .ToList();

        return new GetUnreadMessagesResponse(unread.Sum(), unread.Count);
    }
}
