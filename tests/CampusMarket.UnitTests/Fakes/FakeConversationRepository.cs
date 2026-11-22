using CampusMarket.Api.Domain.Conversations;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeConversationRepository : IConversationRepository
{
    public List<Conversation> Items { get; } = new();

    public Task AddAsync(Conversation conversation, CancellationToken ct = default)
    {
        Items.Add(conversation);
        return Task.CompletedTask;
    }

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var conversation = Items.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(conversation);
    }

    public Task<Conversation?> GetByListingAndBuyerAsync(Guid listingId, Guid buyerId, CancellationToken ct = default)
    {
        var conversation = Items.FirstOrDefault(c => c.ListingId == listingId && c.BuyerId == buyerId);
        return Task.FromResult(conversation);
    }

    public Task<IReadOnlyList<Conversation>> GetForMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        IReadOnlyList<Conversation> snapshot = Items
            .Where(c => c.BuyerId == memberId || c.SellerId == memberId)
            .ToList();
        return Task.FromResult(snapshot);
    }
}
