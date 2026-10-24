using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public sealed record CreateListingCommand(
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price) : ICommand<CreateListingResponse>;
