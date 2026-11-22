namespace CampusMarket.Api.Domain.Conversations;

public interface IConversationRepository
{
    /// <summary>
    /// Adds a new conversation with its messages. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    Task AddAsync(Conversation conversation, CancellationToken ct = default);

    /// <summary>
    /// The conversation with all its messages, or null. Changes to it are saved by IUnitOfWork.CommitAsync.
    /// </summary>
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The buyer's conversation about this listing (with its messages), or null.
    /// </summary>
    Task<Conversation?> GetByListingAndBuyerAsync(Guid listingId, Guid buyerId, CancellationToken ct = default);

    /// <summary>
    /// A snapshot of every conversation where the member is the buyer or the seller, with their messages,
    /// in no particular order.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetForMemberAsync(Guid memberId, CancellationToken ct = default);
}
