using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reviews;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public sealed class GetMyReservationsHandler(
    IListingRepository repository,
    IReviewRepository reviews,
    TimeProvider timeProvider)
    : IQueryHandler<GetMyReservationsQuery, GetMyReservationsResponse>
{
    public async Task<Result<GetMyReservationsResponse>> HandleAsync(
        GetMyReservationsQuery query, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var listings = await repository.GetAllAsync(ct);
        // So the buyer's page knows which completed purchases still wait for a review.
        var reviewed = (await reviews.GetReviewedListingIdsAsync(query.MemberId, ct)).ToHashSet();

        var items = listings
            .SelectMany(listing => listing.Reservations
                .Where(r => r.MemberId == query.MemberId)
                .Select(r => new MyReservationItem(
                    r.Id,
                    listing.Id,
                    listing.Title,
                    listing.Price.Amount,
                    r.AgreedPrice,
                    // README: "expired reservations shown as expired". Nothing changes in the database.
                    r.IsExpired(now) ? ReservationStatus.Expired : r.Status,
                    r.CreatedAt,
                    r.ExpiresAt,
                    reviewed.Contains(listing.Id))))
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.ReservationId)
            .ToList();

        return new GetMyReservationsResponse(items, items.Count);
    }
}
