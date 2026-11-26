using CampusMarket.Api.Common.Events;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

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

        return [.. notifications, .. messages];
    }
}
