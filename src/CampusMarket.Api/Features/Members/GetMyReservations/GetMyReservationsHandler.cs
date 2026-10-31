using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public sealed class GetMyReservationsHandler(
    IListingRepository repository,
    TimeProvider timeProvider)
    : IQueryHandler<GetMyReservationsQuery, GetMyReservationsResponse>
{
    public async Task<Result<GetMyReservationsResponse>> HandleAsync(
        GetMyReservationsQuery query, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var listings = await repository.GetAllAsync(ct);

        var items = listings
            .SelectMany(listing => listing.Reservations
                .Where(r => r.MemberId == query.MemberId)
                .Select(r => new MyReservationItem(
                    r.Id,
                    listing.Id,
                    listing.Title,
                    listing.Price.Amount,
                    // README: "expired reservations shown as expired". Nothing changes in the database.
                    r.IsExpired(now) ? ReservationStatus.Expired : r.Status,
                    r.CreatedAt,
                    r.ExpiresAt)))
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.ReservationId)
            .ToList();

        return new GetMyReservationsResponse(items, items.Count);
    }
}
