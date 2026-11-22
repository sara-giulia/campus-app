using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Features.Categories.GetCategories;

public sealed class GetCategoriesHandler(ICategoryRepository repository)
    : IQueryHandler<GetCategoriesQuery, GetCategoriesResponse>
{
    public async Task<Result<GetCategoriesResponse>> HandleAsync(
        GetCategoriesQuery query, CancellationToken ct)
    {
        var categories = await repository.GetAllAsync(ct);

        var items = categories
            .Select(GetCategoriesItem.From)
            .ToList();

        return new GetCategoriesResponse(items, items.Count);
    }
}
