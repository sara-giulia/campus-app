using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Favorites.RemoveFavorite;

// The member comes from the token, the listing from the route.
public sealed record RemoveFavoriteCommand(Guid MemberId, Guid ListingId) : ICommand<Guid>;
