using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.ExpireReservations;

public sealed class ExpireReservationsHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ExpireReservationsCommand, int>
{
    public async Task<Result<int>> HandleAsync(
        ExpireReservationsCommand command, CancellationToken ct)
    {
        var listings = await repository.GetAllAsync(ct);
        var now = timeProvider.GetUtcNow();
        var total = 0;

        foreach (var listing in listings)
        {
            var expired = listing.ExpireReservations(now);
            if (expired == 0)
                continue;

            repository.Update(listing);
            total += expired;
        }

        if (total > 0)
            await unitOfWork.CommitAsync(ct);

        return total;
    }
}
