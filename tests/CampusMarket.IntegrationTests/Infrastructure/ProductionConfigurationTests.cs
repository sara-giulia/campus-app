using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetListings;
using CampusMarket.Api.Infrastructure.Mailing;
using CampusMarket.Api.Infrastructure.Persistence.Seed;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Covers CampusMarketSetup.ValidateCampusMarketProductionConfiguration (in Production,
// Jwt:Key must be at least 32 characters, and when an SMTP host is configured Email:UseSsl
// must be true) and SeedDemoDataIfRequested (when Seed:DemoData is true and the database
// is empty, seeds the DemoListings catalog; demo Student members are added only when
// Seed:DemoPassword has at least 8 characters). Each test starts its own copy of the API
// on the same database with WithWebHostBuilder, the same way SeedAdminTests do.
public sealed class ProductionConfigurationTests : IntegrationTestBase
{
    private const int LongEnoughJwtKeyLength = 40;
    private const int TooShortJwtKeyLength = 10;
    private const string DemoPassword = "demo-password-12";
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

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
    public async Task Given_ProductionAndBrevoApiKey_When_Starting_Then_RegistersBrevoEmailSender()
    {
        // Arrange: Email__ApiKey wins over Email:Host, so BrevoEmailSender is registered
        // (Render free blocks outbound SMTP ports).
        const string variable = "Email__ApiKey";
        var saved = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "brevo-prod-test-key");
        try
        {
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
            production.Services.GetRequiredService<IEmailSender>().Should().BeOfType<BrevoEmailSender>();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, saved);
        }
    }

    [Fact]
    public async Task Given_DemoDataWithValidPassword_When_StartingTheApp_Then_SeedsDemoCatalogAndStudentMembers()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(DemoListings.ListingCount);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(DemoListings.MemberCount);
        members.Should().OnlyContain(m => m.Role == MemberRole.Student);
    }

    [Fact]
    public async Task Given_DemoDataWithValidPassword_When_RequestingSecondPage_Then_PagesThroughSeededListings()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var firstPage = await client.GetAsync("/api/v1/listings?page=1&pageSize=20");
        var secondPage = await client.GetAsync("/api/v1/listings?page=2&pageSize=20");

        // Assert
        firstPage.StatusCode.Should().Be(HttpStatusCode.OK);
        secondPage.StatusCode.Should().Be(HttpStatusCode.OK);

        var first = await firstPage.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        var second = await secondPage.Content.ReadFromJsonAsync<GetListingsResponse>(Json);

        first!.Page.Should().Be(1);
        first.PageSize.Should().Be(20);
        first.Count.Should().Be(20);
        first.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        second!.Page.Should().Be(2);
        second.PageSize.Should().Be(20);
        second.Count.Should().Be(20);
        second.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        // The two pages return different listings.
        first.Items.Select(i => i.Id).Should().NotIntersectWith(second.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Given_DemoDataWithoutPassword_When_StartingTheApp_Then_SeedsDemoCatalogAnd0Members()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Seed:DemoData", "true"));
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        // No demo members means a single orphan seller id, so Reserve (buyer != seller)
        // can never happen and every seeded listing stays Active.
        body!.TotalCount.Should().Be(DemoListings.ListingCount);

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

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ProductionAndWhitespaceJwtKey_When_StartingTheApp_Then_ThrowsWithJwtKeyMessage()
    {
        // Arrange
        await using var production = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", "   ");
        });

        // Act
        Action act = () => production.CreateClient();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt__Key*");

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(0);
        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DemoDataAndAMemberAlreadyInTheDatabase_When_StartingTheApp_Then_StillSeedsTheDemoCatalog()
    {
        // Arrange: a pre-existing member (an Admin seeded by SeedAdmin, or a
        // real sign-up) must NOT block the demo listings seed. This was the
        // Render regression: with Seed__AdminEmail set, SeedAdmin ran first,
        // db.Members.Any() became true, and the demo catalog was never seeded.
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(DemoListings.ListingCount);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        // The pre-existing member is kept alongside the freshly-seeded demo
        // Students (+1 non-demo member).
        members.Should().HaveCount(DemoListings.MemberCount + 1);
        members.Should().ContainSingle(m => m.Id == member.Id);
    }

    [Fact]
    public async Task Given_ProductionAndAdminSeedAndDemoData_When_StartingTheApp_Then_SeedsAdminAndDemoCatalog()
    {
        // Arrange: this is the exact Render scenario. ASPNETCORE_ENVIRONMENT=Production,
        // Seed__DemoData=true, Seed__DemoPassword set, AND left-over Seed__AdminEmail /
        // Seed__AdminPassword env vars that cause SeedAdmin to run first.
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", MakeTestJwtKey(LongEnoughJwtKeyLength));
            builder.UseSetting("Seed:AdminEmail", "admin@campusmarket.ro");
            builder.UseSetting("Seed:AdminPassword", "Admin12345");
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert: the demo catalog is seeded next to the admin member.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(DemoListings.ListingCount);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        members.Should().HaveCount(DemoListings.MemberCount + 1);
        members.Should().ContainSingle(m => m.Role == MemberRole.Admin);
    }

    [Fact]
    public async Task Given_DemoDataAndADemoEmailAlreadyInTheDatabase_When_StartingTheApp_Then_SkipsTheCollidingMemberAndSeedsTheRest()
    {
        // Arrange: a member already uses one of the demo emails (e.g. an Admin
        // reserved "demo@campusmarket.ro"). The unique email index would blow
        // up SaveChanges if we inserted a second member with that address, so
        // the colliding demo member must be skipped — the rest still seed.
        var existing = Member.Create("Demo Admin", "demo@campusmarket.ro", CreatedAt).Value;
        await SeedAsync(existing);
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        var members = await QueryDbAsync(db => db.Set<Member>().AsNoTracking().ToListAsync());
        // Pre-existing member + the demo members whose emails don't collide.
        members.Should().HaveCount(DemoListings.MemberCount);
    }

    [Fact]
    public async Task Given_DemoDataSeededOnce_When_RestartingTheApp_Then_DoesNotDoubleSeed()
    {
        // Arrange: start once to seed, then dispose and start again on the
        // same database. A second seed would double-insert
        // listings and break the unique email index on the demo members.
        await using (var firstStart = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        }))
        {
            firstStart.CreateClient().Dispose();
        }

        await using var secondStart = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
        });
        var client = secondStart.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert: still exactly one copy of the catalog.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        body!.TotalCount.Should().Be(DemoListings.ActiveListingCount);

        var listingCount = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listingCount.Should().Be(DemoListings.ListingCount);

        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(DemoListings.MemberCount);
    }

    [Fact]
    public async Task Given_DemoDataAndHasherReturnsEmptyHash_When_StartingTheApp_Then_SeedsDemoCatalogAnd0Members()
    {
        // Arrange
        await using var seeded = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:DemoData", "true");
            builder.UseSetting("Seed:DemoPassword", DemoPassword);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, EmptyHashPasswordHasher>();
            });
        });
        var client = seeded.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/listings?pageSize=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetListingsResponse>(Json);
        // Empty hash fails Member.Register (PasswordRequired), so no demo members are
        // created, the fallback uses a single orphan seller id and every listing is Active.
        body!.TotalCount.Should().Be(DemoListings.ListingCount);

        var memberCount = await QueryDbAsync(db => db.Set<Member>().CountAsync());
        memberCount.Should().Be(0);
        var listings = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().ToListAsync());
        listings.Should().HaveCount(DemoListings.ListingCount);
        listings.Select(l => l.SellerId).Distinct().Should().ContainSingle()
            .Which.Should().NotBe(Guid.Empty);
    }
}
