using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Conversations;

// A conversation between a buyer and the seller about one listing. Only the two of them can read it or
// write in it. A buyer has at most one conversation per listing (unique index in the database); the
// seller has one per interested buyer.
public sealed class Conversation
{
    private readonly List<Message> _messages = new();

    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid BuyerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }
    // The Number of the last message each side has seen; the messages of the other side after it are
    // unread. 0 means nothing seen yet. Writing a message also counts as having read the conversation.
    public int BuyerLastReadNumber { get; private set; }
    public int SellerLastReadNumber { get; private set; }
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    // The buyer starts the conversation with a first message.
    public static Result<Conversation> Start(Guid buyerId, Listing listing, string? text, DateTimeOffset now)
    {
        if (buyerId == Guid.Empty)
            return Result<Conversation>.Failure(
                Error.Validation("Conversation.BuyerEmpty", "Buyer id must not be empty."));

        if (listing.SellerId == buyerId)
            return Result<Conversation>.Failure(
                Error.Validation("Conversation.OwnListing", "A seller cannot start a conversation about their own listing."));

        // Only a listing that can still be bought: a draft is not public, a hidden one was taken down by an
        // Admin and a sold one is gone. An existing conversation keeps working whatever the listing becomes.
        if (listing.Status is not (ListingStatus.Active or ListingStatus.Reserved))
            return Result<Conversation>.Failure(
                Error.Validation("Conversation.ListingNotAvailable", "A conversation can only be started about a published listing."));

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            SellerId = listing.SellerId,
            BuyerId = buyerId,
            CreatedAt = now,
            LastMessageAt = now
        };

        var first = conversation.Send(buyerId, text, now);
        if (first.IsFailure)
            return Result<Conversation>.Failure(first.Error);

        return conversation;
    }

    public bool IsParticipant(Guid memberId) => memberId == SellerId || memberId == BuyerId;

    // The other member of the conversation, seen by one of the two.
    public Guid OtherParticipant(Guid memberId) => memberId == SellerId ? BuyerId : SellerId;

    // The messages of the other side that the member has not seen yet.
    public int UnreadCountFor(Guid memberId)
    {
        var lastRead = LastReadNumberOf(memberId);
        return _messages.Count(m => m.SenderId != memberId && m.Number > lastRead);
    }

    // The member opened the conversation: everything in it is read. Returns how many messages were unread.
    public Result<int> MarkReadBy(Guid memberId)
    {
        if (!IsParticipant(memberId))
            return Result<int>.Failure(
                Error.Forbidden("Conversation.NotParticipant", "Only the buyer and the seller can read this conversation."));

        var unread = UnreadCountFor(memberId);
        if (unread > 0)
            SetLastReadNumber(memberId, _messages.Max(m => m.Number));
        return unread;
    }

    private int LastReadNumberOf(Guid memberId) => memberId == SellerId ? SellerLastReadNumber : BuyerLastReadNumber;

    private void SetLastReadNumber(Guid memberId, int number)
    {
        if (memberId == SellerId)
            SellerLastReadNumber = number;
        else
            BuyerLastReadNumber = number;
    }

    public Result<Message> Send(Guid senderId, string? text, DateTimeOffset now)
    {
        if (!IsParticipant(senderId))
            return Result<Message>.Failure(
                Error.Forbidden("Conversation.NotParticipant", "Only the buyer and the seller can write in this conversation."));

        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            return Result<Message>.Failure(
                Error.Validation("Message.Empty", "The message must not be empty."));

        if (trimmed.Length > Message.MaxTextLength)
            return Result<Message>.Failure(
                Error.Validation("Message.TooLong", $"The message must have at most {Message.MaxTextLength} characters."));

        var message = new Message(Id, _messages.Count + 1, senderId, trimmed, now);
        _messages.Add(message);
        LastMessageAt = now;
        // The sender answers after what is already there, so they have seen it.
        SetLastReadNumber(senderId, message.Number);
        return message;
    }
}
