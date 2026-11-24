namespace CampusMarket.Api.Features.Categories.UpdateCategory;

/// <summary>
/// The JSON body of PUT /api/v1/categories/{category}. The category key comes from the route.
/// Both DisplayName and IsActive are required: they are nullable here only so that a missing
/// field reaches the handler as null and is answered with a 400 error, instead of becoming
/// an empty string or false (which would silently deactivate the category).
/// </summary>
public sealed record UpdateCategoryRequest(string? DisplayName, string? Description, bool? IsActive);
