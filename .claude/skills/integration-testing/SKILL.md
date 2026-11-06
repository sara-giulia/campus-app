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
- `"not-an-email"` in the config → `GET /` → 200 with the body `CampusMarket API is running`; no member saved.
- a valid email and an empty password in the config → `GET /` → 200 with the same body; no member saved.

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
