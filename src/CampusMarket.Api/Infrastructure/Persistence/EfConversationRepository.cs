using CampusMarket.Api.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfConversationRepository(CampusMarketDbContext db) : IConversationRepository
{
    public Task AddAsync(Conversation conversation, CancellationToken ct = default)
    {
        db.Conversations.Add(conversation);
        return Task.CompletedTask;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Conversation?> GetByListingAndBuyerAsync(Guid listingId, Guid buyerId, CancellationToken ct = default) =>
        db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.ListingId == listingId && c.BuyerId == buyerId, ct);

    public async Task<IReadOnlyList<Conversation>> GetForMemberAsync(Guid memberId, CancellationToken ct = default) =>
        await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .Where(c => c.SellerId == memberId || c.BuyerId == memberId)
            .ToListAsync(ct);
}
