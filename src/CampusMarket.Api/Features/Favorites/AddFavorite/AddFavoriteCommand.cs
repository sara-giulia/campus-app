using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Favorites.AddFavorite;

// The member comes from the token, the listing from the route.
public sealed record AddFavoriteCommand(Guid MemberId, Guid ListingId) : ICommand<AddFavoriteResponse>;
