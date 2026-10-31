---
name: unit-testing
description: Generates xUnit unit tests with FluentAssertions 7.* for the domain and for the CQRS handlers of CampusMarket, with hand-written fakes. Use when adding or changing domain classes or handlers, or when coverage of Domain/ or of the handlers is below 100%.
---

# Unit testing skill for CampusMarket

## Goal
Generate unit tests for `src/CampusMarket.Api/Domain/` and for every handler in `Features/`, reaching 100% line and branch coverage, with assertions that check behaviour. Never edit production code to make a test pass.

## Project facts (read the real files, do not guess)
- Test project: `tests/CampusMarket.UnitTests` (net10.0). It already references xUnit 2.9.3, coverlet.collector and the API project.
- Add the package `FluentAssertions` with version `7.*` to the test project if it is missing. Never use version 8 or newer (different licence).
- No mocking library (no Moq, no NSubstitute) and no `Microsoft.Extensions.TimeProvider.Testing`.
- `Result<T>` and `Error` (with `ErrorType`: `Validation`, `NotFound`, `Conflict`) are in `Domain/Common/`. Assert on `result.IsSuccess`, `result.IsFailure`, `result.Error.Type` and `result.Error.Code`. `Result<T>.Value` is of type `T` (not nullable) and throws when the result is a failure: read it only after asserting `IsSuccess`, never use the `!` operator on it and never read it in a failure test (assert on `Error` instead).
- Handlers are `sealed class` types with constructor injection, namespace `CampusMarket.Api.Features.<Resource>.<UseCase>`. They expose `Task<Result<T>> HandleAsync(request, CancellationToken ct)`.
- Command handlers take the repository interface, `IUnitOfWork` and sometimes `TimeProvider`. Query handlers take only the repository interface.
- Repository interfaces (`IListingRepository`, `IMemberRepository`): `AddAsync` returns `Task<bool>` (false = duplicate Id), `GetByIdAsync` returns `null` when missing, `GetAllAsync` returns a snapshot, `Update` and `Delete` take the entity. Open the interface files and implement exactly their signatures in the fakes.
- `IUnitOfWork.CommitAsync(CancellationToken ct)` returns `Task<int>`.
- The repositories behind `IListingRepository` and `IMemberRepository` are now EF Core implementations. Unit tests never use EF Core or SQLite: handlers are always tested with the hand-written fakes.

## Folder layout (mirrors production)
```
tests/CampusMarket.UnitTests/
  Domain/Listings/ListingTests.cs, MoneyTests.cs, ReservationTests.cs
  Domain/Members/MemberTests.cs, EmailTests.cs
  Domain/Common/ResultTests.cs, ErrorTests.cs
  Features/Listings/CreateListing/CreateListingHandlerTests.cs   (one file per handler, 10 in total)
  Features/Members/CreateMember/CreateMemberHandlerTests.cs
  Fakes/FakeListingRepository.cs, FakeMemberRepository.cs, FakeUnitOfWork.cs, FixedTimeProvider.cs
```
The old week 2 tests in `Domain/` (`ListingTests`, `MemberReservationTests`, `MemberTests`, `ResultTests`) are replaced by the generated ones: delete them, never edit them. The old `Api/` and `Infrastructure/` test folders no longer exist: never recreate them (HTTP tests belong to the integration-testing skill).

## Naming
- Class: `<ClassUnderTest>Tests`
- Method: `Given_<state>_When_<action>_Then_<expected>`
  Example: `Given_ActiveListing_When_SellerReservesOwnListing_Then_ReturnsValidationError`

