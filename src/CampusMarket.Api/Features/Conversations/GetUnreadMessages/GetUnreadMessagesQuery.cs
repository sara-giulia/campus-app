using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Conversations.GetUnreadMessages;

// The member comes from the token.
public sealed record GetUnreadMessagesQuery(Guid MemberId) : IQuery<GetUnreadMessagesResponse>;
