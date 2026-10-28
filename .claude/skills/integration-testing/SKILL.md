---
name: integration-testing
description: Generates xUnit integration tests for CampusMarket that send real HTTP requests through WebApplicationFactory<Program> to the API running on an in-memory SQLite database, and check the status code, the response body and the database state through the DbContext. Use when adding or changing integration tests, or when a slice has no integration test yet.
---

# Integration testing skill for CampusMarket

> **Status: v3.** Checked against the EF Core code in `Infrastructure/Persistence/` and `CampusMarketSetup`.

## 1. Project facts (do not change these)

- .NET 10 minimal API in `src/CampusMarket.Api`, root namespace `CampusMarket.Api`. Routes are under `/api/v1`.
- Endpoints call CQRS handlers; handlers use the repositories and `IUnitOfWork`. The tests do not call handlers or repositories: they go through HTTP only.
- The database is SQLite through EF Core. DbContext: `CampusMarketDbContext` in `CampusMarket.Api.Infrastructure.Persistence`, registered in `CampusMarketSetup.AddCampusMarket` with `AddDbContext<CampusMarketDbContext>((sp, options) => options.UseSqlite(...))` and the connection string `CampusMarket` from `appsettings.json`.
- `Program.cs` calls `app.EnsureCampusMarketDatabase()` at startup. In the tests it runs on the in-memory connection, so the file `campusmarket.db` is never used.
- There is no foreign key from `Listing.SellerId` to `Member`: a listing can be seeded without a member.
- Errors come back as the `Error` record from `Domain/Common/`: `{ "code": "...", "message": "...", "type": "Validation" | "NotFound" | "Conflict" }`.
- Enums travel as text in JSON (`"Textbook"`, `"Draft"`), because the API registers `JsonStringEnumConverter`.
- Enum values (use only these, never guess): `Category` Textbook, Notes, Electronics, LabEquipment, Other; `Condition` New, Good, Worn; `ListingStatus` Draft, Active, Reserved, Sold; `ErrorType` Validation, NotFound, Conflict.
- Status codes the API returns today:

| Slice | Success | Failures |
|---|---|---|
| Create (`POST`) | 201 + body + `Location` header | 400 |
| GetById (`GET /{id}`) | 200 + body | 404 |
| GetAll (`GET /`) | 200 + body (`items`, `count`) | none |
| Update (`PUT /{id}`) | 204, empty body | 404, 400 |
| Delete (`DELETE /{id}`) | 204, empty body | 404 |

- The 409 branches of `CreateListing` and `CreateMember` cannot be reached over HTTP: the server creates the id with `Guid.NewGuid()`. They are covered by unit tests, not here.

## 2. Test project layout

A separate project, added to `CampusMarket.sln` (`dotnet sln add tests/CampusMarket.IntegrationTests`):

```
tests/CampusMarket.IntegrationTests/
  CampusMarket.IntegrationTests.csproj
  Infrastructure/
    CampusMarketApiFactory.cs      # WebApplicationFactory<Program> + SQLite connection
    IntegrationTestBase.cs         # creates the factory and client per test, seed and query helpers
  Listings/
    CreateListingTests.cs
    GetListingByIdTests.cs
    GetListingsTests.cs
    UpdateListingTests.cs
    DeleteListingTests.cs
  Members/
    CreateMemberTests.cs
    GetMemberByIdTests.cs
    GetMembersTests.cs
    UpdateMemberTests.cs
    DeleteMemberTests.cs
```