## Fakes (hand-written, in `Fakes/`)
- `FakeListingRepository` and `FakeMemberRepository`: implement the real interface over a `Dictionary<Guid, T>`. Expose the stored items (for example `Items`) so tests can assert on them. Add a public property `RejectAdd` (bool, default false): when true, `AddAsync` returns `false` without storing. This is the only way to reach the `Conflict` branch, because the domain generates the Id itself.
- `FakeUnitOfWork`: implements `IUnitOfWork`, counts calls in `SaveCount`, returns `Task.FromResult(0)`.
- `FixedTimeProvider`: `class FixedTimeProvider(DateTimeOffset now) : TimeProvider` overriding `GetUtcNow()`. Never use `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.UtcNow` in tests.
- Fakes contain no assertions and no business logic.

## Rules
1. Arrange, Act, Assert, with one blank line between the blocks. No "// Arrange" comments. Copy the layout of the examples exactly.
2. One behaviour per test.
3. No logic inside tests: no `if`, no loops, no `try/catch`.
4. Use `[Theory]` with `[InlineData]` for input variants (invalid titles, invalid prices, invalid emails, undefined enum values).
5. Cover every success path AND every failure branch of every method that returns a `Result`: check `IsFailure`, `Error.Type` and `Error.Code`. Use the coverage report to find what is still missing.
6. Handler tests, per handler:
- Create: success (item stored, `SaveCount` 1), invalid data (each domain failure, `SaveCount` 0, nothing stored), duplicate Id with `RejectAdd = true` (`ErrorType.Conflict`, `SaveCount` 0).
- Update: success (entity changed, `SaveCount` 1), id not found (`NotFound`, `SaveCount` 0), invalid data (`Validation`, `SaveCount` 0).
- `CreateMemberHandler` also: email already used by another stored member → `ErrorType.Conflict`, code `Member.EmailTaken`, `SaveCount` 0, nothing new stored. Seed the other member in the fake first.
- `UpdateMemberHandler` also: new email already used by another member → `ErrorType.Conflict`, code `Member.EmailTaken`, `SaveCount` 0, member unchanged; keeping the member's own email → success, `SaveCount` 1; and an invalid new email → `Validation` (so both sides of the email check are covered).
- Delete: success (item removed, `SaveCount` 1), id not found (`NotFound`, `SaveCount` 0).
- GetById: found (response matches the entity), not found (`NotFound`).
- GetAll: empty list and list with items. Query handlers have no unit of work: assert that the repository content is unchanged after the call.
7. Tests never depend on each other or on execution order. Every test builds its own fakes. No static mutable state.
8. Assert on behaviour (returned `Result`, state of the object, content of the fake repository), not on implementation details. Private members are tested only through the public API.
9. Every test has at least one assertion.
10. ResultTests must also check both throwing branches: reading `Value` on a failed result throws `InvalidOperationException`, and reading `Error` on a successful result throws `InvalidOperationException`. Use `Action act = () => _ = result.Value;` and `act.Should().Throw<InvalidOperationException>()`. Otherwise those branches stay uncovered.
11. Every run starts in a new Claude Code session, so that the current version of this file is read and not an older context.

- If unit tests already exist, keep them and only add the tests that are missing for the current code. Then generate the coverage report again.

## Coverage
- Collect with the repo file `coverlet.runsettings` (the CI uses it too, format opencover): `dotnet test tests/CampusMarket.UnitTests --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults`
- Generate the HTML report only for the target files: `reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:coverage-report -reporttypes:Html -filefilters:"+*Domain*;+*Features*Handler.cs"`
- Target: 100% line and branch coverage for every file in the report (all of `Domain/` and every handler).
- Commit `coverage-report/` (the HTML report is a deliverable of the assignment). Never commit `TestResults/`.
- After generating, check that Git sees the report: `git status` must list `coverage-report/`, and `git check-ignore -v coverage-report/index.html` must print nothing.
- When coverage is below 100%, list the uncovered lines from the report, add tests for exactly those lines and run again. If the same kind of gap repeats, add a rule to this file instead of fixing the tests by hand.
- Everything that is not in the report is outside the target. This is the single list of exclusions (the README copies it word for word):
  - `Program.cs`: starts the web host, no logic to test (also excluded in the CI).
  - `CampusMarketSetup.cs`: only registers services and routes.
  - `Common/CQRS/`: marker interfaces and reflection-based handler registration, checked by the integration tests.
  - `Infrastructure/`: EF Core DbContext, configurations, repositories and unit of work, no domain logic, covered by the integration tests.
  - `Features/` except `*Handler.cs`: endpoints, requests, responses, commands and queries are HTTP mapping or plain data, covered by the integration tests.

