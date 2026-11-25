using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Favorites.GetMyFavorites;

public sealed class GetMyFavoritesHandler(
    IFavoriteRepository favorites,
    IListingRepository listings)
    : IQueryHandler<GetMyFavoritesQuery, GetMyFavoritesResponse>
{
    public async Task<Result<GetMyFavoritesResponse>> HandleAsync(
        GetMyFavoritesQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
            return Result<GetMyFavoritesResponse>.Failure(
                Error.Validation("Favorites.InvalidPage", "Page must be at least 1."));
        if (query.PageSize < 1 || query.PageSize > GetMyFavoritesQuery.MaxPageSize)
            return Result<GetMyFavoritesResponse>.Failure(
                Error.Validation("Favorites.InvalidPageSize",
                    $"Page size must be between 1 and {GetMyFavoritesQuery.MaxPageSize}."));

        var memberFavorites = await favorites.GetByMemberAsync(query.MemberId, ct);
        var allListings = await listings.GetAllAsync(ct);
        var listingsById = allListings.ToDictionary(l => l.Id);

        // Deleting a listing deletes its favorites (cascade), so every favorite has its listing.
        // A listing hidden by an Admin or expired stays saved but is not shown (and not counted) while it
        // is in that status; a renewed listing comes back.
        var ordered = memberFavorites
            .Where(f => listingsById.TryGetValue(f.ListingId, out var listing)
                        && listing.Status is not (ListingStatus.Hidden or ListingStatus.Expired))
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.ListingId)
            .ToList();

        var items = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(f => MyFavoriteItem.From(f, listingsById[f.ListingId]))
            .ToList();

        return new GetMyFavoritesResponse(items, items.Count, ordered.Count, query.Page, query.PageSize);
    }
}
