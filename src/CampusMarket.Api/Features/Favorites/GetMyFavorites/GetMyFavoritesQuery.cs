using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Favorites.GetMyFavorites;

// The member comes from the token. Same paging rules as GetListings.
public sealed record GetMyFavoritesQuery(
    Guid MemberId,
    int Page = GetMyFavoritesQuery.DefaultPage,
    int PageSize = GetMyFavoritesQuery.DefaultPageSize) : IQuery<GetMyFavoritesResponse>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