## Never do
- Never use a mocking library.
- Never use `Thread.Sleep` or real time.
- Never put two behaviours in one test.
- Never change production code to make a test pass.
- Never share one object between tests.
- Never guess a signature: read the interface or the class first.
- Never touch production code, `postman/` or the CI workflow.
- Never use the ! operator on Result.Value.

## Example: domain test
```csharp
public class ListingTests
{
    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsDraftListingWithVersionOne()
    {
        var sellerId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        var result = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, createdAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ListingStatus.Draft);
        result.Value.Version.Should().Be(1);
    }
}
```

## Example: handler tests
```csharp
public class CreateListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ValidCommand_When_Handling_Then_StoresListingAndSavesOnce()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().HaveCount(1);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsConflictAndDoesNotSave()
    {
        var repository = new FakeListingRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        unitOfWork.SaveCount.Should().Be(0);
    }
}

```

## Feature: Authentication (Sara)

Production code (written by hand, read it before generating):
- `Domain/Members/Member.cs`: `Member.Register(name, email, passwordHash, createdAt, role = MemberRole.Student)`, `PasswordHash`, `Role`, `IsAdmin`.
- `Domain/Members/IPasswordHasher.cs` and `Common/Auth/ITokenService.cs`.
- `Features/Auth/Register/RegisterHandler.cs` and `Features/Auth/Login/LoginHandler.cs`.

New fakes in `Fakes/` (hand-written style, no mocking library):
- `FakePasswordHasher`: `Hash(p)` returns `"hashed:" + p`, `Verify` compares with the same prefix. Never use the real `AspNetPasswordHasher` in unit tests.
- `FakeTokenService`: returns `"token-{member.Id}"` and a fixed `ExpiresAt`, and remembers `LastMember`.

Tests to add:
- `Domain/Members/MemberTests.cs` (add to the existing file, do not delete anything): Register with valid data returns a Student with the hash; Register with `MemberRole.Admin` returns an Admin; empty or null hash returns `Member.PasswordRequired`; invalid name and invalid email still return validation errors.
- `Features/Auth/Register/RegisterHandlerTests.cs`: valid command stores the member with the hashed password and saves once; weak password (shorter than 8, `[Theory]`) returns `Auth.WeakPassword`; invalid name and invalid email return validation errors; email already used returns `Member.EmailTaken` (Conflict); repository rejects the Id returns Conflict. In every failure case: nothing stored and `CommitAsync` never called.
- `Features/Auth/Login/LoginHandlerTests.cs`: valid credentials return the token and `ExpiresAt` from the fake; email with different casing still works; invalid email format (`[Theory]`), empty password, unknown email, member without password hash and wrong password all return the same `Auth.InvalidCredentials` error.

Rules for this feature:
- Never assert on a real hash or a real JWT. Only the fakes' values.
- The error code is the same for every login failure. Test that it is the same code, so we never leak whether the email exists.
- The password must never appear in the response object.

## Feature: Listing search and paging (Diana)

Production code (written by hand, read it before generating):
- `Features/Listings/GetListings/GetListingsQuery.cs`: optional `Category`, `MinPrice`, `MaxPrice`, `Search`, `Page` (default `DefaultPage` = 1), `PageSize` (default `DefaultPageSize` = 20), and `MaxPageSize` = 100.
- `Features/Listings/GetListings/GetListingsHandler.cs`: validates the query, filters the result of `GetAllAsync` in memory, orders by `CreatedAt` descending then `Id` ascending, then pages.
- `Features/Listings/GetListings/GetListingsResponse.cs`: `Items`, `Count` (items on this page), `TotalCount` (items after the filters, before paging), `Page`, `PageSize`.

