using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Categories.GetCategories;
using CampusMarket.Api.Features.Categories.UpdateCategory;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Categories;

public sealed class UpdateCategoryTests : IntegrationTestBase
{
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string TextbookUrl = "/api/v1/categories/Textbook";

    // The 5 categories seeded by the AddCategories migration.
    private static readonly GetCategoriesItem[] SeededCategories =
    [
        new(Category.Textbook, "Manuale", null, true),
        new(Category.Notes, "Notițe", null, true),
        new(Category.Electronics, "Electronice", null, true),
        new(Category.LabEquipment, "Echipament de laborator", null, true),
        new(Category.Other, "Altele", null, true)
    ];

    [Fact]
    public async Task Given_Admin_When_UpdatingCategory_Then_Returns204AndGetShowsTheNewValues()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest("  Cărți de curs  ", "  Manuale și cărți pentru cursuri.  ", false);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        var categories = await Client.GetFromJsonAsync<GetCategoriesResponse>("/api/v1/categories", Json);
        categories!.Items.Should().ContainSingle(c => c.Key == Category.Textbook)
            .Which.Should().Be(new GetCategoriesItem(Category.Textbook, "Cărți de curs", "Manuale și cărți pentru cursuri.", false));

        var stored = await QueryDbAsync(db => db.Set<ListingCategory>().AsNoTracking().ToListAsync());
        stored.Should().BeEquivalentTo(SeededCategories.Skip(1).Append(
            new GetCategoriesItem(Category.Textbook, "Cărți de curs", "Manuale și cărți pentru cursuri.", false)));
    }

    [Fact]
    public async Task Given_NoToken_When_UpdatingCategory_Then_Returns401AndKeepsCategories()
    {
        // Arrange
        var request = new UpdateCategoryRequest("Cărți", null, false);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
        problem.Code.Should().Be("Auth.Unauthorized");
        problem.Instance.Should().Be(TextbookUrl);
        problem.TraceId.Should().NotBeEmpty();

        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_Member_When_UpdatingCategory_Then_Returns403AndKeepsCategories()
    {
        // Arrange
        AuthenticateAs(MemberId);
        var request = new UpdateCategoryRequest("Cărți", null, false);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
        problem.Code.Should().Be("Auth.Forbidden");
        problem.Instance.Should().Be(TextbookUrl);
        problem.TraceId.Should().NotBeEmpty();

        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_UnknownCategory_When_UpdatingCategory_Then_Returns404AndKeepsCategories()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest("Cărți", null, true);

        // Act
        var response = await Client.PutAsJsonAsync("/api/v1/categories/Books", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Category.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);

        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_MissingIsActive_When_UpdatingCategory_Then_Returns400AndKeepsCategories()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest("Cărți", null, null);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Category.IsActiveRequired");
        error.Type.Should().Be(ErrorType.Validation);

        await AssertCategoriesUnchangedAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Given_EmptyDisplayName_When_UpdatingCategory_Then_Returns400AndKeepsCategories(string? displayName)
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest(displayName, null, true);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        await AssertValidationErrorAsync(response, "Category.DisplayNameEmpty");
        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_TooLongDisplayName_When_UpdatingCategory_Then_Returns400AndKeepsCategories()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest(new string('a', ListingCategory.MaxDisplayNameLength + 1), null, true);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        await AssertValidationErrorAsync(response, "Category.DisplayNameTooLong");
        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_TooLongDescription_When_UpdatingCategory_Then_Returns400AndKeepsCategories()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest("Cărți", new string('a', ListingCategory.MaxDescriptionLength + 1), true);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        await AssertValidationErrorAsync(response, "Category.DescriptionTooLong");
        await AssertCategoriesUnchangedAsync();
    }

    [Fact]
    public async Task Given_LastActiveCategory_When_DeactivatingIt_Then_Returns409AndItStaysActive()
    {
        // Arrange
        await DeactivateAsync(Category.Notes, Category.Electronics, Category.LabEquipment, Category.Other);
        AuthenticateAs(AdminId, "Admin");
        var request = new UpdateCategoryRequest("Manuale", null, false);

        // Act
        var response = await Client.PutAsJsonAsync(TextbookUrl, request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Category.LastActive");
        error.Type.Should().Be(ErrorType.Conflict);

        var textbook = await QueryDbAsync(db =>
            db.Set<ListingCategory>().AsNoTracking().SingleAsync(c => c.Key == Category.Textbook));
        textbook.IsActive.Should().BeTrue();
        textbook.DisplayName.Should().Be("Manuale");

        var activeCount = await QueryDbAsync(db => db.Set<ListingCategory>().CountAsync(c => c.IsActive));
        activeCount.Should().Be(1);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string expectedCode)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be(expectedCode);
        error.Type.Should().Be(ErrorType.Validation);
    }

    // Arrange: deactivates the categories straight in this test's own database (other tests have their own).
    private async Task DeactivateAsync(params Category[] keys)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        var all = await db.Set<ListingCategory>().ToListAsync();

        foreach (var category in all.Where(c => keys.Contains(c.Key)))
            category.Update(category.DisplayName, category.Description, isActive: false, all).IsSuccess.Should().BeTrue();

        await db.SaveChangesAsync();
    }

    private async Task AssertCategoriesUnchangedAsync()
    {
        var stored = await QueryDbAsync(db => db.Set<ListingCategory>().AsNoTracking().ToListAsync());
        stored.Should().BeEquivalentTo(SeededCategories);
    }
}