- Namespace: `CampusMarket.IntegrationTests` plus the folder (`CampusMarket.IntegrationTests.Listings`).
- `.csproj`: `net10.0`, `Nullable` and `ImplicitUsings` enabled, `IsPackable` false, `<Using Include="Xunit" />`, a `ProjectReference` to `src/CampusMarket.Api/CampusMarket.Api.csproj`.
- Packages (same versions as `CampusMarket.UnitTests` where they overlap):
  - `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `coverlet.collector` 6.0.4
  - `Microsoft.AspNetCore.Mvc.Testing` 10.0.*
  - `FluentAssertions` 7.2.0, the same as `CampusMarket.UnitTests` (never 8)
  - SQLite for EF Core (`Microsoft.EntityFrameworkCore.Sqlite` 10.0.*) comes through the API project reference. Do not add it to the test project.
- CI: `.github/workflows/build.yml` already runs `dotnet test` on the whole solution, so adding the project to the `.sln` is enough. Do not edit the workflow.

## 3. Test host

### `Program` must be visible to the test project

`WebApplicationFactory<Program>` needs the `Program` class of the API. `Program.cs` uses top-level statements, so the compiler creates `Program` for us. In .NET 10 the Web SDK also generates a `public partial class Program`, so the test project can see it without any change.
Only if the build fails with "`Program` is inaccessible due to its protection level" (CS0122), add this single line at the end of `src/CampusMarket.Api/Program.cs`, and nothing else:

```csharp
public partial class Program { }
```

### One fresh SQLite database per test

- SQLite `DataSource=:memory:` keeps the database in RAM. It lives only while its connection is open: when the connection closes, the database is gone.
- So the factory opens one `SqliteConnection` itself, keeps it open for the whole test, and gives that same connection to EF Core. It closes it when the factory is disposed.
- xUnit creates a new instance of the test class for every test. `IntegrationTestBase` creates a new factory (so a new connection and a new empty database) in `InitializeAsync` and disposes it in `DisposeAsync`. No test sees the data of another test, and the order of the tests does not matter.
- The schema is created with `Database.EnsureCreatedAsync()` right after the factory starts.

`Infrastructure/CampusMarketApiFactory.cs`

```csharp
// AddDbContext with an options lambda registers both DbContextOptions<T> and IDbContextOptionsConfiguration<T>: remove both.
public sealed class CampusMarketApiFactory : WebApplicationFactory<Program>
{
    // The in-memory database lives as long as this connection stays open.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CampusMarketApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the app's SQLite file registration, then use the open in-memory connection.
            services.RemoveAll<DbContextOptions<CampusMarketDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CampusMarketDbContext>>();
            services.AddDbContext<CampusMarketDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }
}
```

`Infrastructure/IntegrationTestBase.cs`

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    // Same JSON settings as the API: camelCase names, enums as text.
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected CampusMarketApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new CampusMarketApiFactory();
        Client = Factory.CreateClient();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    // Arrange: saves entities straight into the database, without going through another endpoint.
    protected async Task SeedAsync(params object[] entities)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    // Assert: reads the database with a new DbContext, so it sees what is really saved.
    protected async Task<T> QueryDbAsync<T>(Func<CampusMarketDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        return await query(db);
    }
}
```

## 4. What every test checks

Every test checks all three, in this order:

1. **Status code**: `response.StatusCode.Should().Be(HttpStatusCode.Created)`.
2. **Response body**:
   - success with a body: read it into the API's own response record (`CreateListingResponse`, `GetListingByIdResponse`, ...) with `ReadFromJsonAsync<T>(Json)` and check the fields;
   - failure: read it into `Error` and check `Code` and `Type` (not the message text);
   - 204: check that the body is empty: `(await response.Content.ReadAsStringAsync()).Should().BeEmpty()`. Never read JSON from a 204.
   - 201: also check `response.Headers.Location` ends with `/api/v1/listings/{id}` (or `/members/{id}`).
3. **Database state**, read with `QueryDbAsync` and `db.Set<Listing>()` / `db.Set<Member>()`, using `AsNoTracking()`:
   - create: the entity exists with the values that were sent;
   - update: the entity has the new values (and `Version` went up for a listing);
   - delete: the entity is gone;
   - failure and queries: nothing changed (same count, same values).
   - unknown id (404): seed one entity first, then check it is still there and unchanged, so the test shows the failed call touched nothing.

## 5. Test plan (20)

Arrange data with `SeedAsync` and the domain factories (`Listing.Create(...).Value`, `Member.Create(...).Value`), never by calling another endpoint.

