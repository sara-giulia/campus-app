namespace CampusMarket.Api.Features.Members.GetPublicProfile;

// Public data only: no email, no role.
public sealed record GetPublicProfileResponse(
    Guid Id,
    string Name,
    DateTimeOffset MemberSince,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<PublicListingItem> ActiveListings);

public sealed record PublicListingItem(Guid Id, string Title, decimal Price);