using CampusMarket.Api.Common.Events;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CampusMarket.Api.Infrastructure.Persistence;

// After a save that added or changed a listing it also rings ListingIndexSignal, so the listing gets
// (or updates) its vector for the search by meaning.
public sealed class EfUnitOfWork(CampusMarketDbContext db, ILiveEvents liveEvents, ListingIndexSignal listingIndex) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct)
    {
        // Read what is new before saving (afterwards every entity is Unchanged), and publish only
        // after the save worked, so a member is never told about something that is not in the database.
        var pending = PendingEvents();
        var listingsChanged = db.ChangeTracker.Entries<Listing>()
            .Any(e => e.State is EntityState.Added or EntityState.Modified);

        var saved = await db.SaveChangesAsync(ct);

        foreach (var (memberId, liveEvent) in pending)
            liveEvents.Publish(memberId, liveEvent);
        if (listingsChanged)
            listingIndex.Notify();

        return saved;
    }

    // A new notification goes to its member; a new message goes to the other member of its
    // conversation (the conversation is tracked: the handler loaded or created it to add the message).
    // A conversation marked read goes to the member who read it, so the badge in their other tabs
    // updates too. Sending a message also moves the sender's read mark, but that is not a "read".
    private List<(Guid MemberId, LiveEvent Event)> PendingEvents()
    {
        var added = db.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var notifications = added.OfType<Notification>()
            .Select(n => (n.MemberId, LiveEvent.Notification));

        var conversations = db.ChangeTracker.Entries<Conversation>().Select(e => e.Entity);
        var messages = added.OfType<Message>()
            .Join(conversations, m => m.ConversationId, c => c.Id,
                (m, c) => (c.OtherParticipant(m.SenderId), LiveEvent.Message(m.ConversationId)));

        var withNewMessages = added.OfType<Message>().Select(m => m.ConversationId).ToHashSet();
        var reads = db.ChangeTracker.Entries<Conversation>()
            .Where(e => e.State == EntityState.Modified && !withNewMessages.Contains(e.Entity.Id))
            .SelectMany(e => ReadBy(e).Select(memberId => (memberId, LiveEvent.Read(e.Entity.Id))));

        return [.. notifications, .. messages, .. reads];
    }

    private static IEnumerable<Guid> ReadBy(EntityEntry<Conversation> entry)
    {
        if (entry.Property(c => c.BuyerLastReadNumber).IsModified)
            yield return entry.Entity.BuyerId;
        if (entry.Property(c => c.SellerLastReadNumber).IsModified)
            yield return entry.Entity.SellerId;
    }
}