File: `Features/Listings/GetListings/GetListingsHandlerTests.cs`. Keep the two existing tests and add the new ones to the same file.

Only `Active` listings are returned. Arrange: build each listing with `Listing.Create(...).Value`, call `listing.Publish()` so it becomes `Active`, then seed it straight into `FakeListingRepository.Items`. Give each listing its own fixed `createdAt` (for example `Now`, `Now.AddHours(1)`, `Now.AddHours(2)`) so the expected order is known.


Tests to add:

- Status: with one `Draft`, one `Active` and one `Reserved` listing seeded (reserve with `listing.Publish()` then `listing.Reserve(buyerId, Now)`), only the `Active` one is returned, and `TotalCount` is 1.
- Validation, each one returns `ErrorType.Validation` and leaves the repository unchanged:
  - `Page` 0 and -1 (`[Theory]`) return `Listings.InvalidPage`.
  - `PageSize` 0, -1 and 101 (`[Theory]`) return `Listings.InvalidPageSize`.
  - `MinPrice` greater than `MaxPrice` returns `Listings.InvalidPriceRange`.
- Limits that are valid: `PageSize` 1 and 100 (`[Theory]`) succeed; `MinPrice` equal to `MaxPrice` succeeds; only `MinPrice` or only `MaxPrice` set succeeds (no range error when one side is null).
- Filters, each one alone, checking exactly which ids come back:
  - `Category` returns only listings of that category.
  - `MinPrice` is inclusive: a listing priced exactly at `MinPrice` is returned, one below it is not.
  - `MaxPrice` is inclusive: a listing priced exactly at `MaxPrice` is returned, one above it is not.
  - `Search` matches part of the title and ignores case (`"oop"` finds `"OOP textbook"`).
  - `Search` with spaces around the text (`"  oop  "`) still matches.
  - `Search` that is empty or only spaces (`[Theory]` with `""` and `"   "`) does not filter: every listing comes back.
  - `Search` that matches nothing returns an empty `Items`, `Count` 0, `TotalCount` 0.
- Two filters together (`Category` and `Search`) return only the listings that match both.
- Order: newest first (`CreatedAt` descending). Two listings with the same `CreatedAt` come back ordered by `Id` ascending (check with `BeInAscendingOrder`).
- Paging, with 3 seeded listings:
  - page 1, size 2: 2 items (the two newest), `Count` 2, `TotalCount` 3, `Page` 1, `PageSize` 2.
  - page 2, size 2: 1 item (the oldest), `Count` 1, `TotalCount` 3.
  - page 5, size 2: empty `Items`, `Count` 0, `TotalCount` 3.
  - default query (no paging values): `Page` 1 and `PageSize` 20 in the response.
- `TotalCount` counts after the filters: with a filter and paging together, `TotalCount` is the number of matching listings, not all listings.

Rules for this feature:
- The handler is a query handler: no `IUnitOfWork`. In every test, assert that the repository still has the same items after the call.
- Never use real time; every `createdAt` is a fixed `DateTimeOffset`.


## Feature: Groundwork (Carina)

New code in the groundwork PR: `Error.Forbidden`, `ErrorType.Forbidden` and `CurrentUserExtensions`. Nothing else changed in behaviour (`SaveChangesAsync` was renamed to `CommitAsync`; the existing tests already cover it).

Files to read: `src/CampusMarket.Api/Domain/Common/Error.cs`, `src/CampusMarket.Api/Domain/Common/ErrorType.cs`, `src/CampusMarket.Api/Common/Auth/CurrentUserExtensions.cs`.

Files:
- `tests/CampusMarket.UnitTests/Domain/Common/ErrorTests.cs`: keep the existing tests and add the one below to the same class.
- `tests/CampusMarket.UnitTests/Common/Auth/CurrentUserExtensionsTests.cs`: new file, same folder structure as `src/`.

