using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;

namespace CampusMarket.Api.Features.Favorites.RemoveFavorite;

public sealed class RemoveFavoriteHandler(
    IFavoriteRepository favorites,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveFavoriteCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        RemoveFavoriteCommand command, CancellationToken ct)
    {
        // Only the member's own favorite is looked up, so nobody can remove someone else's.
        var favorite = await favorites.GetAsync(command.MemberId, command.ListingId, ct);

        if (favorite is null)
            return Result<Guid>.Failure(
                Error.NotFound("Favorite.NotFound", $"Listing '{command.ListingId}' is not in your favorites."));

        favorites.Delete(favorite);
        await unitOfWork.CommitAsync(ct);

        return command.ListingId;
    }
}
