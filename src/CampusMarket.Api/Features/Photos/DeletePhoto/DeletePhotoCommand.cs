using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.DeletePhoto;

// The requester comes from the token.
public sealed record DeletePhotoCommand(Guid RequesterId, Guid ListingId, Guid PhotoId) : ICommand<Guid>;
