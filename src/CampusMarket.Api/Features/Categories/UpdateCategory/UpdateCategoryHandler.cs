using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryHandler(
    ICategoryRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCategoryCommand, Category>
{
    public async Task<Result<Category>> HandleAsync(
        UpdateCategoryCommand command, CancellationToken ct)
    {
        // Matched by name only: Enum.TryParse would also accept numbers ("1"), which are not keys.
        var key = Enum.GetValues<Category>()
            .Cast<Category?>()
            .FirstOrDefault(c => string.Equals(c.ToString(), command.Category, StringComparison.OrdinalIgnoreCase));

        // All the categories are loaded, because the "last active" rule needs the other ones.
        var categories = await repository.GetAllAsync(ct);
        var category = categories.FirstOrDefault(c => c.Key == key);

        if (category is null)
            return Result<Category>.Failure(ListingCategory.NotFound(command.Category));

        if (command.IsActive is not { } isActive)
            return Result<Category>.Failure(
                Error.Validation("Category.IsActiveRequired", "isActive is required."));

        var updated = category.Update(command.DisplayName, command.Description, isActive, categories);

        if (updated.IsFailure)
            return Result<Category>.Failure(updated.Error);

        repository.Update(category);
        await unitOfWork.CommitAsync(ct);

        return category.Key;
    }
}
