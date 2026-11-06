using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Domain.Favorites;

// A member saved someone else's listing to find it again later.
// A member has at most one favorite per listing (unique index in the database).
public sealed class Favorite
{
    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid ListingId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Favorite() { }

    public static Result<Favorite> Create(Guid memberId, Listing listing, DateTimeOffset createdAt)
    {
        if (memberId == Guid.Empty)
            return Result<Favorite>.Failure(
                Error.Validation("Favorite.MemberEmpty", "Member id must not be empty."));

        if (listing.SellerId == memberId)
            return Result<Favorite>.Failure(
                Error.Validation("Favorite.OwnListing", "A seller cannot add their own listing to favorites."));

        // A draft is not public yet, so it cannot be saved.
        if (listing.Status == ListingStatus.Draft)
            return Result<Favorite>.Failure(
                Error.Validation("Favorite.ListingNotAvailable", "Only a published listing can be added to favorites."));

        return new Favorite
        {
            Id = Guid.NewGuid(),
            MemberId = memberId,
            ListingId = listing.Id,
            CreatedAt = createdAt
        };
    }
}
