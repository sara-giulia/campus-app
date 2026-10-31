using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed record UpdateListingCommand(
    Guid Id,
    Guid RequesterId,
    bool IsAdmin,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price) : ICommand<Guid>;
