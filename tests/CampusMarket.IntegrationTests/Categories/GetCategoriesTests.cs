using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Categories.GetCategories;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Categories;

public sealed class GetCategoriesTests : IntegrationTestBase
{
    // The 5 categories seeded by the AddCategories migration, in the order the API returns them (enum order).
    private static readonly GetCategoriesItem[] SeededCategories =
    [
        new(Category.Textbook, "Manuale", null, true),
        new(Category.Notes, "Notițe", null, true),
        new(Category.Electronics, "Electronice", null, true),
        new(Category.LabEquipment, "Echipament de laborator", null, true),
        new(Category.Other, "Altele", null, true)
    ];

    [Fact]
    public async Task Given_NoToken_When_GettingCategories_Then_Returns200WithTheFiveSeededCategories()
    {
        // Arrange: nothing to seed, the migration adds the 5 categories.

        // Act
        var response = await Client.GetAsync("/api/v1/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetCategoriesResponse>(Json);
        body!.Count.Should().Be(5);
        body.Items.Should().BeEquivalentTo(SeededCategories, options => options.WithStrictOrdering());
        body.Items.Should().OnlyContain(c => c.IsActive);

        var stored = await QueryDbAsync(db => db.Set<ListingCategory>().AsNoTracking().ToListAsync());
        stored.Should().BeEquivalentTo(SeededCategories);
    }
}
