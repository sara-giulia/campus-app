using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Covers CampusMarketSetup.ValidateCampusMarketProductionConfiguration (in Production,
// Jwt:Key must be at least 32 characters, and when an SMTP host is configured Email:UseSsl
// must be true) and SeedDemoDataIfRequested (when Seed:DemoData is true and the database
// is empty, seeds 3 published listings; a Student demo member is added only when
// Seed:DemoPassword has at least 8 characters). Each test starts its own copy of the API
// on the same in-memory database with WithWebHostBuilder, the same way SeedAdminTests do.
public sealed class ProductionConfigurationTests : IntegrationTestBase
{
    private const int LongEnoughJwtKeyLength = 40;
    private const int TooShortJwtKeyLength = 10;
    private const string DemoPassword = "demo-password-12";

    // Builds a test-only JWT key of the requested length by repeating a fixed character.
    // Never a real secret; the key is generated here, never read from configuration files.
    private static string MakeTestJwtKey(int length) => new('k', length);

    [Fact]
    public void Given_ProductionAndShortJwtKey_When_StartingTheApp_Then_ThrowsWithJwtKeyMessage()
    {
        // Arrange
        using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(TooShortJwtKeyLength));
        });

        // Act
        Action act = () => production.CreateClient();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt__Key*");
    }

    [Fact]
    public async Task Given_ProductionAndLongJwtKey_When_CallingHealth_Then_Returns200()
    {
        // Arrange
        await using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(LongEnoughJwtKeyLength));
        });
        var client = production.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Given_ProductionAndSmtpHostWithoutTls_When_StartingTheApp_Then_ThrowsWithUseSslMessage()
    {
        // Arrange
        using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(LongEnoughJwtKeyLength));
            builder.UseSetting("Email:Host", "smtp.example.com");
            builder.UseSetting("Email:UseSsl", "false");
        });

        // Act
        Action act = () => production.CreateClient();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Email:UseSsl*");
    }

    [Fact]
    public async Task Given_ProductionAndSmtpHostWithTls_When_CallingHealth_Then_Returns200()
    {
        // Arrange
        await using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(LongEnoughJwtKeyLength));
            builder.UseSetting("Email:Host", "smtp.example.com");
            builder.UseSetting("Email:UseSsl", "true");
        });
        var client = production.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_ProductionAndNoSmtpHost_When_CallingHealth_Then_Returns200()
    {
        // Arrange: no SMTP host means the LoggingEmailSender is used, so the UseSsl
        // check must not fire even when UseSsl is false.
        await using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(LongEnoughJwtKeyLength));
            builder.UseSetting("Email:UseSsl", "false");
        });
        var client = production.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_DemoDataWithValidPassword_When_StartingTheApp_Then_Seeds3ListingsAnd1StudentMember()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(3);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(1);
        members[0].Role.Should().Be(MemberRole.Student);
    }

    [Fact]
    public async Task Given_DemoDataWithoutPassword_When_StartingTheApp_Then_Seeds3ListingsAnd0Members()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Seed:DemoData", "true"));
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(3);

        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DemoDataNotSet_When_StartingTheApp_Then_Seeds0ListingsAnd0Members()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(_ => { });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(0);

        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(0);
    }
}
