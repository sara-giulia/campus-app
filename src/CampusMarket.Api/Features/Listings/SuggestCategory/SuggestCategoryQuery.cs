using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.SuggestCategory;

public sealed record SuggestCategoryQuery(string Title, string? Description = null) : IQuery<SuggestCategoryResponse>;
