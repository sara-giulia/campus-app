using CampusMarket.Api.Features.Categories.GetCategories;
using CampusMarket.Api.Features.Categories.UpdateCategory;

namespace CampusMarket.Api.Features.Categories;

public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories");   // => /api/v1/categories

        GetCategoriesEndpoint.Map(group);
        UpdateCategoryEndpoint.Map(group);

        return app;
    }
}
