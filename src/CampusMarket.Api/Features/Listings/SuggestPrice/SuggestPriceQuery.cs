using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.SuggestPrice;

public sealed record SuggestPriceQuery(
    string Title,
    Category Category,
    Condition Condition,
    string? Description = null) : IQuery<SuggestPriceResponse>;
