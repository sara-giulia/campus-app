---
name: integration-testing
description: Generates xUnit integration tests for CampusMarket that send real HTTP requests through WebApplicationFactory<Program> to the API running on an in-memory SQLite database, and check the status code, the response body and the database state through the DbContext. Use when adding or changing integration tests, or when a slice has no integration test yet.
---

# Integration testing skill for CampusMarket

> **Status: v3.** Checked against the EF Core code in `Infrastructure/Persistence/` and `CampusMarketSetup`.

## 1. Project facts (do not change these)

- .NET 11 minimal API in `src/CampusMarket.Api`, root namespace `CampusMarket.Api`. Routes are under `/api/v1`.
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
- `.csproj`: `net11.0`, `Nullable` and `ImplicitUsings` enabled, `IsPackable` false, `<Using Include="Xunit" />`, a `ProjectReference` to `src/CampusMarket.Api/CampusMarket.Api.csproj`.
- Packages (same versions as `CampusMarket.UnitTests` where they overlap):
  - `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `coverlet.collector` 6.0.4
  - `Microsoft.AspNetCore.Mvc.Testing` 10.0.*
  - `FluentAssertions` 7.2.0, the same as `CampusMarket.UnitTests` (never 8)
  - SQLite for EF Core (`Microsoft.EntityFrameworkCore.Sqlite` 10.0.*) comes through the API project reference. Do not add it to the test project.
- CI: `.github/workflows/build.yml` already runs `dotnet test` on the whole solution, so adding the project to the `.sln` is enough. Do not edit the workflow.

## 3. Test host

### `Program` must be visible to the test project

`WebApplicationFactory<Program>` needs the `Program` class of the API. `Program.cs` uses top-level statements, so the compiler creates `Program` for us. Since .NET 10 the Web SDK also generates a `public partial class Program`, so the test project can see it without any change.
Only if the build fails with "`Program` is inaccessible due to its protection level" (CS0122), add this single line at the end of `src/CampusMarket.Api/Program.cs`, and nothing else:

```csharp
public partial class Program { }
```

### One fresh SQLite database per test

- SQLite `DataSource=:memory:` keeps the database in RAM. It lives only while its connection is open: when the connection closes, the database is gone.
- So the factory opens one `SqliteConnection` itself, keeps it open for the whole test, and gives that same connection to EF Core. It closes it when the factory is disposed.
- xUnit creates a new instance of the test class for every test. `IntegrationTestBase` creates a new factory (so a new connection and a new empty database) in `InitializeAsync` and disposes it in `DisposeAsync`. No test sees the data of another test, and the order of the tests does not matter.
- The schema is created by the app itself: at startup `Program.cs` calls `EnsureCampusMarketDatabase()`, which runs `Database.Migrate()` on the open connection. The `EnsureCreatedAsync()` call in `IntegrationTestBase` then finds the tables already there and does nothing; keep it.

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
- Never declare a private helper with an interface return type when it always returns one concrete type (for example `IReadOnlyList<T>` for a method that returns `.ToList()`): return the concrete type (`List<T>`). SonarCloud reports it as CA1859. After generating, build with `-p:ErrorLog=<file>.sarif%2Cversion=2.1` and check that the new test files have no CA or IDE diagnostic.
- Never pass a constant array literal as an argument (CA1861): put it in a `private static readonly` field.

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
- `dotnet test` passes for all four test projects (unit, integration, ML, architecture).
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
- - `RegisterRequest` now has `confirmPassword`. Update the existing register tests so the body sends the same value as `password`. Add: `confirmPassword` different from `password` → 400 `Auth.PasswordsDoNotMatch`, nothing saved.

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

## Feature: Listing search and paging (Diana)

This feature changes two facts above: `GET /api/v1/listings` now takes query parameters and **can return 400**, and the body has `items`, `count`, `totalCount`, `page`, `pageSize`. For `GetListings` only, the "no failure path" note in section 5 no longer applies. `GetMembers` is unchanged.

Endpoint: `GET /api/v1/listings?category=&minPrice=&maxPrice=&search=&page=&pageSize=`. Every parameter is optional; defaults are `page=1`, `pageSize=20`. Public, no `AuthenticateAs`.
Only `Active` listings are returned. Publish a listing in the domain before seeding it (`listing.Publish()`); never call the publish endpoint, which needs a login. 

File: `tests/CampusMarket.IntegrationTests/Listings/GetListingsTests.cs`. Keep the two existing tests and add the new ones to the same class.

Arrange: seed three listings with `SeedAsync`, each with its own fixed `CreatedAt` (the class `CreatedAt`, plus 1 hour, plus 2 hours) and the same `SellerId`, each one published with `listing.Publish()` before `SeedAsync`:
- `"OOP textbook"`, `Textbook`, 40
- `"oop problem set"`, `Textbook`, 15
- `"Scientific calculator"`, `Electronics`, 120

Tests to add (status, body, then the database still has exactly 3 listings):
- one `Draft` and one published listing seeded, no parameters → 200, only the published one, `totalCount` 1.
- `?search=OOP` → 200, the two OOP listings (case is ignored).
- `?category=Textbook&minPrice=20&maxPrice=100&search=oop` → 200, only `"OOP textbook"`.
- `?minPrice=40&maxPrice=40` → 200, only `"OOP textbook"` (limits are inclusive).
- no parameters → 200, the three listings newest first, `page` 1, `pageSize` 20, `totalCount` 3.
- `?page=2&pageSize=2` → 200, one item (the oldest), `count` 1, `totalCount` 3, `page` 2, `pageSize` 2.
- `?page=0` → 400, `Error` with code `Listings.InvalidPage`, type `Validation`.
- `?pageSize=101` → 400, `Listings.InvalidPageSize`, type `Validation`.
- `?minPrice=100&maxPrice=10` → 400, `Listings.InvalidPriceRange`, type `Validation`.
- `?category=Abc` → 400. This one is rejected by ASP.NET Core before the handler runs, so check only the status code, not an `Error` body.

Rules for this feature:
- Build the query string exactly as a client would send it, in the URL of `Client.GetAsync`.
- Read the success body into `GetListingsResponse` and check the order of `items` by id, not only their count.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Ownership (Anca)

This feature changes facts from section 1: POST, PUT and DELETE now need a login, and the API can return **401** and **403**. `ErrorType` also has `Forbidden`. GET endpoints stay public.

Who may call what (read the endpoints in `Features/` before generating):

| Slice | Who may call it | New failures |
|---|---|---|
| `CreateListing` | anyone logged in; `SellerId` comes from the token, it is no longer in `CreateListingRequest` | 401 |
| `UpdateListing`, `DeleteListing` | the seller or an Admin | 401; 403 `Listing.NotOwner`, type `Forbidden` |
| `CreateMember` | Admins only (`RequireAuthorization("Admin")`) | 401; 403 with an **empty body** (the authorization middleware answers, not the handler) |
| `UpdateMember`, `DeleteMember` | the member themself or an Admin | 401; 403 `Member.NotOwner`, type `Forbidden` |

401 always has an empty body. Log in with `AuthenticateAs(memberId)` or `AuthenticateAs(memberId, "Admin")` from `IntegrationTestBase`, in the Arrange, before the one HTTP call. A test without `AuthenticateAs` is a test with no token.

Update the existing tests in `Listings/CreateListingTests.cs`, `UpdateListingTests.cs`, `DeleteListingTests.cs`, `Members/CreateMemberTests.cs`, `UpdateMemberTests.cs`, `DeleteMemberTests.cs` (do not delete any):
- listings: `AuthenticateAs(SellerId)`. `CreateListingRequest` has no `SellerId` any more: check that the body and the saved listing have the `SellerId` of the token.
- `CreateMember`: `AuthenticateAs(AdminId, "Admin")` with a fixed `AdminId`.
- `UpdateMember`, `DeleteMember`: `AuthenticateAs(member.Id)`, the member changes their own profile.
- tests with an unknown id: log in as `OtherStudentId`; the API answers 404 before it checks the owner.

Tests to add (status, body, then the database):
- `CreateListingTests`: no token → 401, no listing saved.
- `UpdateListingTests`: no token → 401, listing unchanged; another student → 403 `Listing.NotOwner`, listing unchanged; an Admin who is not the seller → 204, the new values saved, `Version` 2; unknown id → 404 `Listing.NotFound`, the seeded listing unchanged.
- `DeleteListingTests`: no token → 401, listing still there; another student → 403 `Listing.NotOwner`, listing still there; an Admin → 204, listing gone.
- `CreateMemberTests`: no token → 401, no member saved; a student → 403 (check only the status), no member saved; Admin with an email already used (seed the member first, send it with different casing) → 409 `Member.EmailTaken`, still exactly one member.
- `UpdateMemberTests`: no token → 401, member unchanged; another member → 403 `Member.NotOwner`, member unchanged; an Admin → 204, the new values saved; unknown id → 404 `Member.NotFound`; a new email that another member already uses → 409 `Member.EmailTaken`, both members unchanged.
- `DeleteMemberTests`: no token → 401, member still there; another member → 403 `Member.NotOwner`, member still there; an Admin → 204, member gone.

Rules for this feature:
- Use fixed ids for the people: `SellerId` (already in the listing classes), `OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222")`, `AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333")`. Members do not need to exist in the database to log in: the token is enough.
- Read a 403 body from the handler into `Error` and check `Code` and `Type` (`Forbidden`). For 401, and for the 403 of `CreateMember`, check only the status code.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Reservations (Sara)

New endpoints (read them in `Features/` before generating). All of them need a login: without `AuthenticateAs` the answer is 401 with an empty body.

| Slice | Route | Who may call it | Success | Failures |
|---|---|---|---|---|
| `PublishListing` | `POST /api/v1/listings/{id}/publish` | the seller or an Admin | 204 | 401; 403 `Listing.NotOwner`; 404 `Listing.NotFound`; 400 `Listing.NotDraft` |
| `ReserveListing` | `POST /api/v1/listings/{id}/reservations` | anyone logged in; the buyer is the user in the token | 201 + reservation (`id`, `listingId`, `buyerId`, `createdAt`, `expiresAt`, `status`) + `Location` ending in `/api/v1/members/me/reservations` | 401; 404 `Listing.NotFound`; 404 `Member.NotFound`; 400 `Listing.NotActive`, `Reservation.OwnListing`, `Reservation.LimitReached` |
| `CancelReservation` | `DELETE /api/v1/listings/{id}/reservations/{reservationId}` | the buyer, the seller or an Admin | 204 | 401; 403 `Reservation.NotAllowed`; 404 `Listing.NotFound`, `Reservation.NotFound`; 400 `Reservation.NotActive` |
| `GetMyReservations` | `GET /api/v1/members/me/reservations` | anyone logged in; only their own reservations | 200 `{ items, count }`, each item `reservationId`, `listingId`, `listingTitle`, `price`, `status`, `createdAt`, `expiresAt` | 401 |

`ReservationStatus` values: Active, Cancelled, Expired, Completed.

Files: `Listings/PublishListingTests.cs`, `Listings/ReserveListingTests.cs`, `Listings/CancelReservationTests.cs`, `Members/GetMyReservationsTests.cs`.

Arrange:
- Seed the listing with `SeedAsync`: `Listing.Create(SellerId, ...)`, then `Publish()` for an `Active` listing, and `Reserve(buyer.Id, CreatedAt)` for a reserved one, all in the domain before `SeedAsync`.
- The buyer must exist for `ReserveListing`: seed a `Member` (`Member.Create(...).Value`) and log in as `AuthenticateAs(buyer.Id)`.
- Fixed ids as in the ownership section: `SellerId`, `OtherStudentId`, `AdminId`.

Tests to add (status, body, then the database):
- PublishListing: the seller → 204, the saved listing is `Active` with `Version` 2; another student → 403 `Listing.NotOwner`, still `Draft`; an Admin → 204; unknown id → 404; an `Active` listing → 400 `Listing.NotDraft`; no token → 401, still `Draft`.
- ReserveListing: a seeded buyer reserves an `Active` listing → 201, the body has the buyer and status `Active`, `expiresAt` is 48 h after `createdAt`, the `Location` header ends with `/api/v1/members/me/reservations`; in the database the listing is `Reserved` and has exactly one reservation for that buyer. Then: the seller reserves their own listing → 400 `Reservation.OwnListing`; a `Draft` listing → 400 `Listing.NotActive`; a logged-in member that is not in the database → 404 `Member.NotFound`; unknown listing → 404; the buyer already has 3 active reservations on 3 other seeded listings → 400 `Reservation.LimitReached`; no token → 401. Every failure: the listing has no new reservation.
- CancelReservation: the buyer cancels → 204, in the database the reservation is `Cancelled` and the listing `Active`; the seller → 204; an Admin → 204; another student → 403 `Reservation.NotAllowed`, still `Active`/`Reserved`; unknown reservation id → 404 `Reservation.NotFound`; a reservation already cancelled → 400 `Reservation.NotActive`; no token → 401.
- GetMyReservations: no token → 401; the buyer with two reservations on two seeded listings → 200, both items, newest first, with the right `listingTitle` and `price`; another student → 200 with an empty list; a reservation created 49 h ago (seed it with `Reserve(buyer.Id, DateTimeOffset.UtcNow.AddHours(-49))`) → status `Expired`, while in the database it is still `Active`.

Rules for this feature:
- Read 403/404/400 bodies into `Error` and check `Code` and `Type`. For 401 check only the status code.
- The reservation's `createdAt` comes from the real clock of the API: never compare it to a fixed time, only check `expiresAt - createdAt` is 48 h.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.


## Feature: Admin seed (Carina)

Production code (written by hand, read it before generating):
- `src/CampusMarket.Api/CampusMarketSetup.cs`, method `SeedAdmin`: when `Seed:AdminEmail` and `Seed:AdminPassword` are both set, it normalizes the email with `Email.Create` (trim + lower case, the same as Register), returns without creating anything when the email is invalid, looks for a member with the same `Email`, and creates an Admin only when none exists. `Program.cs` runs it at startup through `EnsureCampusMarketDatabase()`.
- `src/CampusMarket.Api/Domain/Members/Email.cs`.

Test host:
- `CampusMarketApiFactory` sets `Seed:AdminEmail` to `""`, so the seed never runs in the other tests. Do not change the factory or `IntegrationTestBase`.
- To turn the seed on in one test, start a second copy of the API on the same in-memory database with `Factory.WithWebHostBuilder(builder => { builder.UseSetting("Seed:AdminEmail", email); builder.UseSetting("Seed:AdminPassword", password); })`. It uses the same open connection as `Factory`, so `SeedAsync` and `QueryDbAsync` see the same data. The copy starts, and the seed runs, at its first `CreateClient()`. Dispose every copy with `await using`.
- Put this in one private helper `WithAdminSeed(string adminEmail, string adminPassword = AdminPassword)` that returns `WebApplicationFactory<Program>`. Always set both settings in the test; never rely on `appsettings.Development.json`.
- Fixed data: `private const string AdminPassword = "admin-secret12";` and the class `CreatedAt`.

File: `tests/CampusMarket.IntegrationTests/Auth/SeedAdminTests.cs` (new), class `SeedAdminTests : IntegrationTestBase`.

Tests to add (status, body, then the database). In every test the Act is the one HTTP call made with the client of the seeded copy:
- `"  Admin@Campus.Test  "` in the config → login with `admin@campus.test` and `AdminPassword` → 200, non-empty `token`; exactly one member, `Email.Value` is `admin@campus.test`, `Role` is `MemberRole.Admin`.
- the app starts twice with `"Admin@Campus.Test"` (Arrange: start a first copy with `CreateClient()` and dispose it; Act: start a second copy and log in) → 200; still exactly one member. This is the case from issue #44: before the fix, the second start failed on the unique email index.
- an Admin already in the database (seeded with `Member.Register("Admin", "admin@campus.test", new AspNetPasswordHasher().Hash(AdminPassword), CreatedAt, MemberRole.Admin)`) and `"Admin@Campus.Test"` in the config → login 200; still exactly one member, with the id of the seeded one.
- `"not-an-email"` in the config → `GET /health` → 200 with the body `Healthy`; no member saved.
- a valid email and an empty password in the config → `GET /health` → 200 with the same body; no member saved.

Code review v5, row 11 (`REVIEW.md`): in `Auth/LoginTests.cs` three 401 tests check only the status and the body. Add the database check at the end of each one, the same way `Given_UnknownEmail_When_LoggingIn_Then_Returns401` does it (`CountAsync` on `db.Set<Member>()`), and change nothing else in them:
- `Given_WrongPassword_When_LoggingIn_Then_Returns401` → 1 member.
- `Given_InvalidEmailFormat_When_LoggingIn_Then_Returns401` → 0 members (it seeds nothing).
- `Given_EmptyPassword_When_LoggingIn_Then_Returns401` → 1 member.

Rules for this feature:
- Keep every existing test and its name.
- Never read `appsettings.Development.json` from tests and never hard-code a token.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.
## Feature: Price suggestion (Sara)

Endpoint: `POST /api/v1/listings/price-suggestion`, needs a logged-in user, body `{ title, description?, category, condition }`, returns `200 { suggestedPrice }` (whole lei, at least 1). It is a query: nothing is saved.

The integration tests use the **real** model `price-regression.zip`, which the API project copies to the output folder `ml-models/`. Do not replace `IPriceSuggester` in the test factory.

File: `Listings/SuggestPriceTests.cs`, using the same base class and the same login helper as the other listing tests.

Tests to add:
- Logged in, a laptop (`"Laptop Lenovo ThinkPad T480, i5, 16GB RAM"`, `Electronics`, `New`) → 200, `suggestedPrice` is a whole number ≥ 1.
- The model separates cheap from expensive items: the laptop above (`New`) gets a higher price than course notes (`"Notite Analiza matematica anul 1"`, `Notes`, `Worn`).
- No token → 401.
- Empty title → 400 `Listing.TitleEmpty`; description of 1001 characters → 400 `Listing.DescriptionTooLong`.
- An unknown condition in the JSON (`"condition": "Used"`) → 400 (only the status code: the body is produced by ASP.NET, not by our handler).
- After any of these requests, the database still has 0 listings.

Rules for this feature:
- Never assert an exact price: the model can be retrained. Only assert ≥ 1, whole number, and the laptop-vs-notes relation.

## Feature: Confirm sale (Sara)

Endpoint: `POST /api/v1/listings/{id}/reservations/{reservationId}/confirm`, logged in, 204 on success; 404 (`Listing.NotFound`, `Reservation.NotFound`), 403 `Listing.NotOwner`, 400 (`Reservation.NotActive`, `Reservation.Expired`), 401 without a token. `PUT /api/v1/listings/{id}` on a sold listing → 400 `Listing.Sold`.

File: `Listings/ConfirmSaleTests.cs`, same base class and login helper as the other reservation tests.

Arrange: seed the seller and the buyer, a listing that is published and reserved by the buyer (`Reserve(BuyerId, time)`), with `SeedAsync`. The API uses the real clock: for a normal reservation reserve at the current UTC time; for an expired one reserve 49 hours in the past.

Tests to add (status, body, then the database):
- The seller confirms → 204; in the database the listing is `Sold`, its `Version` went up by 1 and the reservation is `Completed`.
- Afterwards the buyer's `GET /api/v1/members/me/reservations` shows the reservation with status `Completed`.
- An Admin who is not the seller → 204.
- The buyer → 403 `Listing.NotOwner`; another member → 403 `Listing.NotOwner`; no token → 401. Listing still `Reserved`.
- Unknown listing → 404 `Listing.NotFound`; unknown reservation → 404 `Reservation.NotFound`.
- Confirming twice → the second is 400 `Reservation.NotActive`.
- A reservation made 49 hours ago → 400 `Reservation.Expired`; listing still `Reserved`, reservation still `Active`.
- After the sale: `PUT` on the listing → 400 `Listing.Sold`, nothing changed; another member reserving it → 400 `Listing.NotActive`.

Rules for this feature:
- Read 400/403/404 bodies into `Error` and check `Code` and `Type`; for 401 only the status code.
- Every test checks the database at the end.

## Feature: Reservation expiry (Sara)

Production code (read it before generating):
- `Infrastructure/Background/ReservationExpiryService.cs`: a `BackgroundService` that runs `ExpireReservationsCommand` every `ReservationExpiry:Interval` in a new DI scope. It is registered only when `ReservationExpiry:Enabled` is true.
- `CampusMarketApiFactory` sets `ReservationExpiry:Enabled` to `false`, so the job never runs in the other tests. Do not change the factory or `IntegrationTestBase`.

File: `Listings/ReservationExpiryTests.cs`, class `ReservationExpiryTests : IntegrationTestBase`.

Test host: to turn the job on in one test, start a second copy of the API on the same database with `Factory.WithWebHostBuilder(builder => { builder.UseSetting("ReservationExpiry:Enabled", "true"); builder.UseSetting("ReservationExpiry:Interval", "00:00:00.200"); })`, the same way the Admin seed tests do. The copy starts at its first `CreateClient()`; dispose it with `await using`.

Arrange: seed the seller, the buyers and listings published and reserved with `SeedAsync`. The API uses the real clock: an expired reservation is reserved 49 hours in the past, a valid one 1 hour in the past.

Tests to add:
- With the job on, one expired and one valid reservation → poll the database every 100 ms, for at most 5 seconds, until the expired reservation is `Expired`; then: that listing is `Active` with `Version` + 1, and the valid reservation is still `Active` on a `Reserved` listing. Fail with a clear message when 5 seconds pass.
- After the job expired the reservation, another member reserves the listing → 201.
- With the job off (the normal factory), an expired reservation stays `Active` in the database, but the buyer's `GET /api/v1/members/me/reservations` shows it as `Expired` (computed when it is read).

Rules for this feature:
- Never use a fixed `Task.Delay` as the assertion: poll the database with a timeout.
- Every test checks the database at the end.
- The copy of the API with the job on runs a tick every 200 ms on the shared in-memory SQLite connection, so it must never be running while the test sends its Act request or reads the database for its assertions: a tick at the same moment gives "database is locked" and a 500. Start it with `await using (var withJob = WithExpiryJob()) { withJob.CreateClient(); ... }`, poll inside that block until the reservation is `Expired`, and leave the block (the job stops) before the Act and the assertions. In "another member reserves", the Act is sent with the normal `Client` after `AuthenticateAs(...)`, where the job is off.
- The polling reads also run while the job ticks on the same connection, and one of them can fail now and then (the read collides with the job's write). In the polling helper, a read that throws a `SqliteException` or an `InvalidOperationException` (which also covers `ObjectDisposedException`) counts as "not yet": wait 100 ms and read again until the timeout. Catch only those two (`catch (Exception ex) when (ex is SqliteException or InvalidOperationException)`), never every exception: Sonar flags a general catch. After the timeout, read once more without catching anything, so an error that never goes away still fails the test with its own message.



## Feature: Error handling (Anca)

This feature changes facts from section 4 and from the feature sections above. Answers that ASP.NET writes by itself, which had an empty body until now, are now **ProblemDetails** (`Content-Type: application/problem+json`). Read `Infrastructure/ErrorHandling/` and `Program.cs` before generating.

| Where the error comes from | Body |
|---|---|
| a handler (`Result` failure: 400, 403 `*.NotOwner`, 404, 409 `Member.EmailTaken`) | **unchanged**: `{ code, message, type }`, read into `Error` |
| no token (401), the `Admin` policy (the 403 of `CreateMember`), an unknown route (404), a known route with the wrong HTTP method (405), JSON that cannot be read, such as an unknown enum value (400), the login rate limit (429) | ProblemDetails with `status`, `title`, `instance` (the request path), `traceId` and `code`: `Auth.Unauthorized`, `Auth.Forbidden`, `Route.NotFound`, `Request.MethodNotAllowed`, `Request.Invalid`, `Request.TooManyRequests` |
| `DbUpdateConcurrencyException` | 409 ProblemDetails, `code` `Concurrency.Conflict` |
| any other exception | 500 ProblemDetails, `code` `Server.Error`, `detail` `"An unexpected error occurred."`, never the exception message |

The rules "401 always has an empty body" and "for 401 (and the 403 of `CreateMember`, and the 400 of an unknown enum) check only the status code" in the sections above no longer hold: this section replaces them.

Helpers to add in `Infrastructure/` of the test project:
- `ProblemResponse.cs`: `public sealed record ProblemResponse(int Status, string Title, string Instance, string Code, string TraceId);`, read with `ReadFromJsonAsync<ProblemResponse>(Json)`.
- `ConcurrentEditUnitOfWork.cs`: implements `IUnitOfWork`, gets `CampusMarketDbContext` in its constructor. In `CommitAsync` it first runs `await db.Database.ExecuteSqlRawAsync("UPDATE Listings SET Version = Version + 1", ct)` (another user changed the row a moment earlier), then returns `await db.SaveChangesAsync(ct)`, so EF Core's own `Version` check throws a real `DbUpdateConcurrencyException`.
- `ThrowingUnitOfWork.cs`: implements `IUnitOfWork`; `CommitAsync` throws `new InvalidOperationException("secret: Data Source=prod.db")`.
- To use one of them in a test, create the client in the Arrange: `var client = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IUnitOfWork, ConcurrentEditUnitOfWork>())).CreateClient();`, log it in with the same `Authorization: Bearer` header that `AuthenticateAs` sets (`TestTokens.Create(...)`), and send the one HTTP call with it. `SeedAsync` and `QueryDbAsync` keep working: both clients use the same in-memory database.

Every ProblemDetails test checks: the status code; `response.Content.Headers.ContentType!.MediaType` is `application/problem+json`; `Status` and `Code`; `Instance` is the request path; `TraceId` is not empty; then the database.

Update the existing tests (do not delete or rename any; keep their database checks):
- every no-token test (`Given_NoToken_...` in `CreateListingTests`, `UpdateListingTests`, `DeleteListingTests`, `PublishListingTests`, `ReserveListingTests`, `CancelReservationTests`, `ConfirmSaleTests`, `CreateMemberTests`, `UpdateMemberTests`, `DeleteMemberTests`, `GetMyReservationsTests`, `SuggestPriceTests`): instead of the empty body, check the ProblemDetails with `code` `Auth.Unauthorized`.
- `CreateMemberTests`, a student → 403: ProblemDetails with `code` `Auth.Forbidden`.
- `LoginTests`, the 429 test: ProblemDetails with `code` `Request.TooManyRequests`.
- `SuggestPriceTests`, the unknown condition `"Used"`: ProblemDetails with `code` `Request.Invalid`.

Tests to add, in a new file `ErrorHandling/ErrorHandlingTests.cs` (fixed `SellerId` and `OtherStudentId` as in the other classes):
- `GET /api/v1/does-not-exist` → 404 ProblemDetails `Route.NotFound`, `Instance` `/api/v1/does-not-exist`.
- `DELETE /api/v1/listings` (the route exists only for GET and POST) → 405 ProblemDetails `Request.MethodNotAllowed`.
- Logged in as `SellerId`, `POST /api/v1/listings` with `"category": "Banana"` → 400 ProblemDetails `Request.Invalid`; no listing saved.
- A seeded draft of `SellerId`, client with `ConcurrentEditUnitOfWork`, the seller publishes it → 409 ProblemDetails `Concurrency.Conflict`; in the database the listing is still `Draft` and its `Version` is 2 (only the other user's change was saved).
- A seeded draft, client with `ThrowingUnitOfWork`, the seller publishes it → 500 ProblemDetails `Server.Error`, `detail` `"An unexpected error occurred."`, and the raw body does not contain `"secret"`; the listing is still `Draft` with `Version` 1.
- Handler errors keep their format: `OtherStudentId` publishes the seller's draft → 403, `Content-Type` media type is `application/json` (not `problem+json`), body read into `Error` with `Code` `Listing.NotOwner` and `Type` `Forbidden`; listing unchanged.

Rules for this feature:
- Never assert the `title` or `type` text of a ProblemDetails: ASP.NET writes them.
- The fakes live only in `tests/CampusMarket.IntegrationTests/Infrastructure/`; never replace `IUnitOfWork` in `CampusMarketApiFactory` for all tests.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Health check (Sara)

Endpoint: `GET /health`, outside `/api/v1`, no login. `AddHealthChecks().AddDbContextCheck<CampusMarketDbContext>()` in `CampusMarketSetup` and `app.MapHealthChecks("/health")` in `Program.cs`: it answers 200 with the plain text body `Healthy` when the API can reach the database (503 `Unhealthy` when it cannot). It is used by Docker; it reads nothing and writes nothing.

File: `Health/HealthCheckTests.cs`, class `HealthCheckTests : IntegrationTestBase`.

Tests to add:
- No token → 200, `Content-Type` media type `text/plain`, body exactly `Healthy`; the database is unchanged (0 listings, 0 members).
- With a student token (`AuthenticateAs(...)`) → still 200 `Healthy`: the endpoint does not depend on the user.
- `GET /api/v1/health` → 404 ProblemDetails `Route.NotFound`: the health check is not part of the versioned API.

Rules for this feature:
- Read the body with `ReadAsStringAsync()`, never as JSON: the health check writes plain text.
- Do not try to make the database unreachable; the 503 case is not tested.

## Feature: Favorites (Anca)

Endpoints, all under `/api/v1/members/me/favorites` and all need a login (the member is the user in the token):

| Request | Success | Failures |
|---|---|---|
| `POST /api/v1/members/me/favorites/{listingId}` | 201, body `{ id, listingId, createdAt }`, `Location` ends with `/api/v1/members/me/favorites` | 404 `Listing.NotFound`; 404 `Member.NotFound` (the member in the token is not in the database); 400 `Favorite.OwnListing`; 400 `Favorite.ListingNotAvailable` (a draft); 409 `Favorite.AlreadyExists`; 401 |
| `DELETE /api/v1/members/me/favorites/{listingId}` | 204, empty body | 404 `Favorite.NotFound` (also when only another member saved that listing); 401 |
| `GET /api/v1/members/me/favorites?page=&pageSize=` | 200 `{ items, count, totalCount, page, pageSize }`; each item `{ listingId, title, category, condition, price, status, favoritedAt }`, newest first | 400 `Favorites.InvalidPage`, `Favorites.InvalidPageSize`; 401 |

Read `Features/Favorites/` and `Infrastructure/Persistence/Configurations/FavoriteConfiguration.cs` before generating. The `Favorites` table has foreign keys to `Members` and `Listings` with cascade delete, so the member who saves a listing **must be seeded** (unlike the other features, a token alone is not enough), and deleting a listing deletes its favorites.

Files: `Favorites/AddFavoriteTests.cs`, `Favorites/RemoveFavoriteTests.cs`, `Favorites/GetMyFavoritesTests.cs`, same base class and login helper as the other classes. Read the response bodies into `AddFavoriteResponse` and `GetMyFavoritesResponse` from `Features/Favorites/`.

Arrange: seed the member with `Member.Create(...).Value` and log in with `AuthenticateAs(member.Id)`; a listing of the fixed `SellerId` is `Listing.Create(...).Value` then `Publish()`; a favorite is `Favorite.Create(member.Id, listing, createdAt).Value` with a fixed `createdAt` (for example `CreatedAt.AddHours(1)`), seeded together with the member and the listing in one `SeedAsync`.

Tests to add (status, body, then the database through `db.Set<Favorite>()`):
- `AddFavoriteTests`: a published listing → 201 with the listing id and `Location`, one favorite saved with this member and listing; the same listing twice (seed the favorite first) → 409 `Favorite.AlreadyExists`, still one favorite; a draft → 400 `Favorite.ListingNotAvailable`, no favorite; the seller (seed the seller as a member and create the listing with `seller.Id`) → 400 `Favorite.OwnListing`, no favorite; unknown listing → 404 `Listing.NotFound`; logged in as a member that is not in the database → 404 `Member.NotFound`, no favorite; no token → 401 ProblemDetails `Auth.Unauthorized`, no favorite.
- `RemoveFavoriteTests`: a saved favorite → 204, empty body, the favorite is gone and the listing is still there; a listing the member never saved → 404 `Favorite.NotFound`; another member removes it (seed a second member, log in as them) → 404 `Favorite.NotFound`, the first member's favorite is still there; no token → 401 ProblemDetails `Auth.Unauthorized`, the favorite is still there.
- `GetMyFavoritesTests`: no favorites → 200, `count` 0, `totalCount` 0, empty `items`; two favorites → newest first, every field of the items checked against the seeded listings and `favoritedAt` equal to the favorite's `CreatedAt`; another member's favorite is not returned; three favorites with `?page=2&pageSize=2` → one item (the oldest), `count` 1, `totalCount` 3, `page` 2, `pageSize` 2; `?page=0` → 400 `Favorites.InvalidPage`; `?pageSize=101` → 400 `Favorites.InvalidPageSize`; no token → 401 ProblemDetails `Auth.Unauthorized`. The database is unchanged after every call.
- Cascade, in `RemoveFavoriteTests`: the seller deletes the listing (`DELETE /api/v1/listings/{id}` logged in as `SellerId`) → 204, and the member's favorite for it is gone from the database.

Rules for this feature:
- Read 400/404/409 bodies into `Error` and check `Code` and `Type`; 401 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Refresh token and logout (Carina)

Endpoints (`Features/Auth/`): `POST /api/v1/auth/refresh` and `POST /api/v1/auth/logout`, both with the body `{ "refreshToken" }`. They are public: never use `AuthenticateAs` in these tests, because the refresh token itself is the proof. Login now also returns `refreshToken`.

| Slice | Success | Failures |
|---|---|---|
| Login | 200 `{ token, expiresAt, refreshToken }` | 401, 429 (unchanged) |
| Refresh | 200 `{ token, expiresAt, refreshToken }` with a new refresh token | 401 `Auth.InvalidRefreshToken`, type `Validation` |
| Logout | 204, empty body | 401 `Auth.InvalidRefreshToken`, type `Validation` |

The database stores only the SHA-256 hash, in the table `RefreshTokens` (entity `RefreshToken` with `MemberId`, `TokenHash`, `CreatedAt`, `ExpiresAt`, `RevokedAt`). `RefreshToken.Hash(plainToken)` gives the stored value.

Arrange:
- Seed the member with `BuildMemberWithPassword` (as in `LoginTests`) and the token with `RefreshToken.Create(member.Id, "plain-refresh-token", ...)` through `SeedAsync`. Never call `/auth/login` to get a refresh token in the refresh or logout tests.
- The API uses the real clock, so build the token times from `DateTimeOffset.UtcNow`: active = created now; expired = created 8 days ago; revoked = created now and `Revoke(DateTimeOffset.UtcNow)` before `SeedAsync`.
- The access token lasts 15 minutes. Read `var before = DateTimeOffset.UtcNow;` in the Arrange and, after the call, check that `expiresAt` is between `before.AddMinutes(15)` and `DateTimeOffset.UtcNow.AddMinutes(15)`. This window is the only allowed use of the real clock in an assertion.

Files and tests (status, body, then the database):
- `Auth/LoginTests.cs`: in `Given_ValidCredentials_When_LoggingIn_Then_Returns200WithToken`, also check a non-empty `refreshToken`, `expiresAt` inside the 15-minute window, and in the database exactly one `RefreshToken` for the member with `TokenHash` = `RefreshToken.Hash(body.RefreshToken)` (so the plain token is not stored) and `RevokedAt` null. Change nothing else in the existing tests.
- `Auth/RefreshTests.cs` (new):
    - active token → 200, non-empty `token`, a `refreshToken` different from the one sent, `expiresAt` inside the 15-minute window; in the database the old token has `RevokedAt` set and the member has exactly 2 tokens, the new one with `TokenHash` = `RefreshToken.Hash(body.RefreshToken)` and `RevokedAt` null.
    - revoked (already used) token → 401 `Auth.InvalidRefreshToken`; still exactly 1 token, unchanged.
    - expired token → 401; still exactly 1 token, `RevokedAt` still null.
    - unknown token (only the member is seeded) → 401; 0 tokens.
    - empty string → 401; 0 tokens.
    - a token whose member does not exist (seed only the token) → 401; the token is not revoked.
- `Auth/LogoutTests.cs` (new):
    - active token → 204, empty body; in the database `RevokedAt` is set.
    - token already revoked → 401 `Auth.InvalidRefreshToken`; the token is unchanged.
    - unknown token → 401; 0 tokens.
    - empty string → 401.

Rules for this feature:
- Read every 401 body into `Error` and check `Code` and `Type`.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Category suggestion (Anca)

Endpoint: `POST /api/v1/listings/category-suggestion`, needs a logged-in user, body `{ title, description? }`, returns `200 { category, confidence }` (`category` is one of the `Category` names as a string, `confidence` is between 0 and 1). It is a query: nothing is saved.

The integration tests use the **real** model `category-classification.zip`, which the API project copies to the output folder `ml-models/`. Do not replace `ICategorySuggester` in the test factory. Read the body into `SuggestCategoryResponse` from `Features/Listings/SuggestCategory/`.

File: `Listings/SuggestCategoryTests.cs`, using the same base class and the same login helper as the other listing tests (`SuggestPriceTests` is the closest example).

Tests to add:
- Logged in, a laptop (`"Laptop Lenovo ThinkPad T480, i5, 16GB RAM"`, description `"Bateria tine 4 ore."`) → 200, `category` is `Electronics`, `confidence` between 0 and 1.
- Course notes (`"Notite Analiza matematica anul 1"`, no description) → 200, `category` is `Notes`.
- No token → 401 ProblemDetails `Auth.Unauthorized`.
- Empty title (`"   "`) → 400 `Listing.TitleEmpty`; description of 1001 characters → 400 `Listing.DescriptionTooLong`.
- After any of these requests, the database still has 0 listings.

Rules for this feature:
- Never assert an exact confidence: the model can be retrained. Only assert the category for the two clear examples and that the confidence is between 0 and 1.
- Read 400 bodies into `Error` and check `Code` and `Type`; 401 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Spam and moderation (Anca)

Endpoints:

| Request | Who | Success | Failures |
|---|---|---|---|
| `POST /api/v1/listings/{listingId}/reports`, body `{ reason }` | any logged-in member (the reporter is the user in the token) | 201, body `{ id, listingId, createdAt }`, no `Location` | 404 `Listing.NotFound`; 404 `Member.NotFound`; 400 `Report.OwnListing`, `Report.ListingNotAvailable`, `Report.ReasonEmpty`, `Report.ReasonTooLong`; 409 `Report.AlreadyExists`; 401 |
| `GET /api/v1/moderation/listings?page=&pageSize=` | Admins only | 200 `{ items, count, totalCount, page, pageSize }`; each item `{ listingId, title, sellerId, status, reportCount, flaggedAsSpam, spamProbability, lastReportedAt, reasons }` | 400 `Moderation.InvalidPage`, `Moderation.InvalidPageSize`; 401; 403 |
| `POST /api/v1/moderation/listings/{listingId}/hide` | Admins only | 204, empty body | 404 `Listing.NotFound`; 400 `Listing.AlreadyHidden`, `Listing.CannotHide`; 401; 403 |

Also: `POST /api/v1/listings/{id}/publish` now runs the **real** spam model (`spam-detection.zip`, copied to `ml-models/`). A listing that looks like spam is still published (204) but gets an automatic report (`ReporterId` null, `SpamProbability` set). Do not replace `ISpamDetector` in the test factory. A hidden listing cannot be added to favorites (400 `Favorite.ListingNotAvailable`) and is not in `GET /api/v1/members/me/favorites`.

Read `Features/Moderation/`, `Domain/Reports/` and `Infrastructure/Persistence/Configurations/ReportConfiguration.cs` before generating. The `Reports` table has foreign keys to `Listings` and `Members` with cascade delete, so the member who reports **must be seeded** (like favorites). An Admin is logged in with `AuthenticateAs(id, "Admin")` from `IntegrationTestBase`; the admin does not need to be in the database.

Files: `Moderation/ReportListingTests.cs`, `Moderation/GetReportedListingsTests.cs`, `Moderation/HideListingTests.cs`, `Moderation/SpamOnPublishTests.cs` (new), and one test added to `Favorites/AddFavoriteTests.cs` and one to `Favorites/GetMyFavoritesTests.cs`. Read the bodies into the response records from `Features/Moderation/`.

Arrange: a published listing of the fixed `SellerId` is `Listing.Create(...).Value` then `Publish()`; a hidden one also `Hide()`. A user report is `Report.Create(member.Id, listing, "reason", createdAt).Value`, an automatic one `Report.CreateAutomatic(listing.Id, 0.9, createdAt)`, with a fixed `createdAt`, seeded together with the member and the listing in one `SeedAsync`.

Tests to add (status, body, then the database through `db.Set<Report>()` and `db.Set<Listing>()`):
- `ReportListingTests`: a published listing → 201 with the listing id, one report saved with this reporter and the trimmed reason; the same member again (seed the report first) → 409 `Report.AlreadyExists`, still one report; the seller → 400 `Report.OwnListing`; a draft → 400 `Report.ListingNotAvailable`; reason `"   "` → 400 `Report.ReasonEmpty`; unknown listing → 404 `Listing.NotFound`; logged in as a member that is not in the database → 404 `Member.NotFound`; no token → 401 ProblemDetails `Auth.Unauthorized`. No report is saved in any failure.
- `GetReportedListingsTests`: as Admin, a listing with two user reports and another with one automatic report → 200, the first has `reportCount` 2 and `flaggedAsSpam` false, the second `flaggedAsSpam` true and `spamProbability` 0.9; a hidden listing with reports is not in the list; `?page=0` → 400 `Moderation.InvalidPage`; `?pageSize=101` → 400 `Moderation.InvalidPageSize`; as a student → 403 ProblemDetails `Auth.Forbidden`; no token → 401 ProblemDetails `Auth.Unauthorized`.
- `HideListingTests`: as Admin, a published listing → 204, the listing in the database is `Hidden`; a reserved listing → 204, `Hidden`, and its reservation is `Cancelled`; a hidden listing → 400 `Listing.AlreadyHidden`; a draft → 400 `Listing.CannotHide`; unknown listing → 404 `Listing.NotFound`; as a student (even the seller) → 403 ProblemDetails `Auth.Forbidden`, the listing is unchanged; no token → 401. After hiding, `GET /api/v1/listings` does not return it.
- `SpamOnPublishTests` (real model): the seller publishes a spam draft (title `"BANI RAPIZI, SUNA ACUM!!!"`, description `"Metoda sigura de castig online!!! Suna la 0739 465 387."`, `Other`, price 1) → 204, the listing is `Active` and one report exists for it with `ReporterId` null and `SpamProbability` between 0.5 and 1; a normal draft (`"Manual Algebra liniara, editia 2"`, `"Putin folosit, fara sublinieri."`, `Textbook`, price 40) → 204 and no report.
- Favorites: adding a hidden listing → 400 `Favorite.ListingNotAvailable`, no favorite; a saved favorite whose listing is hidden (seed it hidden) is not in `GET /api/v1/members/me/favorites` (`totalCount` 0).

Rules for this feature:
- Never assert an exact spam probability from the real model: only that it is between 0.5 and 1 for the spam example.
- Read 400/404/409 bodies into `Error` and check `Code` and `Type`; 401 and 403 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Seller reviews (Anca)

Endpoints:

| Request | Who | Success | Failures |
|---|---|---|---|
| `POST /api/v1/listings/{listingId}/reviews`, body `{ rating, comment? }` | the buyer of the sold listing (the reviewer is the user in the token) | 201, body `{ id, listingId, sellerId, rating, comment, createdAt }`, `Location` ends with `/api/v1/members/{sellerId}/reviews` | 404 `Listing.NotFound`; 404 `Member.NotFound`; 400 `Review.SaleNotCompleted`, `Review.InvalidRating`, `Review.CommentTooLong`; 403 `Review.NotBuyer` (any other member, also the seller; body is our `Error`, not ProblemDetails); 409 `Review.AlreadyExists`; 401 |
| `GET /api/v1/members/{id}/reviews?page=&pageSize=` | everyone (no token needed) | 200 `{ memberId, averageRating, reviewCount, items, count, page, pageSize }`; each item `{ id, listingId, reviewerId, rating, comment, createdAt }`, newest first; `averageRating` is null when there are no reviews | 400 `Reviews.InvalidPage`, `Reviews.InvalidPageSize`; 404 `Member.NotFound` |

Read `Features/Reviews/`, `Domain/Reviews/` and `Infrastructure/Persistence/Configurations/ReviewConfiguration.cs` before generating. The `Reviews` table has foreign keys to `Listings` and to `Members` twice (seller and reviewer), all with cascade delete, and a unique index on `ListingId`. So the seller **and** the buyer must be seeded as members.

Files: `Reviews/AddReviewTests.cs`, `Reviews/GetMemberReviewsTests.cs`, same base class and login helper as the other classes. Read the bodies into `AddReviewResponse` and `GetMemberReviewsResponse` from `Features/Reviews/`.

Arrange: seed the seller and the buyer with `Member.Create(...).Value`; a sold listing is `Listing.Create(seller.Id, ...).Value`, `Publish()`, `var reservation = listing.Reserve(buyer.Id, CreatedAt).Value`, `listing.ConfirmSale(reservation.Id, CreatedAt)`. Log in as the buyer with `AuthenticateAs(buyer.Id)`. A review is `Review.Create(buyer.Id, listing, rating, comment, createdAt).Value` with a fixed `createdAt` (for example `CreatedAt.AddHours(1)`), seeded together with the members and the listing in one `SeedAsync`.

Tests to add (status, body, then the database through `db.Set<Review>()`):
- `AddReviewTests`: the buyer of a sold listing, rating 5 and a comment → 201 with the listing id, the seller id, 5, the trimmed comment, and `Location`; one review saved with this listing, seller, reviewer and rating; without a comment → 201 and `comment` null; the same listing again (seed the review first) → 409 `Review.AlreadyExists`, still one review; a listing that is only reserved → 400 `Review.SaleNotCompleted`; rating 0 → 400 `Review.InvalidRating`; another member (seed them) → 403 `Review.NotBuyer` with `Type` `Forbidden`; the seller → 403 `Review.NotBuyer`; unknown listing → 404 `Listing.NotFound`; logged in as a member that is not in the database → 404 `Member.NotFound`; no token → 401 ProblemDetails `Auth.Unauthorized`. No review saved in any failure.
- `GetMemberReviewsTests`: no token, a seller with ratings 5 and 4 → 200, `averageRating` 4.5, `reviewCount` 2, newest first, every field of the items checked; a seller with no reviews → 200, `averageRating` null, `reviewCount` 0, empty `items`; reviews of another seller are not returned; three reviews with `?page=2&pageSize=2` → one item, `count` 1, `reviewCount` 3; `?page=0` → 400 `Reviews.InvalidPage`; `?pageSize=101` → 400 `Reviews.InvalidPageSize`; an unknown member → 404 `Member.NotFound`. The database is unchanged after every call.
- Cascade, in `AddReviewTests`: the seller deletes the sold listing (`DELETE /api/v1/listings/{id}` logged in as the seller) → 204, and its review is gone from the database.

Rules for this feature:
- Read 400/403/404/409 bodies of our handlers into `Error` and check `Code` and `Type`; 401 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Whole project coverage (Anca)

SonarCloud measures the coverage of the whole API (only `Program.cs`, `ml/**` and the migrations are left out), not only of `Domain/` and the handlers. After every test project runs, these lines are still not covered. Add the tests below and nothing else; keep every existing test.

Production code (read it before generating):
- `Infrastructure/Persistence/Ef*Repository.cs` (`EfListingRepository`, `EfMemberRepository`, `EfFavoriteRepository`, `EfReportRepository`, `EfReviewRepository`): `AddAsync` returns `false` without adding when an entity with the same `Id` is already in the database, otherwise it adds it (saved at the next `SaveChangesAsync`) and returns `true`. The domain always creates a new `Guid`, so no HTTP request can reach the `false` branch.
- `Features/Listings/CreateListing/CreateListingEndpoint.cs`: maps `ErrorType.Conflict` (`Listing.Duplicate`, when `IListingRepository.AddAsync` returns `false`) to 409, everything else to 400.
- `CampusMarketSetup.cs`:
    - the `login` rate limit policy: 5 requests per minute per partition; the partition is `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`. The test server leaves `RemoteIpAddress` null, so every existing test uses the `"unknown"` partition.
    - `SeedAdmin`: after the email check it calls `Member.Register("Admin", email, hasher.Hash(password), ...)` and returns without saving when it fails (for example an empty hash: `Member.PasswordRequired`).
- `Infrastructure/Background/ReservationExpiryService.cs`: every `ReservationExpiry:Interval` it resolves `ICommandHandler<ExpireReservationsCommand, int>` in a new scope and runs it. A failed `Result` is logged with `LogError` ("ReservationExpiryService run failed: {Code} {Message}") and the service keeps running; an exception is logged with `LogError(ex, "ReservationExpiryService run failed.")` and the service keeps running; an `OperationCanceledException` while the host is stopping ends the loop without an error log.

Test host:
- Never change `CampusMarketApiFactory` or `IntegrationTestBase`. Every special setup goes through `Factory.WithWebHostBuilder(...)` in the test class (a copy of the API on the same in-memory database, started at its first `CreateClient()`, disposed with `await using`), the same way `SeedAdminTests` and `ReservationExpiryTests` do.
- Replace a service with `builder.ConfigureTestServices(services => { services.RemoveAll<T>(); services.AddScoped<T, Fake>(); })`. New fakes go in `Infrastructure/` of the test project, next to `ThrowingUnitOfWork`, one class per file, with a one-line comment that says what they do.
- To give a request an IP address, register an `IStartupFilter` (`Infrastructure/TestRemoteIpStartupFilter.cs`) that adds a first middleware: when the request has the header `X-Test-Remote-Ip`, it sets `context.Connection.RemoteIpAddress = IPAddress.Parse(value)`.
- To read the logs of the background service, register a hand-written `ILoggerProvider` (`Infrastructure/CapturingLoggerProvider.cs`) with `builder.ConfigureLogging(logging => logging.AddProvider(provider))`. It keeps every entry (category, `LogLevel`, formatted message, exception) in a thread-safe list that the test can read.
- Background service tests: `UseSetting("ReservationExpiry:Enabled", "true")` and `UseSetting("ReservationExpiry:Interval", "00:00:00.100")`, and replace `ICommandHandler<ExpireReservationsCommand, int>` with a fake (`Infrastructure/FakeExpireReservationsHandler.cs`) whose behaviour is set by the test: return a failure (`Error.Validation("Test.Failure", "Run failed on purpose.")`), throw `InvalidOperationException("Run failed on purpose.")`, or wait with `Task.Delay(Timeout.Infinite, ct)`. The fake counts its calls (thread-safe) and has a `TaskCompletionSource` that completes at the first call. Register it as a singleton instance so the test reads the same object.

Files and tests to add:
- `Infrastructure/RepositoryTests.cs`, class `RepositoryTests : IntegrationTestBase`. For each of the five repositories: seed one entity with `SeedAsync`, then in a new scope create the repository over the scoped `CampusMarketDbContext` and call `AddAsync` with the same entity instance loaded from that context (or a new instance with the same `Id`) → `false`; then `SaveChangesAsync` and the table still has exactly one row with that `Id`. Also one test per repository where a new entity → `true` and after `SaveChangesAsync` it is in the database. Build the entities the same way the other test classes do (members, a published listing, `Favorite.Create`, `Report.Create`, `Review.Create` on a sold listing). Act is the `AddAsync` call; there is no HTTP request in this class.
- `Listings/CreateListingTests.cs` (add one test): `IListingRepository` replaced by `Infrastructure/DuplicateIdListingRepository.cs` (delegates every method to `new EfListingRepository(db)`, except `AddAsync`, which returns `false`) → `POST /api/v1/listings` with a valid body → 409, body read into `Error` with `Code` `Listing.Duplicate` and `Type` `Conflict`; no listing in the database.
- `Auth/LoginTests.cs` (add one test): with `TestRemoteIpStartupFilter`, five login attempts with `X-Test-Remote-Ip: 10.0.0.1` (the helper may loop, like `ExhaustLoginRateLimitAsync`), then the Act: one login with `X-Test-Remote-Ip: 10.0.0.2` and the right password → 200 with a token (another IP has its own limit). A second test: the sixth attempt from `10.0.0.1` → 429 ProblemDetails `Request.TooManyRequests`. Both check the member count at the end.
- `Auth/SeedAdminTests.cs` (add one test): `WithAdminSeed("admin@campus.test")` plus `IPasswordHasher` replaced by `Infrastructure/EmptyHashPasswordHasher.cs` (`Hash` returns `""`, `Verify` returns `false`) → `GET /health` → 200 with the body `Healthy`; no member saved.
- `Infrastructure/ReservationExpiryServiceTests.cs`, class `ReservationExpiryServiceTests : IntegrationTestBase`:
    - the handler returns a failure → poll (every 50 ms, at most 5 s) until the fake was called at least twice; there is an `Error` log entry of category `CampusMarket.Api.Infrastructure.Background.ReservationExpiryService` whose message contains `Test.Failure` and `Run failed on purpose.`.
    - the handler throws → poll until it was called at least twice; there is an `Error` entry with the `InvalidOperationException` and the message `ReservationExpiryService run failed.`.
    - the handler waits until it is cancelled → wait for its first call (at most 5 s), then dispose the copy of the API → the dispose finishes without an exception, the fake was called exactly once, and there is no `Error` entry from the service.
    - every test also checks that the database is unchanged (no listings, no reservations), because the fake handler never touches it.

Rules for this feature:
- Never use a fixed `Task.Delay` as the assertion: poll with a timeout, as in "Feature: Reservation expiry (Sara)", and fail with a clear message when the time passes.
- This section is the only exception to two general rules: `RepositoryTests` calls the repositories directly (section 7 forbids it elsewhere), and `RepositoryTests` and `ReservationExpiryServiceTests` have no HTTP call (section 6). There the Act is the `AddAsync` call, starting the copy of the API and polling, or disposing the copy.
- Register a replacement with the same lifetime as the real service in `CampusMarketSetup.cs` (for example `IPasswordHasher` is a singleton).
- This section goes with two changes in `src/` made by hand before the run (`CampusMarketSetup.cs` reads `ReservationExpiry:Enabled` with `GetValue`, and `ReservationExpiryService.cs` no longer checks the result of `WaitForNextTickAsync`), so the section 10 check on `git status` also lists those two files.
- Never change `src/`. If a line is still not covered after these tests, stop and report which one.
- Run `dotnet test tests/CampusMarket.IntegrationTests` three times in a row: the new tests must never fail.

## Feature: Condition filter (Carina)

Endpoint: `GET /api/v1/listings?condition=...` (`New`, `Good` or `Worn`), public, no `AuthenticateAs`. Only `Active` listings are returned, as before.

File: `Listings/GetListingsTests.cs` (add to it, keep every existing test).

Arrange: seed published listings (`listing.Publish()` before `SeedAsync`) with the same `SellerId`, one in `Good` and one in `Worn` condition, each with its own fixed `CreatedAt`.

Tests to add (status, body, then the database still has the seeded listings):
- `?condition=Good` → 200, only the Good listing, `totalCount` 1.
- `?condition=Banana` → 400. ASP.NET Core rejects it before the handler runs, so check only the status code.

Rules for this feature:
- Build the query string exactly as a client would send it, in the URL of `Client.GetAsync`.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: API documentation (Carina)

Production code: `services.AddOpenApi()` in `CampusMarketSetup.AddCampusMarket`; in `Program.cs`, only when `app.Environment.IsDevelopment()`: `app.MapOpenApi()` (the document at `/openapi/v1.json`) and `app.MapScalarApiReference()` (the page at `/scalar`). The test host runs in the Development environment, so both are mapped in the tests.

File: `Infrastructure/OpenApiTests.cs` (new), class `OpenApiTests : IntegrationTestBase`.

Tests to add:
- `GET /openapi/v1.json` → 200, a JSON content type, and the body contains `/api/v1/listings` and `/api/v1/auth/login`. Read the body with `ReadAsStringAsync`; do not deserialize the OpenAPI document.
- The database has nothing to do with this endpoint: at the end, check that it is still empty (0 listings), as the 404 and 405 tests of "Feature: Error handling (Anca)" do.

Rules for this feature:
- Do not change `Program.cs`, `CampusMarketSetup.cs` or the test factory.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: My profile (Carina)

Endpoints (`Features/Members/GetMyProfile/`, `Features/Members/UpdateMyProfile/`), both need a token ("me" is the member in the token):

| Slice | Route | Success | Failures |
|---|---|---|---|
| GetMyProfile | `GET /api/v1/members/me` | 200 `{ id, name, email, role, createdAt }` | 401; 404 `Member.NotFound` (the member in the token does not exist) |
| UpdateMyProfile | `PUT /api/v1/members/me` with `{ "name" }` | 204, empty body | 401; 400 `Member.InvalidName`; 404 `Member.NotFound` |

The 401 is a ProblemDetails answer: check it as "Feature: Error handling (Anca)" says. The 400 and 404 come from the handlers and keep `{ code, message, type }`: read them into `Error` and check `Code` and `Type`.

File: `Members/MyProfileTests.cs` (new), class `MyProfileTests : IntegrationTestBase`.

Arrange: seed the member with `SeedAsync` (`Member.Create(...).Value`, fixed `CreatedAt`) and log in with `AuthenticateAs(member.Id)`.

Tests to add (status, body, then the database):
- logged in → GET 200 with my `id`, `name`, `email` (lower case) and `role` `"Student"`; the raw body (`ReadAsStringAsync`, compared in lower case) does not contain `password`; the member is unchanged in the database.
- logged in as a member that is not in the database (`AuthenticateAs` with a fixed id that was never seeded) → GET 404 `Member.NotFound`.
- no token → GET 401 and PUT 401 (two tests); the seeded member is unchanged.
- PUT with a valid new name → 204, empty body; in the database the member has the new name and the old email.
- PUT with a 1-character name → 400 `Member.InvalidName`, type `Validation`; the member is unchanged.

Rules for this feature:
- Fixed ids and times; never compare to `DateTime.Now`.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Public profile (Carina)

Endpoint: `GET /api/v1/members/{id}/profile` (`Features/Members/GetPublicProfile/`). Public: never use `AuthenticateAs` in these tests.
- 200: `{ id, name, memberSince, averageRating, reviewCount, activeListings: [ { id, title, price } ] }`. No email and no role.
- 404 `Member.NotFound` from the handler, with `{ code, message, type }` (read it into `Error`, check `Code` and `Type`).
- An id that is not a Guid (`/members/abc/profile`) does not match the route → 404; check only the status.

File (new): `Members/PublicProfileTests.cs`, class `PublicProfileTests : IntegrationTestBase`.

Arrange: seed with `SeedAsync`, fixed ids and times.
- The seller: `Member.Create(...).Value`. Its listings: `Listing.Create(sellerId, ...)`, published with `Publish()` for an `Active` one, left as is for a `Draft`.
- For a review: a sold listing (`Publish()`, `Reserve(buyer.Id, CreatedAt)`, `ConfirmSale(reservation.Id, CreatedAt)`) and `Review.Create(buyer.Id, soldListing, rating, null, CreatedAt).Value`; seed the buyer, the listing and the review together.

Tests to add (status, body, then the database):
- a seller with one Active and one Draft listing → 200; `activeListings` has only the Active one (its id, title and price); the right `name`; `reviewCount` 0 and `averageRating` null; the raw body (`ReadAsStringAsync`) does not contain the seller's email; the database is unchanged.
- a seller with one sold listing reviewed with rating 4 → 200, `averageRating` 4, `reviewCount` 1, `activeListings` empty.
- unknown id → 404 `Member.NotFound`, type `NotFound`; the seeded data is unchanged.
- `/api/v1/members/abc/profile` → 404 (status only).

Rules for this feature:
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Minimum category confidence (Diana)

File: `Listings/SuggestCategoryTests.cs` (add to it).

Tests to add:
- title "Banana", description "Am de vandut o banana buna" → 200, `category` is null, `confidence` below 0.7.
- title "Notite Analiza matematica, an I" → 200, `category` is "Notes".

## Feature: Maximum price (Diana)

File: `Listings/CreateListingTests.cs` (add to it).

Tests to add:
- POST a listing with price 200000 → 400 `Money.TooHigh`, nothing saved.

## Feature: Production configuration and demo data (Sara)
Production code: in `CampusMarketSetup.cs`, `ValidateCampusMarketProductionConfiguration` (only in Production: throws `InvalidOperationException` when `Jwt:Key` is missing or shorter than 32 characters) and `SeedDemoDataIfRequested` (only when `Seed:DemoData` is true and the database is empty: 3 published listings; a Student demo member only when `Seed:DemoPassword` has at least 8 characters, otherwise a warning and no member).
File: `Infrastructure/ProductionConfigurationTests.cs` (new).
Test host: start a separate host with `Factory.WithWebHostBuilder(b => ...)` and `UseSetting(...)` / `UseEnvironment(...)`, like the Admin seed tests; dispose it with `await using`.
Tests to add:
- Environment Production, `Jwt:Key` of 10 characters → creating the client throws `InvalidOperationException` whose message mentions `Jwt__Key`.
- Environment Production, `Jwt:Key` of 40 characters → `GET /health` 200.
- `Seed:DemoData` = true and `Seed:DemoPassword` = 8+ characters → `GET /api/v1/listings` gives `totalCount` 3; the database has exactly 1 member, with the Student role.
- `Seed:DemoData` = true without `Seed:DemoPassword` → 3 listings, 0 members.
- `Seed:DemoData` not set → 0 listings and 0 members (unchanged behaviour).
  Rules for this feature:
- Never hard-code a real key; use generated test strings.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.



## Feature: Price feedback loop (Sara)
Endpoint: `GET /api/v1/admin/ml/price-accuracy`, Admin only. Confirm sale now saves a `SaleRecord` (suggested price from the real price model, sold price = listing price).
File: `Admin/PriceAccuracyTests.cs` (new).
Tests to add:
- no token → 401; Student token → 403.
- Admin, no sales → 200, count 0, metrics null.
- seed a seller, a buyer and a published listing; reserve as the buyer, confirm the sale as the seller (204) → the database has 1 `SaleRecord` for that listing with sold price = listing price and suggested price > 0; then Admin GET → count 1 and the record in `lastRecords`.
  Rules: if a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: CORS for the frontend (Anca)

The web frontend (`frontend/`, Vite on `http://localhost:5173`) calls the API from another origin, so the browser needs CORS. Read `CampusMarketSetup.cs` (`AddCors`, policy `CampusMarketSetup.FrontendCorsPolicy` = `"Frontend"`), `Program.cs` (`app.UseCors(...)` is the first middleware, before `UseExceptionHandler`) and `appsettings.json` (`Cors:AllowedOrigins` = `["http://localhost:5173"]`) before generating.

Production code:
- Only the origins in `Cors:AllowedOrigins` are allowed; any header and any method; `Location` is an exposed header; no credentials (the token goes in the `Authorization` header, never in a cookie).
- The test host reads the same `appsettings.json`, so `http://localhost:5173` is allowed in every test without extra setup.

File: `Cors/CorsTests.cs`, class `CorsTests : IntegrationTestBase` (fixed `SellerId` as in the other classes; `private const string FrontendOrigin = "http://localhost:5173";`).

Tests to add:
- Preflight: `OPTIONS /api/v1/auth/login` with headers `Origin: http://localhost:5173`, `Access-Control-Request-Method: POST`, `Access-Control-Request-Headers: content-type,authorization` → 204; `Access-Control-Allow-Origin` is exactly `http://localhost:5173`; `Access-Control-Allow-Methods` contains `POST`; `Access-Control-Allow-Headers` contains `authorization` (compare case-insensitively); there is no `Access-Control-Allow-Credentials` header; no member in the database.
- Preflight from an origin that is not allowed (`http://evil.example`) → still 204, but there is no `Access-Control-Allow-Origin` header (the browser then blocks the call); no member in the database.
- Logged in as `SellerId`, `POST /api/v1/listings` with a valid body and `Origin: http://localhost:5173` → 201; `Access-Control-Allow-Origin` is the origin; `Access-Control-Expose-Headers` contains `Location`; the `Location` header is set; the listing is in the database.
- No token, `GET /api/v1/members/me/favorites` with the origin → 401 ProblemDetails `Auth.Unauthorized` and `Access-Control-Allow-Origin` is the origin (the browser can only read an error when the CORS header is there).
- A seeded draft of `SellerId`, client with `ThrowingUnitOfWork` (see "Feature: Error handling (Anca)"), the seller publishes it with the origin → 500 ProblemDetails `Server.Error` and `Access-Control-Allow-Origin` is still the origin; the listing is still `Draft`.
- The same request without an `Origin` header (`GET /api/v1/listings`) → 200 and no `Access-Control-Allow-Origin` header (clients that are not browsers see no change).
- Another origin through configuration, two tests (one HTTP call each), both with `Factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", "https://campusmarket.example"))` from a private helper, disposed with `await using`: a preflight from `https://campusmarket.example` gets `Access-Control-Allow-Origin: https://campusmarket.example`; a preflight from `http://localhost:5173` gets no `Access-Control-Allow-Origin` (index 0 was replaced). This is how the deployed frontend's address is set (`Cors__AllowedOrigins__0`). Both check that no member was saved.

Rules for this feature:
- Build the preflight with `new HttpRequestMessage(HttpMethod.Options, url)` and add the headers with `request.Headers.Add(...)`; read the CORS headers from `response.Headers` with `TryGetValues`, never from `response.Content.Headers`.
- Never change `appsettings.json`, `CampusMarketApiFactory` or `IntegrationTestBase`.
- This section goes with changes made by hand before the run (`CampusMarketSetup.cs`, `Program.cs`, `appsettings.json`, the new `frontend/` folder, `README.md`, `.dockerignore`, `.github/workflows/build.yml`), so the section 10 check on `git status` also lists them.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Register rate limiting (Carina)

Production code: `CampusMarketSetup.AddCampusMarket` adds the rate limiter policy "register": a fixed window of 1 minute per IP (`context.Connection.RemoteIpAddress`, or "unknown"), with the limit from `RateLimiting:RegisterPermitLimit` (default 5). `RegisterEndpoint` uses `.RequireRateLimiting("register")`. A rejected request gets 429 (status only, from `options.RejectionStatusCode`). `CampusMarketApiFactory` sets the limit to 1000 so the other tests are not blocked; do not change the factory or `IntegrationTestBase`.

File: `Auth/RegisterRateLimitTests.cs` (new), class `RegisterRateLimitTests : IntegrationTestBase`.

Test host: start a second copy of the API with the real limit, `Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:RegisterPermitLimit", "5"))`, the same way the Admin seed tests do; use only the client from its `CreateClient()` and dispose the copy with `await using`. In the test server every request falls in the same partition, so all of them count against one limit.

Request body: `{ name, email, password, confirmPassword }`, with a valid name, a different email for each request and a password of at least 8 characters.

Tests to add (status, body, then the database):
- 5 registrations with different emails → all 201; a 6th, valid, with a new email → 429; the database has exactly 5 members and none with the 6th email.
- 5 registrations whose passwords do not match → 400 each (they also count toward the limit); a 6th, valid → 429; the database has no members.
- with the normal factory (limit 1000), 6 registrations with different emails → all 201; the database has 6 members.

Rules for this feature:
- Check the members with `QueryDbAsync` on `db.Set<Member>()`.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Change password (Carina)

Endpoint: `POST /api/v1/auth/change-password` with `{ "currentPassword", "newPassword", "confirmNewPassword" }`, needs a token ("me" is the member in the token).

| Success | Failures |
|---|---|
| 204, empty body | 400 `Auth.InvalidCredentials` (wrong current password), 400 `Auth.WeakPassword`, 400 `Auth.PasswordsDoNotMatch`, 404 `Member.NotFound`, 401 (no token) |

The 401 is a ProblemDetails answer: check it as "Feature: Error handling (Anca)" says. The 400 and 404 come from the handler and keep `{ code, message, type }`: read them into `Error` and check `Code` and `Type`.

File: `Auth/ChangePasswordTests.cs` (new), class `ChangePasswordTests : IntegrationTestBase`.

Arrange: seed the member with a real hash, as `LoginTests` does (`new AspNetPasswordHasher().Hash("secret12")`), and log in with `AuthenticateAs(member.Id)`.

Tests to add (status, body, then the database):
- valid request → 204, empty body; in the database `AspNetPasswordHasher.Verify` accepts the new password and rejects the old one.
- after a successful change (Arrange), `POST /api/v1/auth/login` with the new password → 200 with a token; in another test, with the old password → 401.
- wrong current password → 400 `Auth.InvalidCredentials`; the stored hash is unchanged (the old password still verifies).
- new password of 7 characters → 400 `Auth.WeakPassword`; confirmation different → 400 `Auth.PasswordsDoNotMatch`; the hash is unchanged in both.
- a token of a member that is not in the database → 404 `Member.NotFound`.
- no token → 401; the member is unchanged.

Rules for this feature:
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Live and ready health checks (Diana)

Production code: `Infrastructure/ML/MlModelsHealthCheck.cs`; `/health/live` (no checks) and `/health/ready` (database + ML models) in `Program.cs`.

File: `Infrastructure/HealthTests.cs` (new or add to it).

Tests to add:
- `/health/live` → 200, body "Healthy".
- `/health/ready` → 200, body "Healthy" (the models are copied to the test output).
- `/health` → 200 (unchanged).
- If MlModelsHealthCheck has lines the HTTP tests cannot reach (the Unhealthy branch), add a test that calls CheckHealthAsync with a ModelsPath folder that does not exist → Unhealthy, message lists the missing files.

## Feature: Correlation id (Diana)

Production code: `Infrastructure/Logging/CorrelationIdMiddleware.cs`, registered first in `Program.cs`.

File: `Infrastructure/CorrelationIdTests.cs` (new), class `CorrelationIdTests : IntegrationTestBase`.

Tests to add:
- `GET /api/v1/listings` without the header → the response has `X-Correlation-Id`, not empty, and it is a valid Guid.
- the same request with `X-Correlation-Id: test-123` → the response header is exactly `test-123`.
- an error response (`GET /api/v1/listings/{random guid}` → 404) also has the header.

## Feature: Listings cache (Diana)

Production code: `GetListingsEndpoint` caches for 30 s with tag "listings"; `ListingsCache.ClearAsync` is called after every successful change of a listing (create, update, delete, publish, reserve, cancel reservation, confirm sale, hide).

Important: the ASP.NET Core output cache does not store responses to requests that have an `Authorization` header. Every search in these tests is sent **without a token**; only the change (create, publish, update, reserve) is sent with a token.

File: `Listings/ListingsCacheTests.cs` (replace its tests).

Setup: every test starts with an empty cache: at the start, get `IOutputCacheStore` from `Factory.Services` and call `EvictByTagAsync(ListingsCache.Tag, ...)`, so the tests do not affect each other.

Tests to add:
- the cache is used: search without a token → change a published listing directly in the database (through the DbContext, no endpoint, for example its title) → the same search without a token still returns the old title.
- search without a token → create a listing with a token → the same search without a token (the draft is not there, and this fills the cache) → publish with a token → the same search without a token → the new listing is there.
- search without a token → update a listing's title with a token → the same search without a token → the new title is there.
- search without a token → reserve the listing with a token → the same search without a token → it is gone (no longer Active).

## Feature: Validation thresholds (Anca)

Production code (read it before generating): the two decision thresholds of the ML models are settings, chosen on the validation data when the models are trained (`ml/models/*.json`, `"settings"`), not constants.
- `Ml:Category:MinConfidence` (`CategorySuggestionOptions` in `Common/Categories/`, 0.6 in `appsettings.json`): `SuggestCategoryHandler` returns `category` null below it. `SuggestCategoryHandler.MinConfidence` does not exist any more.
- `Ml:Spam:Threshold` (`SpamDetectionOptions` in `Common/Moderation/`, 0.4 in `appsettings.json`): `SpamDetector` reports spam when the model's probability reaches it, so `PublishListing` adds the automatic report from that value.
- Both are validated at startup (`ValidateOnStart` in `Infrastructure/ML/MlServiceCollectionExtensions.cs`): a value outside 0–1 stops the API with an `OptionsValidationException` whose message names the setting.

Changes to the existing tests (keep every test and its name):
- `Listings/SuggestCategoryTests.cs`: the two tests that compare the confidence with `SuggestCategoryHandler.MinConfidence` compare it with the configured value instead: `Factory.Services.GetRequiredService<IOptions<CategorySuggestionOptions>>().Value.MinConfidence`.

File: `Infrastructure/MlThresholdsTests.cs` (new), class `MlThresholdsTests : IntegrationTestBase` (fixed `SellerId`). A different setting goes through `Factory.WithWebHostBuilder(b => b.UseSetting(...))`, disposed with `await using`, like the Admin seed tests.

Tests to add:
- The normal test host reads `appsettings.json`: `IOptions<CategorySuggestionOptions>` has `MinConfidence` 0.6 and `IOptions<SpamDetectionOptions>` has `Threshold` 0.4 (resolve them from `Factory.Services`; no HTTP call in this one, the Act is resolving the options). The database is empty.
- `Ml:Category:MinConfidence` = `0.99`, logged in as `SellerId`, `POST /api/v1/listings/category-suggestion` with title `"Notite Analiza matematica, an I"` → 200, `category` null, `confidence` below 0.99 and at least 0.6 (the same text gives `Notes` with the normal setting). No listing saved.
- `Ml:Spam:Threshold` = `0`, a seeded draft of `SellerId` with the normal text of `SpamOnPublishTests` (`"Manual Algebra liniara, editia 2"`, `"Putin folosit, fara sublinieri."`, `Textbook`, price 40), the seller publishes it → 204, the listing is `Active` and one automatic report exists for it (`ReporterId` null): with threshold 0 every listing counts as spam.
- `Ml:Category:MinConfidence` = `1.5` → creating the client throws `OptionsValidationException` whose message contains `Ml:Category:MinConfidence`.
- `Ml:Spam:Threshold` = `-0.1` → creating the client throws `OptionsValidationException` whose message contains `Ml:Spam:Threshold`.

Rules for this feature:
- Never assert an exact confidence or probability from the real models, only ranges.
- For the two startup failures there is no HTTP call: the Act is `CreateClient()`; check that the database has no listing.
- In a copy made with `WithWebHostBuilder`, `AuthenticateAs` does not apply: set `Authorization: Bearer` with `TestTokens.Create(SellerId)` on the copy's client, as `ErrorHandlingTests` and `CorsTests` do.
- This section goes with changes made by hand before the run (`src/`, `ml/`, the unit tests generated by the unit-testing skill and the hand-written ML tests), so the section 10 check on `git status` also lists them.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Similar listings (Anca)

Endpoint: `GET /api/v1/listings/{id}/similar`, public (no token needed). It uses the **real** similar-listings model (`ml/models/similar-listings.zip`, copied to `ml-models/` like the others; do not replace `ISimilarListingsFinder` in the test factory). Read `Features/Listings/GetSimilarListings/` and `Infrastructure/ML/SimilarListingsFinder.cs` before generating.
- 200 `{ items: [{ id, title, category, condition, price, similarity }] }`: at most 5 `Active` listings, never the listing itself, the most similar first, `similarity` between 0 and 1.
- 404 `Listing.NotFound` (handler error body `{ code, message, type }`) for an unknown id.
- The target can have any status (a draft has similar listings too).

File: `Listings/GetSimilarListingsTests.cs` (new), class `GetSimilarListingsTests : IntegrationTestBase` (fixed `SellerId`, each listing with its own fixed `CreatedAt`).

Arrange for the ranking tests: published listings of `SellerId`, condition `Good`, each with this title and description:
- `"Manual Analiza matematica, editia 2"` — `"Manual pentru anul I, cu exercitii rezolvate."` (Textbook, 40)
- `"Culegere Analiza matematica, exercitii rezolvate"` — `"Culegere pentru examen, exercitii rezolvate pe capitole."` (Textbook, 30)
- `"Laptop Lenovo ThinkPad, i5, 8GB"` — `"Laptop pentru facultate, baterie buna, 8GB RAM."` (Electronics, 1500)
- `"Laptop Dell Latitude, i7, 16GB"` — `"Laptop rapid, 16GB RAM, SSD 512GB."` (Electronics, 2200)
- `"Notite curs Structuri de date"` — `"Notite scrise de mana, cursurile din anul II."` (Notes, 20)
- `"Bicicleta de oras"` — **no description** (Other, 300)
- plus a draft textbook `"Manual Analiza matematica, editia 1"` — `"Editie mai veche, cu sublinieri."` (Textbook, 25, not published).
Both kinds of description (with text and none) must be there: `SimilarListingsFinder` handles a missing description separately.

Tests to add:
- target the published `"Manual Analiza matematica, editia 2"` → 200; at most 5 items; the first item is the other math textbook; the draft and the target are not in the list; every item is `Active` in the database; similarities between 0 and 1, in decreasing order.
- target one laptop → 200; the first item is the other laptop.
- target the draft textbook → 200; the first item is a math textbook (the draft is not a candidate of itself, but it can be a target).
- the only published listing is the target (plus a draft) → 200 with an empty `items`.
- unknown id → 404 `Listing.NotFound`.
- every call is made without a token; the database is unchanged at the end (same number of listings).

Change to an existing test (keep it and its name): `Infrastructure/HealthTests.cs`, the "models folder does not exist" test also checks that the description contains `similar-listings.zip`.

Rules for this feature:
- Never assert an exact similarity from the real model; only the order of the clear cases above and the 0–1 range.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Password reset (Sara)

Endpoints:
- `POST /api/v1/auth/forgot-password` `{ email }` → 202 with the same body for a known email and for an unknown email (so the API never leaks whether the email exists).
- `POST /api/v1/auth/reset-password` `{ token, newPassword, confirmNewPassword }` → 204 on success, or 400 ProblemDetails with `Auth.InvalidResetToken` / `Auth.WeakPassword` / `Auth.PasswordsDoNotMatch`.

File: `Auth/PasswordResetTests.cs` (new), class `PasswordResetTests : IntegrationTestBase` (fixed `SellerId`, a seeded member with a known password).

Test host: replace `IEmailSender` with a capturing fake (a `TestEmailSender` in the test project that records every email in a thread-safe list) through `ConfigureTestServices` in a host started with `Factory.WithWebHostBuilder(...)`, disposed with `await using`. The handler builds the reset link with the plain token as `?token=...`; read it from the captured email's body with a private static helper (store the terminators in a `private static readonly char[]` field — CA1861). For the expiry test, the same `WithWebHostBuilder` host also replaces `TimeProvider` with a fake that returns a fixed `Now`. The `forgot-password` endpoint shares the "login" rate limit (if configured): keep each test to a few calls, and when the test needs to call it more often, the host is a fresh copy.

Tests to add:
- `POST /auth/forgot-password` always 202:
  - registered email → 202 with the standard body, one email captured for that address.
  - unknown email → 202 with exactly the same body, no email captured, no token in the database.
- Full flow: a registered member sends `forgot-password`, the test reads the plain token from the captured email's link, then `POST /auth/reset-password` with that token and a strong new password → 204; `POST /auth/login` with the new password → 200 and returns an access token; `POST /auth/login` with the old password → 401 `Auth.InvalidCredentials`. One extra email is captured for "password changed" and its body does not contain the new password. The token row in the database has `UsedAt` set.
- Reused token: the same token used a second time → 400 ProblemDetails with `Auth.InvalidResetToken`; the member's password is not changed again and no new email is captured.
- Expired token: through a fake `TimeProvider` set to `Now + PasswordResetToken.Lifetime` (30 minutes), reset with the token → 400 ProblemDetails with `Auth.InvalidResetToken`; the member's password is unchanged and no email is captured.
- Random token (never issued) → 400 ProblemDetails with `Auth.InvalidResetToken`.
- Weak password (`""` and a 7-character value, `[Theory]`) → 400 ProblemDetails with `Auth.WeakPassword`; a confirmation different from the new password → 400 ProblemDetails with `Auth.PasswordsDoNotMatch`.
- Rate limiting (only if `forgot-password` has a configured limit): call the endpoint until the limit is reached; the next call returns 429. Check it on a fresh `WithWebHostBuilder` host so the other tests are not touched; skip this test if no limit is configured.

Rules for this feature:
- Never send a real email: the capturing `IEmailSender` is registered in every test host; never open an SMTP connection.
- Never use real time for the 30-minute boundary: the expired-token test uses the fake `TimeProvider` through `ConfigureTestServices`. For `sentAt`/`createdAt` that come from HTTP, assert that they are close to now (`BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))`), never an exact value.
- Never put a constant array literal in an argument (CA1861): the token-extraction helper reads terminators from a `private static readonly char[]` field.
- 400 bodies are ProblemDetails, as in "Feature: Error handling (Anca)": assert on the response body's `Extensions`/`Detail` for `Code`, or on the response status plus the `Code` that the handler puts in ProblemDetails.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.


## Feature: Conversations (Anca)

Endpoints (all need a token; the member is the user in the token):

| Request | Who | Success | Failures |
|---|---|---|---|
| `POST /api/v1/listings/{listingId}/conversations`, body `{ text }` | a member who is not the seller | 201, body `{ conversationId, id, number, senderId, text, sentAt }`, `Location` ends with `/api/v1/conversations/{conversationId}`. The first message starts the conversation; if this member already has one about the listing, the message is added to it (same `conversationId`, next `number`) | 404 `Listing.NotFound`; 404 `Member.NotFound`; 400 `Conversation.OwnListing` (the seller), `Conversation.ListingNotAvailable` (draft, sold or hidden listing, only for a new conversation), `Message.Empty`, `Message.TooLong` (over 1000 characters after trim); 401 |
| `POST /api/v1/conversations/{id}/messages`, body `{ text }` | the buyer or the seller of the conversation | 201, same body and `Location` | 404 `Conversation.NotFound`; 403 `Conversation.NotParticipant` (anyone else, an Admin too; body is our `Error`); 400 `Message.Empty`, `Message.TooLong`; 401 |
| `GET /api/v1/conversations/{id}` | the buyer or the seller | 200 `{ id, listingId, listingTitle, sellerId, sellerName, buyerId, buyerName, createdAt, messages: [{ id, number, senderId, text, sentAt }] }`, messages in `number` order | 404 `Conversation.NotFound`; 403 `Conversation.NotParticipant`; 401 |
| `GET /api/v1/members/me/conversations` | any member | 200 `{ items: [{ id, listingId, listingTitle, otherMemberId, otherMemberName, lastMessage, lastMessageSenderId, lastMessageAt, messageCount }], count }`, as buyer and as seller, the latest message first | 401 |

Read `Features/Conversations/`, `Domain/Conversations/` and `Infrastructure/Persistence/Configurations/ConversationConfiguration.cs` / `MessageConfiguration.cs` before generating. The `Conversations` table has foreign keys to `Listings` and to `Members` twice (seller and buyer), all with cascade delete, and a unique index on `(ListingId, BuyerId)`; `Messages` belong to their conversation (cascade). So the seller **and** the buyer must be seeded as members.

Files: `Conversations/StartConversationTests.cs`, `Conversations/SendMessageTests.cs`, `Conversations/GetConversationTests.cs`, `Conversations/GetMyConversationsTests.cs`, same base class and login helper as the other classes. Read the bodies into `MessageResponse`, `GetConversationResponse` and `GetMyConversationsResponse` from `Features/Conversations/`.

Arrange: seed the seller, the buyer and (when needed) another member with `Member.Create(...).Value`; a published listing is `Listing.Create(seller.Id, ...).Value` then `Publish()` (sold: `Reserve` then `ConfirmSale`). An existing conversation is `Conversation.Start(buyer.Id, listing, "text", CreatedAt).Value`, more messages with `conversation.Send(memberId, "text", CreatedAt.AddMinutes(n))`, seeded together with the members and the listing in one `SeedAsync`. Log in with `AuthenticateAs(member.Id)`.

Tests to add (status, body, then the database through `db.Set<Conversation>()` / `db.Set<Message>()`):
- `StartConversationTests`: the buyer on a published listing with `"  Mai e disponibil?  "` → 201 with number 1, the buyer as sender, the trimmed text and `Location`; one conversation (this listing, seller and buyer) and one message saved; the same buyer again → 201 with the same `conversationId` and number 2, still one conversation and two messages; a reserved listing → 201; the seller → 400 `Conversation.OwnListing`; a draft → 400 `Conversation.ListingNotAvailable`; a sold listing with no conversation → 400 `Conversation.ListingNotAvailable`; a sold listing where the buyer already has a conversation (seed it before the sale) → 201, number 2; empty text → 400 `Message.Empty`; 1001 characters → 400 `Message.TooLong`; unknown listing → 404 `Listing.NotFound`; logged in as a member that is not in the database → 404 `Member.NotFound`; no token → 401 ProblemDetails `Auth.Unauthorized`. Nothing saved in any failure.
- `SendMessageTests`: the seller answers → 201, number 2, sender the seller, `Location`; the message is saved and the conversation's `LastMessageAt` is the time of the answer; the buyer answers again → number 3; another member → 403 `Conversation.NotParticipant` with `Type` `Forbidden`; an Admin (`AuthenticateAs(AdminId, "Admin")`, as in the moderation tests) → 403; empty → 400 `Message.Empty`; unknown conversation → 404 `Conversation.NotFound`; no token → 401. No message saved in any failure.
- `GetConversationTests`: as the buyer and as the seller → 200 with every field (names and listing title), three messages in number order; another member → 403 `Conversation.NotParticipant`; unknown → 404 `Conversation.NotFound`; no token → 401. The database is unchanged after every call.
- `GetMyConversationsTests`: the seller with two conversations (two buyers) → 200, `count` 2, the latest message first, `otherMemberId`/`otherMemberName` = the buyer, `lastMessage`, `lastMessageSenderId` and `messageCount` checked; the buyer → one item with the seller as the other member; another member → empty `items`, `count` 0; no token → 401. The database is unchanged after every call.
- Cascade, in `StartConversationTests`: the seller deletes the listing (`DELETE /api/v1/listings/{id}` logged in as the seller) → 204, and its conversation and messages are gone from the database.

Rules for this feature:
- Read 400/403/404 bodies of our handlers into `Error` and check `Code` and `Type`; 401 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- Real time is used for the messages sent through HTTP: assert that `sentAt` is close to now (`BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))`), never an exact value.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Listing photos (Anca)

Endpoints:

| Request | Who | Success | Failures |
|---|---|---|---|
| `POST /api/v1/listings/{listingId}/photos`, `multipart/form-data` with the image in the `file` field | the seller (any listing status) | 201, body `{ id, listingId, url, contentType, sizeBytes, createdAt }` (`url` = `/api/v1/photos/{id}`), `Location` ends with `/api/v1/photos/{id}`; the file is saved on disk | 404 `Listing.NotFound`; 403 `Photo.NotSeller` (body is our `Error`); 400 `Photo.LimitReached` (already 5), `Photo.Empty` (no `file` field or an empty file), `Photo.TooLarge` (over 2 MB = 2 097 152 bytes), `Photo.InvalidFormat` (not JPEG/PNG/WebP by its first bytes, whatever the name and type sent); 401 |
| `GET /api/v1/listings/{listingId}/photos` | everyone | 200 `{ items: [...same fields...], count }` in the order they were added | 404 `Listing.NotFound` |
| `GET /api/v1/photos/{id}` | everyone | 200 with the image bytes, `Content-Type` of the photo, `Cache-Control: public, max-age=86400` | 404 `Photo.NotFound` (unknown, or the file is missing on disk) |
| `DELETE /api/v1/listings/{listingId}/photos/{photoId}` | the seller | 204; the row and the file are gone | 404 `Listing.NotFound`, `Photo.NotFound` (also a photo of another listing); 403 `Photo.NotSeller`; 401 |

Also: `DELETE /api/v1/listings/{id}` now removes the listing's photo folder from disk (the rows go by cascade).

Read `Features/Photos/`, `Domain/Photos/`, `Common/Storage/`, `Infrastructure/Storage/LocalPhotoStorage.cs` and `Infrastructure/Persistence/Configurations/PhotoConfiguration.cs` before generating. Files are stored as `<Storage:PhotosPath>/<listingId>/<photoId>.<ext>`.

Change to the test infrastructure (keep everything else): `Infrastructure/CampusMarketApiFactory.cs` sets `Storage:PhotosPath` to a new folder under `Path.GetTempPath()` (a new `Guid` in the name, one per factory), exposes it as a public `PhotosPath` property and deletes the folder (if it exists) in `Dispose`. No test may write photos into the source tree.

Files: `Photos/UploadPhotoTests.cs`, `Photos/GetListingPhotosTests.cs`, `Photos/GetPhotoFileTests.cs`, `Photos/DeletePhotoTests.cs`, same base class and login helper as the other classes. Read the bodies into `PhotoResponse` and `GetListingPhotosResponse` from `Features/Photos/`. Upload with `MultipartFormDataContent` and a `ByteArrayContent` named `file` (with a file name). Test images are byte arrays that start with the signatures (JPEG `FF D8 FF`, PNG `89 50 4E 47 0D 0A 1A 0A`, WebP `"RIFF"` + 4 bytes + `"WEBP"`) followed by zeros, as `static readonly` fields.

Arrange: seed the seller (and another member when needed) and a listing of the seller. Existing photos: upload them through the endpoint as the seller (so the file is on disk), except for the "already 5" case, where 5 `Photo.Create(...)` rows may be seeded directly (no files needed).

Tests to add (status, body, then the database through `db.Set<Photo>()` and the disk under `Factory.PhotosPath`):
- `UploadPhotoTests`: the seller uploads a PNG → 201 with `contentType` `image/png`, the size, `url` and `Location`; one row (this listing, `FileName` = `<id>.png`) and the file on disk with the same bytes; a JPEG and a WebP → 201 with their types; a draft listing → 201; a GIF sent as `image/png` with name `a.png` → 400 `Photo.InvalidFormat`; a file of 2 097 153 bytes (PNG start) → 400 `Photo.TooLarge`; exactly 2 097 152 bytes → 201; no `file` field (another field name) → 400 `Photo.Empty`; 5 photos already → 400 `Photo.LimitReached`; another member → 403 `Photo.NotSeller`; unknown listing → 404 `Listing.NotFound`; no token → 401 ProblemDetails `Auth.Unauthorized`; a JSON body instead of a form → 415. No row and no file in any failure.
- `GetListingPhotosTests`: no token, three uploaded photos → 200, `count` 3, in upload order, every field; a listing with no photo → empty `items`; photos of another listing are not returned; unknown listing → 404. The database is unchanged.
- `GetPhotoFileTests`: no token, an uploaded PNG → 200, the same bytes, `Content-Type` `image/png`, the `Cache-Control` header; unknown id → 404 `Photo.NotFound`; the row exists but the file was deleted from disk by the test → 404 `Photo.NotFound`.
- `DeletePhotoTests`: the seller deletes one of two photos → 204, the row and the file are gone, the other photo stays (row and file), then `GET /api/v1/photos/{id}` → 404; another member → 403 `Photo.NotSeller`; a photo of another listing (same seller) → 404 `Photo.NotFound`; unknown photo → 404; unknown listing → 404 `Listing.NotFound`; no token → 401. Nothing removed in any failure.
- Cascade, in `DeletePhotoTests`: the seller deletes the listing (`DELETE /api/v1/listings/{id}`) → 204, its photo rows are gone and its folder under `Factory.PhotosPath` no longer exists; deleting a listing that never had a photo → 204 (no folder, no error).

Rules for this feature:
- Read 400/403/404 bodies of our handlers into `Error` and check `Code` and `Type`; 401 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.


## Feature: Full coverage (Carina)

Issue #121: SonarCloud must show 100% line and branch coverage and every mutant killed. After every test project runs, these cases are still not covered (the coverage analysis of #121). Add the tests below and nothing else; keep every existing test. Read each production file named here before generating.

Production code:
- `Features/Admin/PriceAccuracy/PriceAccuracyEndpoint.cs`: `GET /api/v1/admin/ml/price-accuracy`, `RequireAuthorization("Admin")`, returns 200 with the handler's `PriceAccuracyResponse` (`count`, `mae`, `mape`, `perCategory`, `lastRecords`). It reads the records through `Infrastructure/Persistence/EfSaleRecordRepository.GetAllAsync`. `SaleRecords` has no foreign key to `Listings`, so a record can be seeded alone.
- `Features/Members/UpdateMyProfile/UpdateMyProfileEndpoint.cs`: a `NotFound` error from the handler → 404 with `Error`; any other error → 400.
- `CampusMarketSetup.cs`:
    - the `register` rate limit policy is partitioned by `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`; the existing `RegisterRateLimitTests` only use the `"unknown"` partition.
    - `ValidateCampusMarketProductionConfiguration`: in Production, `string.IsNullOrWhiteSpace(Jwt:Key)` or a key shorter than 32 characters → `InvalidOperationException`. The existing tests cover only the short key.
    - `SeedDemoDataIfRequested`: returns without seeding when the database already has a member or a listing.
    - `TrySeedDemoMember`: with a `Seed:DemoPassword` of at least 8 characters it calls `Member.Register("Demo Student", ..., hasher.Hash(password), ...)`; when that fails (an empty hash → `Member.PasswordRequired`) it returns `null`, so no member is saved and the 3 demo listings are still seeded, owned by a new random seller id.
- `Infrastructure/Mailing/SmtpEmailSender.cs`: builds a `MailMessage` and an `SmtpClient(Host, Port)` with `EnableSsl = UseSsl`; when the environment variable `Email__Username` is set it sets `NetworkCredential(Email__Username, Email__Password)`; then `SendMailAsync`.

Files and tests to add (status, body, then the database, as section 4 says):
- `Admin/PriceAccuracyTests.cs` (new), class `PriceAccuracyTests : IntegrationTestBase`, fixed `AdminId`, `OtherStudentId`, `CreatedAt`:
    - as Admin (`AuthenticateAs(AdminId, "Admin")`), two `SaleRecord`s seeded with `SeedAsync` (`SaleRecord.Create(listingId, category, condition, suggested, sold, soldAt).Value`; suggested 100 / sold 80 in `Textbook` and suggested 50 / sold 50 in `Electronics`, with different fixed `soldAt`) → 200, read into `PriceAccuracyResponse`: `count` 2, `mae` 10, `mape` 12.5, one `perCategory` row per category, `lastRecords` newest first with every field of the seeded records; the database still has exactly the 2 records.
    - as a student → 403 ProblemDetails `Auth.Forbidden`; no token → 401 ProblemDetails `Auth.Unauthorized` (as in "Feature: Error handling (Anca)"); the seeded record is unchanged in both.
- `Members/MyProfileTests.cs` (add one test): `PUT /api/v1/members/me` with a valid name, logged in as `UnknownMemberId` (never seeded) → 404 `Member.NotFound`, type `NotFound`; a seeded member is unchanged and there is still exactly one member.
- `Auth/RegisterRateLimitTests.cs` (add two tests): a copy of the API with `RateLimiting:RegisterPermitLimit` 5 **and** `TestRemoteIpStartupFilter` (register it with `ConfigureTestServices(s => s.AddSingleton<IStartupFilter, TestRemoteIpStartupFilter>())`, as `LoginTests` does); five valid registrations with `X-Test-Remote-Ip: 10.0.0.1`, then the Act: one valid registration with `X-Test-Remote-Ip: 10.0.0.2` → 201 (another IP has its own limit), 6 members; and a second test where the sixth registration comes from `10.0.0.1` → 429, 5 members and none with the sixth email. Set the header on each `HttpRequestMessage`, not on the client.
- `Infrastructure/ProductionConfigurationTests.cs` (add three tests):
    - Production with `Jwt:Key` = `"   "` (only spaces) → `CreateClient()` throws `InvalidOperationException` whose message mentions `Jwt__Key`; the database has no listing and no member.
    - `Seed:DemoData` true and a valid `Seed:DemoPassword`, with one member already seeded with `SeedAsync` → `GET /api/v1/listings` → 200, `totalCount` 0; the database still has exactly that one member and no listing.
    - `Seed:DemoData` true, a valid `Seed:DemoPassword` and `IPasswordHasher` replaced by the existing `Infrastructure/EmptyHashPasswordHasher` (singleton, as in `SeedAdminTests`) → `GET /api/v1/listings` → 200, `totalCount` 3; the database has 0 members and 3 listings, and their `SellerId` is the same for all three and is not the id of any member.
- `Infrastructure/SmtpEmailSenderTests.cs` (new), class `SmtpEmailSenderTests` (not an `IntegrationTestBase`: there is no API and no database here), with a new fake `Infrastructure/FakeSmtpServer.cs`:
    - `FakeSmtpServer`: a `TcpListener` on `IPAddress.Loopback` and port 0 (a free port chosen by the OS, read back from `LocalEndpoint`), `IAsyncDisposable`. It accepts one connection and answers the basic SMTP commands in plain text: `220` greeting; `EHLO`/`HELO` → `250` with `AUTH LOGIN` advertised; `AUTH LOGIN` (with or without the initial username) → `334` prompts, then `235`; `MAIL FROM`, `RCPT TO` → `250`; `DATA` → `354`, read until the line `.`, then `250`; `QUIT` → `221` and close. It records the decoded username and password (null when no `AUTH` came), the `MAIL FROM` and `RCPT TO` lines and the message data, and completes a `Task` when the dialog ends. One-line comment on top, like the other fakes.
    - a send without `Email__Username` → `SendAsync` completes; the server got the message with the `From`, the `To`, the subject and the body, and no `AUTH` (username null).
    - a send with `Email__Username` and `Email__Password` set → `SendAsync` completes; the server got the same message and the decoded username and password equal the two variables.
    - Options: `Options.Create(new EmailOptions { Host = "127.0.0.1", Port = server.Port, From = "noreply@campusmarket.local", UseSsl = false })`. `UseSsl` false is only for this local fake (there is no TLS on it); never use it for anything else.
    - These tests change process environment variables, so they must never run in parallel with another test: put the class in `[Collection(SmtpEnvironmentCollection.Name)]` and define `SmtpEnvironmentCollection` with `[CollectionDefinition(Name, DisableParallelization = true)]` in the same file. The constructor saves the current values of `Email__Username` and `Email__Password` and clears them; `Dispose` restores the saved values, so every test starts without the variables and nothing leaks.
    - Every wait on the server has a timeout (`WaitAsync(TimeSpan.FromSeconds(5))`), so a broken dialog fails the test instead of hanging it.

Rules for this feature:
- This section is the only exception to two general rules: `FakeSmtpServer` listens on a local port (section 7 forbids a real server for the API, which is still never started on a port), and `SmtpEmailSenderTests` opens an SMTP connection, but only to `FakeSmtpServer` on `127.0.0.1` ("Feature: Password reset (Sara)" still forbids any other SMTP connection). Never connect to a real SMTP server.
- Never change `CampusMarketApiFactory` or `IntegrationTestBase` in this feature (the temporary `PhotosPath` of "Feature: Listing photos (Anca)" is already there and stays as it is); every special setup goes through `Factory.WithWebHostBuilder(...)`, disposed with `await using`.
- Never hard-code a real JWT key or a real password; use generated test strings, as `ProductionConfigurationTests` does.
- Then run all the tests with coverage as the CI does (`dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings`): `PriceAccuracyEndpoint.cs`, `EfSaleRecordRepository.cs`, `UpdateMyProfileEndpoint.cs`, `CampusMarketSetup.cs`, `SmtpEmailSender.cs`, `ConfirmSaleHandler.cs` and `ResetPasswordHandler.cs` are at 100% for lines and branches. If a line or branch is still not covered, stop and report which one and why; never change `src/` and never write a test whose only purpose is to execute a line without checking its behaviour.
- Run `dotnet test tests/CampusMarket.IntegrationTests` three times in a row: the new tests must never fail.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Sorting (Diana)

File: `Listings/GetListingsTests.cs` (add to it).

Tests to add:
- `?sort=price_asc` → 200, prices in ascending order.
- `?sort=abc` → 400 `Listings.InvalidSort`.

## Feature: Notifications (Sara)
Endpoints (token needed): `GET /api/v1/members/me/notifications?unreadOnly=&page=&pageSize=`, `POST /api/v1/members/me/notifications/{id}/read` (204, 404 if not mine or unknown), `POST /api/v1/members/me/notifications/read-all` (204).
File: `Notifications/NotificationsTests.cs` (new).
Tests to add:
- seller and buyer seeded, published listing; the buyer reserves → the seller's GET has 1 unread ReservationCreated notification for that listing; the buyer's GET is empty.
- the seller confirms the sale → the buyer has 1 ListingSold notification.
- mark one as read → 204, `unreadCount` 0, `readAt` set in the database; marking someone else's → 404.
- read-all → 204, all mine read.
- no token → 401 on the three endpoints.
- the list order: seed two notifications of the seller with `Notification.Create(seller.Id, NotificationType.ReservationCreated, "...", listing.Id, createdAt)` at two fixed times (the older one first in the seed) → `GET` returns the newer first, `count` 2, `totalCount` 2, `unreadCount` 2, every field of both items; after marking the newer one as read, `?unreadOnly=true` → only the older one, `totalCount` 1, `unreadCount` 1.
- paging: `?page=0` → 400 `Notifications.InvalidPage`; `?pageSize=101` → 400 `Notifications.InvalidPageSize` (body is our `Error`); the database is unchanged.
  Rules: if a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Admin categories (Carina)

Production code (written by hand, read it before generating): `Domain/Categories/` (`ListingCategory`, `ICategoryRepository`), `Features/Categories/` (`GetCategories`, `UpdateCategory`), `Infrastructure/Persistence/EfCategoryRepository.cs`, `Infrastructure/Persistence/Configurations/ListingCategoryConfiguration.cs`, and the category check in `CreateListingHandler`, `UpdateListingHandler` and `PublishListingHandler`.

| Request | Who | Success | Failures |
|---|---|---|---|
| `GET /api/v1/categories` | everyone (no token) | 200 `{ items: [{ key, displayName, description, isActive }], count }`, in enum order (Textbook, Notes, Electronics, LabEquipment, Other), active and inactive ones | none |
| `PUT /api/v1/categories/{category}`, body `{ displayName, description?, isActive }` | Admins only (`RequireAuthorization("Admin")`) | 204, empty body; name and description saved trimmed, a blank description saved as `null` | 401 ProblemDetails `Auth.Unauthorized`; 403 ProblemDetails `Auth.Forbidden` (a member); 404 `Category.NotFound` (the key is matched by name, case-insensitive); 400 `Category.IsActiveRequired` (`isActive` missing), `Category.DisplayNameEmpty`, `Category.DisplayNameTooLong` (over `ListingCategory.MaxDisplayNameLength` = 100), `Category.DescriptionTooLong` (over `ListingCategory.MaxDescriptionLength` = 500); 409 `Category.LastActive` (deactivating the last active category) |

Listings: `POST /api/v1/listings` in an inactive category → 400 `Listing.CategoryInactive`; `PUT /api/v1/listings/{id}` that **moves** a listing into an inactive category → 400 `Listing.CategoryInactive`, while editing a listing that is already in an inactive category without changing the category succeeds (204); `POST /api/v1/listings/{id}/publish` of a draft whose category is inactive → 400 `Listing.CategoryInactive`.

Facts:
- The 5 categories are **seeded by the migration** `AddCategories` (`HasData` in `ListingCategoryConfiguration`): `Textbook` "Manuale", `Notes` "Notițe", `Electronics` "Electronice", `LabEquipment` "Echipament de laborator", `Other` "Altele", all active, description `null`. Every test database already has them; never seed or delete category rows.
- Read the bodies into `GetCategoriesResponse` / `GetCategoriesItem` and send `UpdateCategoryRequest` (its fields are nullable, so `isActive` can be left out).

Files: `Categories/GetCategoriesTests.cs`, `Categories/UpdateCategoryTests.cs`, `Listings/InactiveCategoryTests.cs` (new).

Arrange: to deactivate a category, a private helper in the test class opens a scope, loads all the categories from `CampusMarketDbContext` (tracked), calls the domain `Update(displayName, description, isActive: false, all)` on the ones to deactivate and `SaveChangesAsync`. Never deactivate through the endpoint in an Arrange.

Tests to add (status, body, then the database through `db.Set<ListingCategory>()` / `db.Set<Listing>()`):
- `GetCategoriesTests`: no token → 200, `count` 5, the 5 seeded categories in order with key, display name, description and `isActive`, all active; the database still has exactly the 5 seeded ones.
- `UpdateCategoryTests`: Admin → 204, empty body, then `GET /api/v1/categories` (in the Assert) shows the new trimmed display name, description and `isActive`; the database has the new values and the other 4 unchanged. No token → 401; a member → 403; unknown key (`Books`) → 404 `Category.NotFound`; missing `isActive` → 400 `Category.IsActiveRequired`; display name `""`, `"   "`, `null` → 400 `Category.DisplayNameEmpty`; 101 characters → 400 `Category.DisplayNameTooLong`; description of 501 characters → 400 `Category.DescriptionTooLong`; the 4 others deactivated in the Arrange, deactivating the last one → 409 `Category.LastActive` (type `Conflict`) and it stays active. Every failure: the 5 categories are unchanged.
- `InactiveCategoryTests`: create in an inactive category → 400 `Listing.CategoryInactive`, no listing; move a listing into an inactive category → 400, listing unchanged; edit a listing already in an inactive category without changing the category → 204, new values, `Version` 2; publish a draft whose category became inactive → 400, still `Draft` with `Version` 1, no report.
- Existing listings in a deactivated category (decision of week 8, task 12): deactivating a category only stops **new** listings (create, move by edit, publish draft). An `Active` listing whose category is deactivated afterwards stays visible. In `InactiveCategoryTests`, seed a published listing (`Publish()` before `SeedAsync`), then deactivate its category with the helper, then send the one HTTP call without a token: `GET /api/v1/listings` → 200, the listing is in `items` (`totalCount` 1); `GET /api/v1/listings?category=<that category>` → 200, the listing is returned; `GET /api/v1/listings/{id}` → 200 with the listing's fields and `Active`. Every test checks at the end that the listing is unchanged (`Active`, `Version` 2) and that the category is still inactive in the database.

Rules for this feature:
- A test that deactivates a category must not affect other tests: it deactivates only in its own database (every test has a fresh factory and in-memory database, section 3), never through a shared or static state, and never with a `WithWebHostBuilder` copy kept between tests.
- The existing publish, create and edit integration tests (`PublishListingTests`, `CreateListingTests`, `UpdateListingTests`) stay unchanged: they run with all 5 categories active, as the migration seeds them. The new cases go in `Listings/InactiveCategoryTests.cs`.
- Read 400/404/409 bodies of our handlers into `Error` and check `Code` and `Type`; 401 and 403 bodies are ProblemDetails, as in "Feature: Error handling (Anca)".
- Then run all the tests with coverage as the CI does: `Domain/Categories/`, `Features/Categories/`, `EfCategoryRepository.cs`, `ListingCategoryConfiguration.cs`, the three listing handlers, `CampusMarketSetup.cs` and `CampusMarketDbContext.cs` are at 100% for lines and branches (all test projects merged).
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Spam rules (Anca)

`Infrastructure/ML/SpamRules.cs` (new) and `Infrastructure/ML/SpamDetector.cs` (changed): on publish (`POST /api/v1/listings/{id}/publish`) a listing is spam when the **real** model's probability reaches `Ml:Spam:Threshold` **or** when one of the rules matches its title or description (compared in lower case, without diacritics, after removing email addresses): a phrase from `SpamRules.Phrases` (for example `whatsapp`, `scrie-mi pe`, `bani usor`, `castiga`, `plata in avans`), a link (`http://`, `www.`, or a word ending in `.ro/.com/.net/.org/.info/.biz/.xyz/.io/.me`), a Romanian mobile number (`07xx xxx xxx`, `+40 7…`), an `@handle`, or `!!!`. When only a rule caught it, the automatic report has `SpamProbability` = `SpamRules.RuleProbability` (0.9); when the model caught it, the model's own probability (rounded to two decimals). A spam listing gets one automatic report (`Report.IsAutomatic`, reporter null) and stays `Active`, as before.

Read `SpamRules.cs`, `SpamDetector.cs`, `Features/Listings/PublishListing/` and the existing "Feature: Spam and moderation (Anca)" tests before generating.

File: `Moderation/SpamRulesTests.cs` (new), class `SpamRulesTests : IntegrationTestBase`: seed the seller, create a draft through `Listing.Create(seller.Id, title, Category.Other, Condition.Good, 40m, CreatedAt, description)` (price 40 for every listing: the model reads the price) and seed it, then publish it with the real endpoint logged in as the seller. Check the database through `db.Set<Report>()` for that listing.

Tests to add (one listing each, 204 from publish every time):
- rule only, each rule on its own (the model does not flag these): title `"Manual de fizica"`, description `"Plata in avans prin transfer"` (phrase); `"Manual de fizica"` / `"Detalii pe www.manuale-ieftine.com"` (link); `"Calculator Casio fx-991"` / `"Sunati la 0722 123 456"` (phone); `"Halat laborator"` / `"Mesaj la @halat_m"` (handle); `"Manual POO ieftin!!!"` / `"Stare foarte buna."` (`!!!`); `"Câștigă bani ușor"` / `"Fără investiție"` (diacritics removed before the phrases) → one automatic report with `SpamProbability` exactly 0.9. Use a `[Theory]` with the title and description as data.
- the model flags it: `"Vand cont Netflix premium ieftin"` / `"Scrie-mi pe whatsapp pentru detalii"` → one automatic report with `SpamProbability` above 0.9 (the model's value, not the rule's).
- not spam: `"Manual chimie organica"` / `"Folosit un semestru, cateva notite pe margine"`; `"Halat laborator"` / `"Marimea M, contact: ana.pop@student.ro"` (an email is not a link) → no report.

Rules for this feature:
- Never assert an exact probability from the model; only `> 0.9` for the model case and exactly `0.9` for the rule-only cases.
- If the model already flags one of the rule-only texts (its probability is not exactly 0.9), stop and report which one; do not change `src/` and do not change the texts on your own.


## Feature: My listings (Diana)

Endpoint: `GET /api/v1/members/me/listings` (`Features/Members/GetMyListings/GetMyListingsEndpoint.cs`). It needs a login (`RequireAuthorization()`), takes no parameters and always answers 200 `{ items, count }`; each item is `id`, `sellerId`, `title`, `category`, `condition`, `price`, `status`, `createdAt`. It returns the listings of the logged-in member in **every** status (`Draft`, `Active`, `Reserved`, `Sold`, `Hidden`), newest `CreatedAt` first, then `Id`. It is not output-cached, so a change shows up in the next call. Failure: 401 only (no token); the 401 body is ProblemDetails `Auth.Unauthorized`, as in "Feature: Reservations (Sara)".

File: `Members/GetMyListingsTests.cs` (new), class `GetMyListingsTests : IntegrationTestBase`, with the fixed `SellerId`, `OtherStudentId` and `CreatedAt` of the other member tests. Members do not need to exist in the database to log in.

Arrange: seed listings with `SeedAsync`, built in the domain before seeding: `Listing.Create(sellerId, ...)` (`Draft`), `Publish()` (`Active`), then `Reserve(buyerId, CreatedAt)` (`Reserved`) or `Hide()` (`Hidden`). Log in with `AuthenticateAs(memberId)` before the one HTTP call.

Tests to add (status, body, then the database still has the same listings):
- no token → 401, the ProblemDetails `Auth.Unauthorized`; the database has 0 listings.
- two members with listings (two for `SellerId`, one for `OtherStudentId`), logged in as `SellerId` → 200, `count` 2, only the two ids of `SellerId`. A second test, logged in as `OtherStudentId` → 200, `count` 1, only their own id. One call per test.
- a new `Draft` listing appears immediately: the first `GET` (part of the Arrange, logged in as `SellerId`, no listings yet) → 200; then seed a `Draft` listing of `SellerId` and make the Act `GET` again → 200, `count` 1, the `Draft` listing is in `items` with status `Draft`. This also shows the response is not cached.
- a listing in another status still appears: one `Hidden` and one `Reserved` listing of the member → both in `items`, with the right `status`, `count` 2.
- order: three listings of the member with `CreatedAt`, plus 1 hour, plus 2 hours, seeded oldest first → `items` newest first, checked by id with `Equal`.

Rules for this feature:
- Read the success body into `GetMyListingsResponse`; check the order of `items` by id, not only their count.
- For 401 check the status code and the ProblemDetails code, as in the reservations tests.
- If a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Observability (Sara)
Endpoint: `GET /api/v1/admin/metrics` (Admin only).
File: `Admin/MetricsTests.cs` (new).
Tests to add: no token → 401; Student → 403; Admin after a few `GET /api/v1/listings` and one 404 → the listings route has the right count, the 404 is counted and not as an error; `/health` and the metrics endpoint itself are not counted; after one price suggestion the price model has 1 prediction.
Rules: if a test fails because the API behaves differently from this list, stop and report it; do not change `src/`.

