using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Common.Events;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Offers;

namespace CampusMarket.Api.Features.Moderation.SuspendMember;

// An Admin suspends a member, until a date or permanently, always with a reason. In one commit:
// - the member is suspended and their pending reports are confirmed;
// - every active refresh token is revoked (login and refresh refuse a suspended member);
// - the active reservations where they are the seller or the buyer are cancelled, the other side is notified;
// - their Pending offers (as buyer or seller) are closed, the other side is notified.
// Their listings are not changed: every public query leaves out the listings of a suspended member, so
// they come back by themselves when the suspension ends or is lifted. Conversations stay.
// After the commit: the suspension check cache is cleared (old access tokens stop working at once),
// the member's live connections are closed and the email is sent (a failed email does not undo anything).
public sealed partial class SuspendMemberHandler(
    IMemberRepository members,
    IMemberReportRepository reports,
    IRefreshTokenRepository refreshTokens,
    IListingRepository listings,
    IOfferRepository offers,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    ISuspensionCheck suspensionCheck,
    ILiveEvents liveEvents,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<SuspendMemberHandler> logger)
    : ICommandHandler<SuspendMemberCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(SuspendMemberCommand command, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(command.MemberId, ct);
        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        var now = timeProvider.GetUtcNow();
        var suspended = member.Suspend(command.AdminId, command.Reason, command.Until, now);
        if (suspended.IsFailure)
            return Result<Guid>.Failure(suspended.Error);

        foreach (var report in await reports.GetPendingByMemberAsync(member.Id, ct))
            report.Confirm(command.AdminId, now);

        foreach (var token in await refreshTokens.GetActiveByMemberAsync(member.Id, now, ct))
        {
            token.Revoke(now);
            refreshTokens.Update(token);
        }

        var allListings = await listings.GetAllAsync(ct);
        CancelReservations(member.Id, allListings, now);
        await CloseOffersAsync(member.Id, allListings, now, ct);

        members.Update(member);
        await unitOfWork.CommitAsync(ct);

        suspensionCheck.Invalidate();
        liveEvents.Disconnect(member.Id);
        await SendEmailAsync(member, ct);

        return member.Id;
    }

    // A listing has at most one active reservation (see Listing.Reserve).
    private void CancelReservations(Guid memberId, IReadOnlyList<Listing> allListings, DateTimeOffset now)
    {
        foreach (var listing in allListings)
        {
            var active = listing.Reservations.FirstOrDefault(r => r.Status == ReservationStatus.Active);
            if (active is null)
                continue;

            (Guid NotifiedId, string Message) notice;
            if (listing.SellerId == memberId)
                notice = (active.MemberId, $"Your reservation for {listing.Title} was cancelled: the seller is no longer available");
            else if (active.MemberId == memberId)
                notice = (listing.SellerId, $"The reservation of {listing.Title} was cancelled: the buyer is no longer available");
            else
                continue;

            // Cannot fail: the reservation is active, so the listing is Reserved.
            listing.CancelReservation(active.Id);
            listings.Update(listing);

            var notification = Notification.Create(
                notice.NotifiedId, NotificationType.ReservationCancelled, notice.Message, listing.Id, now);
            if (notification.IsSuccess)
                notifications.Add(notification.Value);
        }
    }

    private async Task CloseOffersAsync(Guid memberId, IReadOnlyList<Listing> allListings, DateTimeOffset now, CancellationToken ct)
    {
        var titles = allListings.ToDictionary(l => l.Id, l => l.Title);

        foreach (var offer in await offers.GetPendingForMemberAsync(memberId, ct))
        {
            if (!offer.CloseBecauseMemberSuspended(now))
                continue;

            var otherId = offer.BuyerId == memberId ? offer.SellerId : offer.BuyerId;
            // Deleting a listing deletes its offers (cascade), so every offer has its listing.
            var title = titles.GetValueOrDefault(offer.ListingId, string.Empty);
            OfferNotifications.Add(notifications, otherId, NotificationType.OfferRejected,
                OfferNotifications.ClosedBecauseMemberUnavailable(offer.Amount, title), offer.ListingId, now);
        }
    }

    private async Task SendEmailAsync(Member member, CancellationToken ct)
    {
        try
        {
            await emailSender.SendAsync(
                member.Email.Value, SuspensionEmails.SuspendedSubject, SuspensionEmails.SuspendedBody(member), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEmailFailed(logger, member.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suspension email for member {MemberId} could not be sent.")]
    private static partial void LogEmailFailed(ILogger logger, Guid memberId, Exception exception);
}