Tests to add:
- `Error.Forbidden("Listing.NotOwner", "Not allowed")` → `Code` is `"Listing.NotOwner"`, `Message` is `"Not allowed"`, `Type` is `ErrorType.Forbidden`.
- `GetMemberId` with a `sub` claim set to the fixed id `11111111-1111-1111-1111-111111111111` → returns that `Guid`.
- `IsAdmin` with the claim `role` = `Admin` → `true`.
- `IsAdmin` with the claim `role` = `Student` → `false`.
- `IsAdmin` with no `role` claim → `false`.

Arrange:
- Build the user with `new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))`. The authentication type must be set, otherwise the identity counts as not authenticated.
- Claim names are exactly `sub` and `role` (the API reads tokens with `MapInboundClaims = false`).
- No mocks, no `HttpContext`.

Rules for this feature:
- The coverage of `Domain/` stays 100% for lines and branches, so `Error.Forbidden` must be called by a test.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Ownership (Anca)

Production code (written by hand, read it before generating):
- `Features/Listings/UpdateListing/UpdateListingCommand.cs` and `Features/Listings/DeleteListing/DeleteListingCommand.cs` now have `RequesterId` and `IsAdmin` right after `Id`. The handlers return `Error.Forbidden("Listing.NotOwner", ...)` when `listing.SellerId != RequesterId` and `IsAdmin` is false.
- `Features/Members/UpdateMember/UpdateMemberCommand.cs` and `Features/Members/DeleteMember/DeleteMemberCommand.cs`: the same two fields; the handlers return `Error.Forbidden("Member.NotOwner", ...)` when `member.Id != RequesterId` and `IsAdmin` is false.
- The order in all four handlers: 404 when the entity does not exist, then 403, then the other checks (validation, `Member.EmailTaken`).
- `Domain/Listings/Reservation.cs`: `Cancel()` is now `internal`. The test project sees it through `InternalsVisibleTo` in `CampusMarket.Api.csproj`, so `ReservationTests` keeps calling it directly.
- `CreateListingCommand` did not change (the endpoint fills `SellerId` from the token), so `CreateListingHandlerTests` stays as it is.

Update the existing tests (do not delete any):
- `UpdateListingHandlerTests`, `DeleteListingHandlerTests`, `UpdateMemberHandlerTests`, `DeleteMemberHandlerTests`: build every command with the new fields. In the existing tests the requester is the owner (`listing.SellerId` or `member.Id`) and `IsAdmin` is false, so they keep testing what they tested before. For an unknown id use any `Guid` as requester.
- `Domain/Listings/ListingTests.cs`: rename `Given_ReservedListing_When_CancellingAlreadyCancelledReservation_Then_ReturnsValidationError` to `Given_ListingWithCancelledReservation_When_CancellingItAgain_Then_ReturnsValidationError`. The Arrange cancels the reservation first, so the listing is `Active`, not `Reserved`, when the Act runs. Change only the name.

Tests to add, in the same four handler test files:
- another member, not Admin → `ErrorType.Forbidden` with `Listing.NotOwner` (listings) or `Member.NotOwner` (members); the entity is unchanged (same values, same `Version` for a listing), it is still in the repository, and `SaveCount` is 0.
- an Admin who is not the owner → success, the change is made (updated, or removed for delete), `SaveCount` 1.
- unknown id, requester is another member and not Admin → `NotFound`, not `Forbidden` (404 comes before 403).
- `UpdateMemberHandlerTests` only: another member, not Admin, sending an email that a third member already uses → `Member.NotOwner`, not `Member.EmailTaken` (403 comes before the email check).

Rules for this feature:
- Use fixed, named ids for the people in a test (`OwnerId`, `OtherMemberId`, `AdminId`) so it is clear who is calling.
- `ErrorType.Forbidden` exists in `Domain/Common/ErrorType.cs`; never assert on a status code in a unit test, only on `ErrorType` and the error code.
