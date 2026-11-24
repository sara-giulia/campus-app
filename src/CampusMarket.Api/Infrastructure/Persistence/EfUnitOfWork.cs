using CampusMarket.Api.Common.Events;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class EfUnitOfWork(CampusMarketDbContext db, ILiveEvents liveEvents) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct)
    {
        // Read what is new before saving (afterwards every entity is Unchanged), and publish only
        // after the save worked, so a member is never told about something that is not in the database.
        var pending = PendingEvents();

        var saved = await db.SaveChangesAsync(ct);

        foreach (var (memberId, liveEvent) in pending)
            liveEvents.Publish(memberId, liveEvent);

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
