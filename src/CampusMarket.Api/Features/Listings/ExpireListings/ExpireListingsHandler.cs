using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Listings.ExpireListings;

// Expires every Active listing whose ExpiresAt has passed. The seller gets an in-app notification
// (saved with the listing, in the same commit) and an email with a link to My listings, where the
// "Reînnoiește" button renews the listing.
public sealed partial class ExpireListingsHandler(
    IListingRepository repository,
    IMemberRepository members,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider,
    ILogger<ExpireListingsHandler> logger)
    : ICommandHandler<ExpireListingsCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireListingsCommand command, CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);
        var now = timeProvider.GetUtcNow();
        var expired = new List<Listing>();

        foreach (var listing in listings)
        {
            if (!listing.Expire(now))
                continue;

            repository.Update(listing);
            expired.Add(listing);

            var notification = Notification.Create(
                listing.SellerId,
                NotificationType.ListingExpired,
                $"Your listing {listing.Title} expired",
                listing.Id,
                now);
            if (notification.IsSuccess)
                notifications.Add(notification.Value);
        }

        if (expired.Count == 0)
            return 0;

        await unitOfWork.CommitAsync(ct);

        // The emails go out only after the listings are saved, so a seller is never told about an
        // expiry that is not in the database. A failed email does not undo the expiry or stop the others.
        var link = $"{frontendOptions.Value.BaseUrl.TrimEnd('/')}/my/listings";
        foreach (var listing in expired)
            await SendEmailAsync(listing, link, ct);

        return expired.Count;
    }

    private async Task SendEmailAsync(Listing listing, string link, CancellationToken ct)
    {
        var seller = await members.GetByIdAsync(listing.SellerId, ct);
        if (seller is null)
            return;

        var body =
            $"Hello {seller.Name},{Environment.NewLine}{Environment.NewLine}" +
            $"Your listing \"{listing.Title}\" expired and is no longer shown in the search.{Environment.NewLine}" +
            $"Open My listings and press \"Reînnoiește\" to publish it again:{Environment.NewLine}" +
            $"{link}";

        try
        {
            await emailSender.SendAsync(seller.Email.Value, "Your CampusMarket listing expired", body, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEmailFailed(logger, listing.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Listing expiry email for listing {ListingId} could not be sent.")]
    private static partial void LogEmailFailed(ILogger logger, Guid listingId, Exception exception);
}
