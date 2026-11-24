using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Categories.GetCategories;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Categories.GetCategories;

public class GetCategoriesHandlerTests
{
    private static readonly Category[] KeysInOrder =
        [Category.Textbook, Category.Notes, Category.Electronics, Category.LabEquipment, Category.Other];

    private static readonly string[] DisplayNamesInOrder =
        ["Manuale", "Notițe", "Electronice", "Echipament de laborator", "Altele"];

    [Fact]
    public async Task Given_FiveCategories_When_Handling_Then_ReturnsAllWithTheirValuesInKeyOrder()
    {
        var repository = new FakeCategoryRepository();
        var handler = new GetCategoriesHandler(repository);

        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(5);
        result.Value.Items.Select(i => i.Key).Should().Equal(KeysInOrder);
        result.Value.Items.Select(i => i.DisplayName).Should().Equal(DisplayNamesInOrder);
        result.Value.Items.Should().OnlyContain(i => i.Description == null && i.IsActive);
        repository.Items.Should().HaveCount(5);
        repository.UpdateCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_InactiveCategory_When_Handling_Then_ReturnsItWithIsActiveFalse()
    {
        var repository = new FakeCategoryRepository();
        repository.Deactivate(Category.Electronics);
        var handler = new GetCategoriesHandler(repository);

        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => !i.IsActive)
            .Which.Key.Should().Be(Category.Electronics);
        repository.Items[Category.Electronics].IsActive.Should().BeFalse();
        repository.Items.Should().HaveCount(5);
    }
}
