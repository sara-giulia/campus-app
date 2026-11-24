using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Categories.UpdateCategory;

/// <summary>
/// <paramref name="Category"/> is the key as written in the route ("Textbook", case-insensitive);
/// the handler answers Category.NotFound when it is not one of the base categories.
/// </summary>
public sealed record UpdateCategoryCommand(
    string Category,
    string? DisplayName,
    string? Description,
    bool? IsActive) : ICommand<Category>;
