using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Photos.GenerateListingImage;

// Sent by the background service (ImageGenerationService) for a published listing; there is no endpoint.
public sealed record GenerateListingImageCommand(Guid ListingId) : ICommand<PhotoResponse>;