| Class | Test | Expected |
|---|---|---|
| `CreateListingTests` | valid body | 201, body with trimmed title, `Draft`, `Version` 1, `Location`; listing saved |
| `CreateListingTests` | empty title | 400 `Listing.TitleEmpty`; no listing saved |
| `GetListingByIdTests` | listing exists | 200, body equals the seeded listing; database unchanged |
| `GetListingByIdTests` | unknown id | 404 `Listing.NotFound`; the seeded listing is unchanged |
| `GetListingsTests` | two listings seeded | 200, `count` 2, both ids in `items` |
| `GetListingsTests` | empty database | 200, `count` 0, empty `items` |
| `UpdateListingTests` | body that changes title, category, condition and price | 204, empty body; listing has all four new values and `Version` 2 |
| `UpdateListingTests` | price 0 | 400 `Money.Invalid`; listing unchanged |
| `DeleteListingTests` | listing exists | 204, empty body; listing gone |
| `DeleteListingTests` | unknown id | 404 `Listing.NotFound`; the seeded listing is unchanged |
| `CreateMemberTests` | valid body | 201, body with trimmed name and lower-case email, `Location`; member saved |
| `CreateMemberTests` | email without `@` | 400 `Email.Invalid`; no member saved |
| `GetMemberByIdTests` | member exists | 200, body equals the seeded member; database unchanged |
| `GetMemberByIdTests` | unknown id | 404 `Member.NotFound`; the seeded member is unchanged |
| `GetMembersTests` | two members seeded | 200, `count` 2, both ids in `items` |
| `GetMembersTests` | empty database | 200, `count` 0, empty `items` |
| `UpdateMemberTests` | body that changes name and email | 204, empty body; member has the new name and email |
| `UpdateMemberTests` | name of 1 character | 400 `Member.InvalidName`; member unchanged |
| `DeleteMemberTests` | member exists | 204, empty body; member gone |
| `DeleteMemberTests` | unknown id | 404 `Member.NotFound`; the seeded member is unchanged |

`GetListings` and `GetMembers` take no input and always answer 200, so they have no failure path in the API. Their second test is the empty-database case. Do not invent a failure for them.

## 6. Naming and structure

- Class: `<UseCase>Tests`, one class per slice, inheriting `IntegrationTestBase`.
- Method: `Given_<state>_When_<action>_Then_<expected>`, for example `Given_UnknownId_When_GettingListing_Then_Returns404`.
- Body: `// Arrange`, `// Act`, `// Assert` comments. Act is exactly one HTTP call.
- One behaviour per test. No `if`, no loops in a test. Use `[Theory]` with `[InlineData]` only for the same behaviour with different input.
- Assertions with FluentAssertions 7 (`Should()`), never `Assert.Equal`.
- Use fixed, readable test data (`"Manual POO"`, `"ana@student.ro"`), not random values. Each class that needs a seller uses one fixed id: `private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");`, and a fixed `CreatedAt`: `new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)`. `Guid.NewGuid()` only for an unknown id.

## 7. Never do

- Never use the EF Core InMemory provider (`UseInMemoryDatabase`): it is not a real database. Always SQLite.
- Never use a SQLite file, a shared static factory, or `IClassFixture` / collection fixtures that share a database between tests.
- Never let a test depend on another test or on the order of the tests.
- Never start a real server on a port (`UseUrls`, Kestrel); use `Factory.CreateClient()`.
- Never call handlers or repositories from a test, and never check the database through a repository: use the `DbContext`.
- Never assert on `CreatedAt` with `DateTime.Now` / `DateTimeOffset.UtcNow`, and never use `Thread.Sleep` or `Task.Delay`.
- Never use a mocking library (Moq, NSubstitute) or FluentAssertions 8.
- Never change files in `src/` to make a test pass. The only allowed change is the `public partial class Program { }` line from section 3, and only on CS0122. If a test fails because the API behaves differently from this plan, stop and report it.
- Never invent status codes or errors the API does not return (for example 409 on create, or a failure for `GetListings` / `GetMembers`).
- Never edit `.claude/skills/` (this skill included) or `PROMPTS.md`. If something in this skill is wrong, missing or out of date, stop and report it; do not fix the skill yourself.
- Never commit or push; report the results.
- Never edit `postman/`, `.github/`, or anything in `tests/CampusMarket.UnitTests`.

## 8. Old API tests

