using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Categories.GetCategories;

public sealed record GetCategoriesQuery() : IQuery<GetCategoriesResponse>;
