using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Favorites.AddFavorite;

public sealed class AddFavoriteHandler(
    IListingRepository listings,
    IMemberRepository members,
    IFavoriteRepository favorites,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<AddFavoriteCommand, AddFavoriteResponse>
{
    public async Task<Result<AddFavoriteResponse>> HandleAsync(
        AddFavoriteCommand command, CancellationToken ct)
    {
        var listing = await listings.GetByIdAsync(command.ListingId, ct);

        // The listing of a suspended seller is not public while the suspension lasts.
        if (listing is null || await members.IsSuspendedAsync(listing.SellerId, timeProvider.GetUtcNow(), ct))
            return Result<AddFavoriteResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.ListingId}' was not found."));

        var member = await members.GetByIdAsync(command.MemberId, ct);

        if (member is null)
            return Result<AddFavoriteResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        var created = Favorite.Create(command.MemberId, listing, timeProvider.GetUtcNow());

        if (created.IsFailure)
            return Result<AddFavoriteResponse>.Failure(created.Error);

        var existing = await favorites.GetAsync(command.MemberId, command.ListingId, ct);

        if (existing is not null)
            return Result<AddFavoriteResponse>.Failure(
                Error.Conflict("Favorite.AlreadyExists", "This listing is already in your favorites."));

        var favorite = created.Value;

        var added = await favorites.AddAsync(favorite, ct);
        if (!added)
            return Result<AddFavoriteResponse>.Failure(
                Error.Conflict("Favorite.Duplicate", $"A favorite with id '{favorite.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return AddFavoriteResponse.From(favorite);
    }
}