The week 2 API tests (`tests/CampusMarket.UnitTests/Api/`) were already deleted by the EF Core work. Do not recreate them and do not delete or change anything in `tests/CampusMarket.UnitTests`.

## 9. Example: `Listings/CreateListingTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.CreateListing;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Listings;

public sealed class CreateListingTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Given_ValidListing_When_CreatingListing_Then_Returns201AndSavesListing()
    {
        // Arrange
        var request = new CreateListingRequest(
            SellerId: SellerId,
            Title: "  Manual POO  ",
            Category: Category.Textbook,
            Condition: Condition.Good,
            Price: 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateListingResponse>(Json);
        body!.Title.Should().Be("Manual POO");
        body.Status.Should().Be(ListingStatus.Draft);
        body.Version.Should().Be(1);
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/listings/{body.Id}");

        var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(body.Id);
        saved.Title.Should().Be("Manual POO");
        saved.Price.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task Given_EmptyTitle_When_CreatingListing_Then_Returns400AndSavesNothing()
    {
        // Arrange
        var request = new CreateListingRequest(SellerId, "", Category.Textbook, Condition.Good, 50m);

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/listings", request, Json);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.TitleEmpty");
        error.Type.Should().Be(ErrorType.Validation);

        var count = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        count.Should().Be(0);
    }
}
```

Pattern for a 204 slice with seeded data (`DeleteListingTests`, with the same `SellerId` field and a fixed `CreatedAt`):

```csharp
// Arrange
var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
await SeedAsync(listing);

// Act
var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

// Assert
response.StatusCode.Should().Be(HttpStatusCode.NoContent);
(await response.Content.ReadAsStringAsync()).Should().BeEmpty();

var exists = await QueryDbAsync(db => db.Set<Listing>().AnyAsync(l => l.Id == listing.Id));
exists.Should().BeFalse();
```

## 10. Check after generating

- `dotnet build` finishes with 0 errors and 0 warnings.
- `dotnet test` passes for both test projects.
- `dotnet test tests/CampusMarket.IntegrationTests` passes three runs in a row (no flaky tests).
- There are at least 20 tests, and every slice of the 10 has at least two.
- Every test checks the status code, the body (or that it is empty) and the database.
- `git status` shows changes only in `tests/CampusMarket.IntegrationTests/` and `CampusMarket.sln`.

## Feature: Authentication (Sara)

Endpoints: `POST /api/v1/auth/register` and `POST /api/v1/auth/login` (`Features/Auth/AuthEndpoints.cs`).

Test host (already done by hand, do not change):
- `CampusMarketApiFactory` sets `Seed:AdminEmail` to `""` (no default Admin in tests) and `Jwt:Key` to `TestTokens.Key`.
- `TestTokens.Create(memberId, role)` and `IntegrationTestBase.AuthenticateAs(memberId, role = "Student")` exist for protected endpoints. Auth tests themselves do not need them.

Files: `tests/CampusMarket.IntegrationTests/Auth/RegisterTests.cs` and `Auth/LoginTests.cs`.

Register (5 tests):
- valid body → 201, `Location` header, the email comes back in lowercase and the name trimmed, no password in the response; the member is in the database with a non-empty `PasswordHash` different from the password.
- weak password → 400, nothing saved.
- email without `@` → 400, nothing saved.
- name too short → 400, nothing saved.
- email already used, sent with different casing (the existing member is put in the database directly) → 409, still exactly one member.

Login (6 tests). Arrange: build the member with `Member.Register` and a real `AspNetPasswordHasher`, and put it in the database directly (helper `BuildMemberWithPassword(email, password)`). Do not call `/auth/register` in login tests.
- valid credentials → 200, non-empty `token`, `expiresAt` after the fixed test time.
- unknown email → 401.
- wrong password → 401.
- invalid email format → 401.
- empty password → 401.
- rate limit: 5 login calls, then the 6th → 429 `TooManyRequests`. Put the 5 calls in a private helper `ExhaustLoginRateLimitAsync`. Every test gets a fresh factory, so the limit never leaks into other tests.

Rules:
- 401 responses for login must all look the same (same error code), whatever the reason.
- Never hard-code a token string; never read `appsettings.Development.json` from tests.
