---
name: unit-testing
description: Generates xUnit unit tests with FluentAssertions 7.* for the domain and for the CQRS handlers of CampusMarket, with hand-written fakes. Use when adding or changing domain classes or handlers, or when coverage of Domain/ or of the handlers is below 100%.
---

# Unit testing skill for CampusMarket

## Goal
Generate unit tests for `src/CampusMarket.Api/Domain/` and for every handler in `Features/`, reaching 100% line and branch coverage, with assertions that check behaviour. Never edit production code to make a test pass.

## Project facts (read the real files, do not guess)
- Test project: `tests/CampusMarket.UnitTests` (net11.0). It already references xUnit 2.9.3, coverlet.collector and the API project.
- Add the package `FluentAssertions` with version `7.*` to the test project if it is missing. Never use version 8 or newer (different licence).
- No mocking library (no Moq, no NSubstitute) and no `Microsoft.Extensions.TimeProvider.Testing`.
- `Result<T>` and `Error` (with `ErrorType`: `Validation`, `NotFound`, `Conflict`) are in `Domain/Common/`. Assert on `result.IsSuccess`, `result.IsFailure`, `result.Error.Type` and `result.Error.Code`. `Result<T>.Value` is of type `T` (not nullable) and throws when the result is a failure: read it only after asserting `IsSuccess`, never use the `!` operator on it and never read it in a failure test (assert on `Error` instead).
- Handlers are `sealed class` types with constructor injection, namespace `CampusMarket.Api.Features.<Resource>.<UseCase>`. They expose `Task<Result<T>> HandleAsync(request, CancellationToken ct)`.
- Command handlers take the repository interface, `IUnitOfWork` and sometimes `TimeProvider`. Query handlers take only the repository interface.
- Repository interfaces (`IListingRepository`, `IMemberRepository`): `AddAsync` returns `Task<bool>` (false = duplicate Id), `GetByIdAsync` returns `null` when missing, `GetAllAsync` returns a snapshot, `Update` and `Delete` take the entity. Open the interface files and implement exactly their signatures in the fakes.
- `IUnitOfWork.CommitAsync(CancellationToken ct)` returns `Task<int>`.
- The repositories behind `IListingRepository` and `IMemberRepository` are now EF Core implementations. Unit tests never use EF Core, a database or Docker (the API runs on PostgreSQL, tested by the integration-testing skill with Testcontainers): handlers are always tested with the hand-written fakes.

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
12. Never pass a constant array literal as an argument (CA1861): put it in a `private static readonly` field.

- If unit tests already exist, keep them and only add the tests that are missing for the current code. Then generate the coverage report again.

## Coverage
- Collect with the repo file `coverlet.runsettings` (the CI uses it too, format opencover): `dotnet test tests/CampusMarket.UnitTests --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults`
- Generate the HTML report only for the target files: `reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:coverage-report -reporttypes:Html -filefilters:"+*Domain*;+*Features*Handler.cs;+*MetricsRegistry.cs;+*PriceTextFeatures.cs;+*DemoListings.cs"`
- Target: 100% line and branch coverage for every file in the report (all of `Domain/`, every handler, `MetricsRegistry.cs`, `PriceTextFeatures.cs` and `DemoListings.cs`).
- The report in `coverage-report/` is only for reading locally. It is ignored by `.gitignore`: never commit it. Before committing, `git status` must not list `coverage-report/`. Never commit `TestResults/` either.
- When coverage is below 100%, list the uncovered lines from the report, add tests for exactly those lines and run again. If the same kind of gap repeats, add a rule to this file instead of fixing the tests by hand.
- Everything that is not in the report is outside the target. This is the single list of exclusions (the README copies it word for word):
  - `Program.cs`: starts the web host, no logic to test (also excluded in the CI).
  - `CampusMarketSetup.cs`: only registers services and routes.
  - `Common/CQRS/`: marker interfaces and reflection-based handler registration, checked by the integration tests.
  - `Infrastructure/`: EF Core DbContext, configurations, repositories and unit of work, no domain logic, covered by the integration tests. Exceptions that are in the target (pure logic, no database, HTTP or file access): `Infrastructure/Observability/MetricsRegistry.cs`, `Infrastructure/ML/PriceTextFeatures.cs` and `Infrastructure/Persistence/Seed/DemoListings.cs`, all at 100% lines and branches (for `DemoListings.cs` the unit tests cover what the integration tests cannot reach, see "Feature: Demo seed (Diana)").
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
- `RegisterCommand` now has `ConfirmPassword` after `Password`. Update the existing `RegisterHandlerTests` so every command sends the same value in both (they must keep testing what they tested before). Add: `ConfirmPassword` different from `Password` (also a [Theory] with only the letter case different, `"secret123"` vs `"Secret123"`) → `Validation` `Auth.PasswordsDoNotMatch`, nothing stored, `CommitAsync` never called; a weak password with a different confirmation → `Auth.WeakPassword` (the weak check comes first).
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

## Feature: Reservations (Sara)

Production code (written by hand, read it before generating):
- `Features/Listings/PublishListing/`: `PublishListingCommand(Id, RequesterId, IsAdmin)`. Order in the handler: 404 `Listing.NotFound`, then 403 `Listing.NotOwner` (not the seller and not Admin), then the domain error from `listing.Publish()` (`Listing.NotDraft`), then `Update` and one `CommitAsync`.
- `Features/Listings/ReserveListing/`: `ReserveListingCommand(ListingId, BuyerId)`, handler with `IListingRepository`, `IMemberRepository`, `IUnitOfWork`, `TimeProvider`. Order: 404 `Listing.NotFound`, 404 `Member.NotFound` (the buyer does not exist), `Reservation.LimitReached` (Validation) when the buyer already has `MaxActiveReservations` (3) reservations with status `Active` on any listing, then the domain errors from `listing.Reserve` (`Listing.NotActive`, `Reservation.OwnListing`), then one `CommitAsync`. It does **not** call `repository.Update` (see the comment in the handler). The response is built from the new reservation.
- `Features/Listings/CancelReservation/`: `CancelReservationCommand(ListingId, ReservationId, RequesterId, IsAdmin)`. Order: 404 `Listing.NotFound`, 404 `Reservation.NotFound` (no reservation with that id on the listing), 403 `Reservation.NotAllowed` (the requester is not the buyer, not the seller and not Admin), then the domain error from `listing.CancelReservation` (`Reservation.NotActive`), then `Update` and one `CommitAsync`.
- `Features/Members/GetMyReservations/`: query handler with `IListingRepository` and `TimeProvider`, no `IUnitOfWork`. It returns the reservations of `MemberId` from every listing, with the listing's title and price, newest `CreatedAt` first (then `ReservationId` ascending). A reservation that `IsExpired(now)` is shown with status `Expired`; nothing is saved.

Files (same folders as `src/`):
- `Features/Listings/PublishListing/PublishListingHandlerTests.cs`
- `Features/Listings/ReserveListing/ReserveListingHandlerTests.cs`
- `Features/Listings/CancelReservation/CancelReservationHandlerTests.cs`
- `Features/Members/GetMyReservations/GetMyReservationsHandlerTests.cs`

Arrange:
- Use `FakeListingRepository`, `FakeMemberRepository`, `FakeUnitOfWork` and `FixedTimeProvider` with a fixed `Now`. Fixed, named ids: `SellerId`, `BuyerId`, `OtherMemberId`, `AdminId`.
- An `Active` listing: `Listing.Create(...).Value` then `listing.Publish()`. A reserved listing: then `listing.Reserve(BuyerId, Now)`. The buyer exists when `FakeMemberRepository.Items` has a member with that id: create it with `Member.Create(...).Value` and use its `Id` as `BuyerId`.

Tests to add:
- PublishListing: the seller publishes a draft → success, status `Active`, `Version` 2, `SaveCount` 1; an Admin who is not the seller → success; another member → `Forbidden` `Listing.NotOwner`, status still `Draft`, `SaveCount` 0; unknown id → `NotFound`; an `Active` listing → `Validation` `Listing.NotDraft`, `SaveCount` 0.
- ReserveListing: success → the response has the buyer, the listing id, status `Active`, `CreatedAt` = `Now`, `ExpiresAt` = `Now` + 48 h; the listing is `Reserved` and has one reservation; `SaveCount` 1. Then: unknown listing → `Listing.NotFound`; unknown buyer → `Member.NotFound`; a `Draft` listing → `Listing.NotActive`; the seller reserves their own listing → `Reservation.OwnListing`; the buyer already has 3 active reservations on 3 other listings → `Reservation.LimitReached`; the buyer has 2 active and 1 cancelled reservation → success (cancelled ones do not count). Every failure: `SaveCount` 0 and the listing unchanged.
- CancelReservation: the buyer cancels → success, reservation `Cancelled`, listing `Active`, `SaveCount` 1; the seller cancels → success; an Admin cancels → success; another member → `Forbidden` `Reservation.NotAllowed`, reservation still `Active`, `SaveCount` 0; unknown listing → `Listing.NotFound`; unknown reservation id → `Reservation.NotFound`; a reservation already cancelled → `Validation` `Reservation.NotActive`, `SaveCount` 0.
- GetMyReservations: a member with no reservations → empty `Items`, `Count` 0; two reservations on two listings → both, newest first, with the right title and price; a reservation of another member is not returned; a cancelled reservation is returned with status `Cancelled`; a reservation created more than 48 h before `Now` → status `Expired`, and the reservation in the repository is still `Active`.

Rules for this feature:
- Never assert on a status code in a unit test, only on `ErrorType` and the error code.
- Never use real time: `Now` is a fixed `DateTimeOffset`, given to `FixedTimeProvider`.

## Feature: Surviving mutants (Diana)

Stryker.NET left mutants alive in `Domain/` and the handlers. The goal is a mutation score of 100%: add or extend unit tests so that each mutant below makes at least one test fail. Production code does not change, except the `// Stryker disable once` comments for equivalent mutants (point 6 and *"Feature: Full coverage (Carina)"*).

Read the production code of every file named below before writing a test, and use the exact texts and values from it.

### 1. Error messages (String mutations, about 47 mutants)
Stryker replaces error messages with `""`. Today the tests check only `Error.Code` and `Error.Type`.
- In every existing test that asserts on `result.Error.Code`, add one assertion on `result.Error.Message` with the exact text from the production code. For interpolated messages (`$"... {id} ..."`), build the expected text from the same values the test uses. This is the only change allowed in existing tests; do not change anything else in them.
- If an error in `Domain/` or in a handler has no test that reaches it yet, add one.
- `ResultTests`: the two tests that expect `InvalidOperationException` also check the message with `.WithMessage(...)`, using the exact texts from `Result.cs`.

### 2. `repository.Update(...)` calls (Statement mutations, 4 mutants)
`UpdateListingHandler`, `UpdateMemberHandler`, `PublishListingHandler` and `CancelReservationHandler` call `repository.Update(...)`. The fakes store the same object, so a missing call is not visible.
- `FakeListingRepository` and `FakeMemberRepository` get a public `int UpdateCount`, incremented in `Update`.
- In the success test of each of the four handlers, assert `UpdateCount` is 1.

### 3. Boundaries (Equality mutations, 3 mutants)
- `Listing.Create` with a title of exactly `Listing.MaxTitleLength` (100) characters succeeds.
- `Member.Create` with a name of exactly 2 characters succeeds.
- `Member.Create` with a name of exactly 100 characters succeeds.

### 4. Login early return (Logical mutation, 1 mutant)
`LoginHandler` returns early when the email is invalid **or** the password is empty.
- `FakePasswordHasher` gets a public `int VerifyCount`, incremented in `Verify`.
- New test: a stored member with a password, a login with that member's valid email and an **empty** password returns `Auth.InvalidCredentials` and `VerifyCount` is 0. (With `&&` instead of `||`, the handler would reach `Verify`.)

### 5. Order of my reservations (Linq mutation, 1 mutant)
`GetMyReservationsHandler` orders by `CreatedAt` descending, then by `ReservationId` ascending.
- New test: the same buyer reserves two different active listings at the same fixed time (same `CreatedAt`). The items come back ordered by `ReservationId` ascending (`BeInAscendingOrder(i => i.ReservationId)`).

### 6. Equivalent mutants: no test
`Listing.cs:14` (`Title = string.Empty`) and `Member.cs:8` (`Name = string.Empty`) are always overwritten by the factories, so no test can see the change. They are marked in the code with `// Stryker disable once` by hand. Do not write tests for them. The later equivalent mutants (`Message.cs` `Text = string.Empty`, `Review.cs` `First` vs `FirstOrDefault`, `Photo.cs` `FileName` and `ContentType` defaults, `ExpireReservationsHandler.cs` `&&` vs `||` in the active-reservation predicate, `GetListingsHandler.cs` the `"newest"` fallback of the sort) and the rule for marking them are in *"Feature: Full coverage (Carina)"*.

Rules for this feature:
- Keep every existing test and its name. In existing tests, only add the assertions described in points 1 and 2; everything else is a new test.
- Never change production code, except a `// Stryker disable once <Mutator> : <reason>` comment on a mutant that no test can kill (see *"Feature: Full coverage (Carina)"*).
- After the run, `dotnet test` passes and coverage of `Domain/` and the handlers stays 100%.


## Feature: Code review v5 renames (Carina)

Code review v5 (`REVIEW.md`, "Week 4 – code-review v5", rows 12 and 13) found two test names that do not match what the test does or do not follow `Given_<state>_When_<action>_Then_<expected>`. Change only the names: no new tests, no other change in these files, nothing in `src/`.

- `Domain/Listings/ReservationTests.cs`: rename `Given_CancelledReservation_When_CheckingExpirationAfterExpiry_Then_ReturnsFalse` to `Given_CancelledReservation_When_CheckingExpirationAtExpiry_Then_ReturnsFalse`. The test checks `Now.AddHours(48)`, which is exactly the expiry moment, not after it. Keep `Now.AddHours(48)`: mutation testing needs the exact boundary.
- `Domain/Listings/ListingTests.cs`: rename `Given_ActiveListing_When_BuyerIdIsEmpty_Then_ReturnsValidationError` to `Given_ActiveListing_When_ReservingWithEmptyBuyerId_Then_ReturnsValidationError`. The `When` part must be the action (`Reserve`), not a state.

Rules for this feature:
- Only names change, so the coverage does not change: do not generate the coverage report again for this feature.
- After the run, `dotnet test` passes with the same number of unit tests as before.
## Feature: Price suggestion (Sara)

Production code (read it before generating):
- `Common/Pricing/IPriceSuggester.cs`: `int Suggest(string title, string? description, Category category, Condition condition)`. The real implementation (`Infrastructure/ML/PriceSuggester.cs`) uses the ML model; unit tests never use it.
- `Features/Listings/SuggestPrice/SuggestPriceQuery.cs` and `SuggestPriceHandler.cs`: a query handler (no `IUnitOfWork`). Validation order: unknown category `Listing.CategoryInvalid`, unknown condition `Listing.ConditionInvalid`, description longer than `Listing.MaxDescriptionLength` after trim `Listing.DescriptionTooLong`, empty title `Listing.TitleEmpty`, title longer than `Listing.MaxTitleLength` after trim `Listing.TitleTooLong`. Then it calls the suggester once with the **trimmed** title and returns `SuggestPriceResponse` with the suggester's value.

New fake in `Fakes/` (hand-written, no mocking library):
- `FakePriceSuggester : IPriceSuggester`: returns the public property `SuggestedPrice` (default `123`), counts calls in `CallCount`, and remembers `LastTitle`, `LastDescription`, `LastCategory`, `LastCondition`.

File: `Features/Listings/SuggestPrice/SuggestPriceHandlerTests.cs`.

Tests to add:
- Valid query (title `"  Casio fx-991EX  "`, a description, `Electronics`, `Good`) → success, `SuggestedPrice` equals the fake's value; `CallCount` 1; `LastTitle` is `"Casio fx-991EX"` (trimmed); description, category and condition are passed unchanged.
- Description `null` → success, `LastDescription` is `null`.
- Invalid category (`(Category)99`) → `Listing.CategoryInvalid`; invalid condition (`(Condition)99`) → `Listing.ConditionInvalid`.
- Description of 1001 characters → `Listing.DescriptionTooLong`; exactly 1000 → success; 1000 characters with spaces around → success (the length is checked after trim).
- Title `""` and `"   "` (`[Theory]`) → `Listing.TitleEmpty`; 101 characters → `Listing.TitleTooLong`; exactly 100 → success; 100 characters with spaces around → success.
- Order of the checks: invalid category **and** empty title → `Listing.CategoryInvalid`; description too long **and** empty title → `Listing.DescriptionTooLong`.

Rules for this feature:
- In every failure test: `ErrorType.Validation`, the exact `Error.Code` **and** the exact `Error.Message` from the handler, and `CallCount` 0 (the model is never asked when the input is invalid).
- Build the expected messages for the length errors from `Listing.MaxDescriptionLength` and `Listing.MaxTitleLength`, the same way the handler does.
- Never use the real `PriceSuggester` or a `.zip` model in unit tests.

## Feature: Confirm sale (Sara)

Production code (read it before generating):
- `Domain/Listings/Reservation.cs`: `internal Complete()`: only an `Active` reservation becomes `Completed`, otherwise `Reservation.NotActive`. Tests may call it directly (`InternalsVisibleTo`).
- `Domain/Listings/Listing.cs`: `ConfirmSale(reservationId, currentTime)`: unknown reservation → NotFound `Reservation.NotFound`; not `Active` → `Reservation.NotActive`; `IsExpired(currentTime)` → `Reservation.Expired`; otherwise the reservation is `Completed`, the listing `Sold` and `Version` + 1. `UpdateDetails` on a `Sold` listing → `Listing.Sold` (checked before the other details).
- `Features/Listings/ConfirmSale/`: `ConfirmSaleCommand(ListingId, ReservationId, RequesterId, IsAdmin)`; order in the handler: 404 `Listing.NotFound`, 403 `Listing.NotOwner` (not the seller and not Admin; the buyer cannot confirm), the domain errors, then `Update` and one `CommitAsync`.

Files: `Domain/Listings/ReservationTests.cs` and `ListingTests.cs` (add to them), `Features/Listings/ConfirmSale/ConfirmSaleHandlerTests.cs` (new).

Arrange: a reserved listing is `Listing.Create(...).Value`, `Publish()`, then `Reserve(BuyerId, Now)`. Fixed, named ids: `SellerId`, `BuyerId`, `OtherMemberId`, `AdminId`. The reservation expires at `Now` + 48 h.

Tests to add:
- `Reservation.Complete()`: an active reservation → `Completed`; a cancelled one → `Reservation.NotActive`, status unchanged.
- `Listing.ConfirmSale`: at `Now` + 1 h → success, listing `Sold`, `Version` + 1, reservation `Completed`; unknown reservation id → `Reservation.NotFound`; a cancelled reservation → `Reservation.NotActive`; confirming twice → `Reservation.NotActive`; at exactly `Now` + 48 h → `Reservation.Expired`; at `Now` + 48 h minus 1 second → success. In every failure: status `Reserved` and `Version` unchanged.
- `UpdateDetails` on a sold listing → `Listing.Sold`, details unchanged; with invalid details too (empty title) → still `Listing.Sold` (it is checked first).
- A sold listing cannot be published (`Listing.NotDraft`) or reserved (`Listing.NotActive`).
- Handler: the seller confirms → success, `Sold`, `SaveCount` 1, `UpdateCount` 1; an Admin who is not the seller → success; the buyer → Forbidden `Listing.NotOwner`; another member → Forbidden `Listing.NotOwner`; unknown listing → `Listing.NotFound`; unknown reservation → `Reservation.NotFound`; the clock at `Now` + 48 h → `Reservation.Expired`. Order: unknown listing with another member as requester → `NotFound`; the buyer with an unknown reservation id → `Forbidden` (403 comes before the reservation check). In every failure: `SaveCount` 0, `UpdateCount` 0, listing unchanged.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`.
- Never use real time: `Now` is fixed, the handler gets it through `FixedTimeProvider`.

## Feature: Reservation expiry (Sara)

Production code (read it before generating):
- `Domain/Listings/Reservation.cs`: `internal Expire()`: only an `Active` reservation becomes `Expired`, otherwise `Reservation.NotActive`. Tests may call it directly (`InternalsVisibleTo`).
- `Domain/Listings/Listing.cs`: `ExpireReservations(currentTime)` returns 0 or 1. When the active reservation `IsExpired(currentTime)`: the reservation becomes `Expired`, the listing `Active` again, `Version` + 1, and it returns 1. Otherwise nothing changes and it returns 0.
- `Features/Listings/ExpireReservations/`: `ExpireReservationsCommand()` (a system job, no requester); the handler gets every listing, calls `ExpireReservations(now)`, calls `repository.Update` only for the listings that changed, one `CommitAsync` only when at least one changed, and returns the number expired.

Files: `Domain/Listings/ReservationTests.cs` and `ListingTests.cs` (add to them), `Features/Listings/ExpireReservations/ExpireReservationsHandlerTests.cs` (new).

Arrange: a reserved listing is `Listing.Create(...).Value`, `Publish()`, then `Reserve(BuyerId, Now)`; it expires at `Now` + 48 h. Fixed, named ids: `SellerId`, `BuyerId`, `OtherBuyerId`.

Tests to add:
- `Reservation.Expire()`: an active reservation → `Expired`; a cancelled one → `Reservation.NotActive` with the exact message, status unchanged.
- `Listing.ExpireReservations`: at `Now` + 48 h minus 1 second → 0, listing `Reserved`, `Version` unchanged; at exactly `Now` + 48 h → 1, reservation `Expired`, listing `Active`, `Version` + 1; a cancelled or a completed (sold) reservation → 0, nothing changed; a draft listing with no reservations → 0; calling it twice → the second call returns 0; after expiry another buyer (`OtherBuyerId`) can reserve the listing.
- Handler: no listings → 0, `SaveCount` 0, `UpdateCount` 0; one expired, one still valid and one draft listing → 1, `UpdateCount` 1, `SaveCount` 1, only the expired one changed; two expired → 2, `UpdateCount` 2, `SaveCount` 1 (one commit for all); nothing expired → 0, `SaveCount` 0, `UpdateCount` 0. The clock comes from `FixedTimeProvider`.

Rules for this feature:
- Never use real time: `Now` is fixed.
- Failure tests check `ErrorType`, the exact `Error.Code` and the exact `Error.Message`.

## Feature: Favorites (Anca)

Production code (read it before generating):
- `Domain/Favorites/Favorite.cs`: `Favorite.Create(memberId, listing, createdAt)`: `Guid.Empty` member → `Favorite.MemberEmpty`; the member is the listing's seller → `Favorite.OwnListing`; the listing is `Draft` → `Favorite.ListingNotAvailable`; otherwise a favorite with a new `Id`, the member, `listing.Id` and `createdAt`. `Active`, `Reserved` and `Sold` listings can all be saved.
- `Domain/Favorites/IFavoriteRepository.cs`: `AddAsync` (false = duplicate Id), `GetAsync(memberId, listingId)` (null when the member has not saved that listing), `GetByMemberAsync(memberId)`, `Delete(favorite)`.
- `Features/Favorites/AddFavorite/`: `AddFavoriteCommand(MemberId, ListingId)`. Order in the handler: 404 `Listing.NotFound`, 404 `Member.NotFound`, the domain errors of `Favorite.Create`, 409 `Favorite.AlreadyExists` (the member already saved this listing), 409 `Favorite.Duplicate` (`AddAsync` returned false), then one `CommitAsync`. Returns `AddFavoriteResponse(Id, ListingId, CreatedAt)`.
- `Features/Favorites/RemoveFavorite/`: `RemoveFavoriteCommand(MemberId, ListingId)`. No favorite of this member for this listing → 404 `Favorite.NotFound`; otherwise `Delete` and one `CommitAsync`, returns the listing id.
- `Features/Favorites/GetMyFavorites/`: `GetMyFavoritesQuery(MemberId, Page = 1, PageSize = 20)`, `MaxPageSize` 100. `Page` < 1 → `Favorites.InvalidPage`; `PageSize` < 1 or > 100 → `Favorites.InvalidPageSize`. Only the member's favorites whose listing exists, newest first (`CreatedAt` descending, then `ListingId`), then paged. `Count` = items on the page, `TotalCount` = all matching favorites.

New fake `Fakes/FakeFavoriteRepository.cs`, built like `FakeListingRepository`: implements `IFavoriteRepository` over a `List<Favorite>` exposed as `Items`; `RejectAdd` (bool, default false: `AddAsync` returns false without storing); `DeleteCount` that counts `Delete` calls. `GetAsync` and `GetByMemberAsync` read `Items`; `GetByMemberAsync` returns a snapshot (`ToList()`).

Files: `Domain/Favorites/FavoriteTests.cs`, `Features/Favorites/AddFavorite/AddFavoriteHandlerTests.cs`, `Features/Favorites/RemoveFavorite/RemoveFavoriteHandlerTests.cs`, `Features/Favorites/GetMyFavorites/GetMyFavoritesHandlerTests.cs` (all new).

Arrange: a published listing is `Listing.Create(SellerId, ...).Value` then `Publish()`; a reserved one also `Reserve(OtherMemberId, Now)`. A favorite in the fake is `Favorite.Create(memberId, listing, createdAt).Value`, with `createdAt` chosen by the test (for example `Now.AddHours(-2)`) to control the order. For `AddFavorite` the member must exist in `FakeMemberRepository`: create it with `Member.Create(...)` and use its `member.Id`. Fixed, named ids for the rest: `SellerId`, `OtherMemberId`. `Now` is fixed and reaches the handler through `FixedTimeProvider`.

Tests to add:
- `Favorite.Create`: a published listing of someone else → success with a non-empty `Id`, the member id, `listing.Id` and `createdAt`; `Guid.Empty` member → `Favorite.MemberEmpty`; the seller → `Favorite.OwnListing`; a draft → `Favorite.ListingNotAvailable`; a reserved listing → success; a sold listing (published, reserved, `ConfirmSale`) → success. Two calls give two different `Id`s.
- `AddFavoriteHandler`: success → the response has the favorite's `Id`, the listing id and `Now`, one favorite in the fake with this member and listing, `SaveCount` 1; unknown listing → NotFound `Listing.NotFound`; unknown member → NotFound `Member.NotFound`; unknown listing and unknown member together → `Listing.NotFound` (the listing is checked first); the seller → `Favorite.OwnListing`; a draft → `Favorite.ListingNotAvailable`; the member already saved this listing → Conflict `Favorite.AlreadyExists`, still exactly one favorite; the same member saving another listing → success (two favorites); `RejectAdd = true` → Conflict `Favorite.Duplicate`. In every failure `SaveCount` 0 and nothing new in the fake.
- `RemoveFavoriteHandler`: success → returns the listing id, the favorite is gone from the fake, `DeleteCount` 1, `SaveCount` 1; no favorite for this listing → NotFound `Favorite.NotFound`; another member's favorite for the same listing → NotFound `Favorite.NotFound` and that favorite is still in the fake. In every failure `DeleteCount` 0 and `SaveCount` 0.
- `GetMyFavoritesHandler`: no favorites → empty `Items`, `Count` 0, `TotalCount` 0; two favorites of the member → newest first, each item has the listing's id, title, category, condition, price (`Price.Amount`), status and the favorite's `CreatedAt` as `FavoritedAt`; two favorites with the same `CreatedAt` → ordered by `ListingId`; another member's favorites are not returned; a favorite whose listing is not in `FakeListingRepository` is not returned and not counted; three favorites with `Page` 2 and `PageSize` 2 → one item (the oldest), `Count` 1, `TotalCount` 3, `Page` 2, `PageSize` 2; `Page` 1 and `PageSize` 1 → the newest only; `PageSize` 100 → success; `Page` 0 → `Favorites.InvalidPage`; `PageSize` 0 and 101 → `Favorites.InvalidPageSize`; the default query (only `MemberId`) → `Page` 1, `PageSize` 20. The fakes are unchanged after every call.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`.
- Never use real time: `Now` is fixed.

## Feature: Refresh token and logout (Carina)

Production code (written by hand, read it before generating):
- `Domain/Members/RefreshToken.cs`: `RefreshToken.Create(memberId, token, now)` stores only `Hash(token)` (SHA-256, 64 upper-case hex characters), never the token itself; `ExpiresAt = now + Lifetime` (7 days). `IsActive(now)` is true when `RevokedAt` is null and `now < ExpiresAt`. `Revoke(now)` sets `RevokedAt` only the first time. `RefreshToken.Invalid` is a `Validation` error with code `Auth.InvalidRefreshToken` and message `The refresh token is not valid.`
- `Domain/Members/IRefreshTokenRepository.cs`: `Add`, `GetByHashAsync`, `Update`.
- `Common/Auth/ITokenService.cs`: new method `CreateRefreshToken()`.
- `Features/Auth/Login/LoginHandler.cs`: the constructor now also takes `IRefreshTokenRepository`, `IUnitOfWork` and `TimeProvider`, after the three old parameters. On success it creates one refresh token, adds it and calls `CommitAsync` once. `LoginResponse` has a new field `RefreshToken`.
- `Features/Auth/Refresh/RefreshHandler.cs`, in this order: empty or whitespace token → `Invalid`; no token with that hash → `Invalid`; token not active (revoked or expired) → `Invalid`; member not found → `Invalid`; otherwise it revokes the old token, calls `Update`, adds a new token for the same member, creates the access token and calls `CommitAsync` once.
- `Features/Auth/Logout/LogoutHandler.cs`: empty token, unknown token or token not active → `Invalid`; otherwise `Revoke(now)`, `Update`, one `CommitAsync`, and it returns the token's `Id`.

Fakes (hand-written, in `Fakes/`):
- `FakeTokenService`: add `CreateRefreshToken()`, which returns `"refresh-1"`, `"refresh-2"`, ... from a counter, and a public `RefreshTokenCount`. Do not change `CreateToken`.
- New `FakeRefreshTokenRepository`: `Items` (`Dictionary<Guid, RefreshToken>` by `Id`); `Add` stores; `GetByHashAsync` returns the item with the same `TokenHash`, or null; `Update` stores and increments a public `UpdateCount`.

Update the existing tests (do not delete any):
- `Features/Auth/Login/LoginHandlerTests.cs`: every `new LoginHandler(...)` also gets `new FakeRefreshTokenRepository()`, `new FakeUnitOfWork()` and `new FixedTimeProvider(Now)` at the end, so the tests keep testing what they tested before.

Tests to add:
- `Domain/Members/RefreshTokenTests.cs` (new):
  - `Create` → `MemberId` and `CreatedAt` as given, `ExpiresAt` = `Now.AddDays(7)`, `RevokedAt` null, `Id` not empty, `TokenHash` = `RefreshToken.Hash(token)` and different from the token.
  - `Hash`: the same text gives the same hash; two different texts give different hashes; the hash matches `^[0-9A-F]{64}$`.
  - `IsActive`: true one second before `ExpiresAt`; false exactly at `ExpiresAt`; false after `Revoke`.
  - `Revoke` called twice: `RevokedAt` keeps the first time.
  - `RefreshToken.Invalid`: code, message and type.
- `LoginHandlerTests` (add to the same file):
  - valid credentials → `RefreshToken` in the response is `"refresh-1"`; the repository has exactly one token, with `MemberId` = the member's id, `TokenHash` = `RefreshToken.Hash("refresh-1")`, `CreatedAt` = `Now`, `ExpiresAt` = `Now.AddDays(7)`; `SaveCount` 1.
  - wrong password → no token stored, `RefreshTokenCount` 0, `SaveCount` 0.
- `Features/Auth/Refresh/RefreshHandlerTests.cs` (new):
  - active token → success: `Token` = `"token-{member.Id}"`, `ExpiresAt` = `FakeTokenService.ExpiresAt`, `RefreshToken` = `"refresh-1"`; the old token has `RevokedAt` = `Now`; the repository has 2 tokens, and the new one belongs to the same member, has `TokenHash` = `RefreshToken.Hash("refresh-1")` and is active; `UpdateCount` 1; `SaveCount` 1.
  - `""` and `"   "` (`[Theory]`) → `Invalid`, `SaveCount` 0.
  - unknown token → `Invalid`, repository unchanged, `SaveCount` 0.
  - revoked token → `Invalid`, no new token, `SaveCount` 0.
  - a token created exactly 7 days before `Now` → `Invalid` (it expires at that moment); a token created 7 days minus 1 second before `Now` → success.
  - a token of a member that is not in `FakeMemberRepository` → `Invalid`, the token is not revoked, `SaveCount` 0.
  - rotation: the Arrange uses the token once with the handler (success); the Act uses the same token again with the same handler and fakes → `Invalid`.
- `Features/Auth/Logout/LogoutHandlerTests.cs` (new):
  - active token → success, returns the token's `Id`, `RevokedAt` = `Now`, `UpdateCount` 1, `SaveCount` 1.
  - `""` and `"   "` (`[Theory]`) → `Invalid`, `SaveCount` 0.
  - unknown token → `Invalid`, `SaveCount` 0.
  - token already revoked → `Invalid`, `RevokedAt` unchanged, `SaveCount` 0.
  - expired token → `Invalid`, `SaveCount` 0.

Rules for this feature:
- Arrange: create tokens with `RefreshToken.Create(...)` and put them straight into `FakeRefreshTokenRepository.Items`. Never call `LoginHandler` to get a token for the refresh or logout tests.
- Every error assertion checks `Type`, `Code` and `Message`.
- Never use real time: `Now` is a fixed `DateTimeOffset`, given to `FixedTimeProvider`.
- Then run the tests with coverage and generate the report as this skill says: `Domain/` and the handlers stay at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Category suggestion (Anca)

Production code (read it before generating):
- `Common/Categories/ICategorySuggester.cs`: `CategorySuggestion Suggest(string title, string? description)`, where `CategorySuggestion(Category Category, double Confidence)`. The real implementation (`Infrastructure/ML/CategorySuggester.cs`) uses the ML model; unit tests never use it.
- `Features/Listings/SuggestCategory/SuggestCategoryQuery.cs` and `SuggestCategoryHandler.cs`: a query handler (no `IUnitOfWork`). Validation order: description longer than `Listing.MaxDescriptionLength` after trim `Listing.DescriptionTooLong`, empty title `Listing.TitleEmpty`, title longer than `Listing.MaxTitleLength` after trim `Listing.TitleTooLong` (the same codes and messages as `SuggestPrice`). Then it calls the suggester once with the **trimmed** title and the description unchanged, and returns `SuggestCategoryResponse(Category, Confidence)` with the suggester's values.

New fake in `Fakes/` (hand-written, no mocking library):
- `FakeCategorySuggester : ICategorySuggester`: returns the public property `Suggestion` (default `new CategorySuggestion(Category.Electronics, 0.87)`), counts calls in `CallCount`, and remembers `LastTitle` and `LastDescription`.

File: `Features/Listings/SuggestCategory/SuggestCategoryHandlerTests.cs`.

Tests to add:
- Valid query (title `"  Laptop Dell Latitude  "`, a description) → success, `Category` and `Confidence` equal the fake's `Suggestion`; `CallCount` 1; `LastTitle` is `"Laptop Dell Latitude"` (trimmed); the description is passed unchanged.
- The fake set to `new CategorySuggestion(Category.Notes, 0.5)` → the response has `Notes` and `0.5` (the handler does not pick its own values).
- Description `null` → success, `LastDescription` is `null`.
- Description of 1001 characters → `Listing.DescriptionTooLong`; exactly 1000 → success; 1000 characters with spaces around → success (the length is checked after trim).
- Title `""` and `"   "` (`[Theory]`) → `Listing.TitleEmpty`; 101 characters → `Listing.TitleTooLong`; exactly 100 → success; 100 characters with spaces around → success.
- Order of the checks: description too long **and** empty title → `Listing.DescriptionTooLong`.

Rules for this feature:
- In every failure test: `ErrorType.Validation`, the exact `Error.Code` **and** the exact `Error.Message` from the handler, and `CallCount` 0 (the model is never asked when the input is invalid).
- Build the expected messages for the length errors from `Listing.MaxDescriptionLength` and `Listing.MaxTitleLength`, the same way the handler does.
- Never use the real `CategorySuggester` or a `.zip` model in unit tests.

## Feature: Spam and moderation (Anca)

Production code (read it before generating):
- `Domain/Listings/Listing.cs`, new method `Hide()`: `Hidden` → `Listing.AlreadyHidden`; `Draft` or `Sold` → `Listing.CannotHide`; `Active` → `Hidden`, `Version` +1; `Reserved` → its active reservation becomes `Cancelled`, the listing `Hidden`, `Version` +1.
- `Domain/Reports/Report.cs`: `Report.Create(reporterId, listing, reason, createdAt)` checks, in this order: `Guid.Empty` → `Report.ReporterEmpty`; the reporter is the seller → `Report.OwnListing`; the listing is not `Active` or `Reserved` → `Report.ListingNotAvailable`; reason null, empty or blank → `Report.ReasonEmpty`; more than `Report.MaxReasonLength` (500) characters after trim → `Report.ReasonTooLong`. Otherwise it returns a report with a new `Id`, `listing.Id`, the reporter, the **trimmed** reason, `SpamProbability` null and `createdAt`. `Report.CreateAutomatic(listingId, spamProbability, createdAt)`: `ReporterId` null, `Reason` = `Report.AutomaticReason`, the probability. `IsAutomatic` is true only when `ReporterId` is null.
- `Domain/Reports/IReportRepository.cs`: `AddAsync` (false = duplicate Id), `ExistsAsync(reporterId, listingId)`, `GetAllAsync()`.
- `Domain/Favorites/Favorite.cs`: `Favorite.Create` now also refuses a `Hidden` listing with `Favorite.ListingNotAvailable`.
- `Features/Favorites/GetMyFavorites/GetMyFavoritesHandler.cs`: a favorite whose listing is `Hidden` is not returned and not counted in `TotalCount`.
- `Common/Moderation/ISpamDetector.cs`: `SpamCheck Check(string title, string? description, decimal price)`, with `SpamCheck(bool IsSpam, double Probability)`. The real `SpamDetector` uses the ML model; unit tests never use it.
- `Features/Listings/PublishListing/PublishListingHandler.cs` now takes `(IListingRepository, IUnitOfWork, ISpamDetector, IReportRepository, TimeProvider)`. After a successful `Publish()` it calls the detector **once** with the title, the description and `Price.Amount` of the listing; if `IsSpam` is true it adds `Report.CreateAutomatic(listing.Id, probability, now)`. Then `Update` and one `CommitAsync`, as before. On every failure the detector is not called.
- `Features/Moderation/ReportListing/`: `ReportListingCommand(ReporterId, ListingId, Reason)`. Order: 404 `Listing.NotFound`, 404 `Member.NotFound`, the errors of `Report.Create`, 409 `Report.AlreadyExists` (`ExistsAsync` is true), 409 `Report.Duplicate` (`AddAsync` returned false), then one `CommitAsync`. Returns `ReportListingResponse(Id, ListingId, CreatedAt)`.
- `Features/Moderation/GetReportedListings/`: `GetReportedListingsQuery(Page = 1, PageSize = 20)`, `MaxPageSize` 100. `Page` < 1 → `Moderation.InvalidPage`; `PageSize` < 1 or > 100 → `Moderation.InvalidPageSize`. One item per listing that has reports, is in the listing repository and is not `Hidden`: `ListingId`, `Title`, `SellerId`, `Status`, `ReportCount` (all its reports), `FlaggedAsSpam` (it has an automatic report), `SpamProbability` (of the automatic report, else null), `LastReportedAt` (newest `CreatedAt`), `Reasons` (newest first). Order: `ReportCount` descending, then `LastReportedAt` descending, then `ListingId`. Then paged; `Count` = items on the page, `TotalCount` = all items.
- `Features/Moderation/HideListing/`: `HideListingCommand(ListingId)`. Unknown listing → 404 `Listing.NotFound`; then the errors of `Hide()`; otherwise `Update`, one `CommitAsync`, and it returns the listing id.

New fakes in `Fakes/` (hand-written, no mocking library):
- `FakeSpamDetector : ISpamDetector`: returns the public property `Result` (default `new SpamCheck(false, 0.1)`), counts calls in `CallCount`, and remembers `LastTitle`, `LastDescription` and `LastPrice`.
- `FakeReportRepository : IReportRepository`, built like `FakeFavoriteRepository`: a `List<Report>` exposed as `Items`; `RejectAdd` (bool, default false: `AddAsync` returns false without storing); `ExistsAsync` and `GetAllAsync` read `Items`; `GetAllAsync` returns a snapshot (`ToList()`).

Files: `Domain/Reports/ReportTests.cs` (new); `Domain/Listings/ListingTests.cs` and `Domain/Favorites/FavoriteTests.cs` (add tests); `Features/Moderation/ReportListing/ReportListingHandlerTests.cs`, `Features/Moderation/GetReportedListings/GetReportedListingsHandlerTests.cs`, `Features/Moderation/HideListing/HideListingHandlerTests.cs` (new); `Features/Listings/PublishListing/PublishListingHandlerTests.cs` and `Features/Favorites/GetMyFavorites/GetMyFavoritesHandlerTests.cs` (change and add).

Change to the existing `PublishListingHandlerTests`: every `new PublishListingHandler(repository, unitOfWork)` becomes `new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now))` with a `new FakeSpamDetector()` and a `new FakeReportRepository()`. Do not change what the existing tests assert; only add, in their failure tests, that `CallCount` is 0 and no report was added.

Arrange: a published listing is `Listing.Create(SellerId, ...).Value` then `Publish()`; a reserved one also `Reserve(OtherMemberId, Now)`; a sold one also `ConfirmSale(reservation.Id, Now)`. For `ReportListing` the reporter must exist in `FakeMemberRepository` (`Member.Create(...)`, use its `member.Id`). A user report in the fake is `Report.Create(reporterId, listing, "reason", createdAt).Value`, an automatic one `Report.CreateAutomatic(listing.Id, 0.9, createdAt)`, with `createdAt` chosen by the test (for example `Now.AddHours(-2)`) to control the order. `Now` is fixed.

Tests to add:
- `Listing.Hide`: active → success, `Hidden`, `Version` +1; reserved → `Hidden`, the reservation is `Cancelled`, `Version` +1 (only once); draft → `Listing.CannotHide`; sold → `Listing.CannotHide`; hidden → `Listing.AlreadyHidden`. On failure the status and `Version` do not change.
- `Report.Create`: a valid report (reason `"  Fake photos  "`) → success, new non-empty `Id`, `listing.Id`, the reporter, reason `"Fake photos"`, `SpamProbability` null, `IsAutomatic` false, `createdAt`; a reserved listing → success; each error above with the exact code and message (`Report.ListingNotAvailable` for a draft, a sold and a hidden listing); reason `null`, `""` and `"   "` → `Report.ReasonEmpty`; 500 characters → success; 501 → `Report.ReasonTooLong`; 500 characters with spaces around → success. Order: `Guid.Empty` reporter on a draft → `Report.ReporterEmpty`; the seller with an empty reason → `Report.OwnListing`. `Report.CreateAutomatic`: `ReporterId` null, `IsAutomatic` true, `Reason` equals `Report.AutomaticReason`, the probability and `createdAt`. Two calls give two different `Id`s.
- `Favorite.Create`: a hidden listing → `Favorite.ListingNotAvailable`.
- `GetMyFavoritesHandler`: a favorite of a hidden listing (published, then `Hide()`) is not returned and `TotalCount` does not count it, while a favorite of an active listing is returned.
- `PublishListingHandler`: the detector says not spam → success, `CallCount` 1, `LastTitle`, `LastDescription` and `LastPrice` are those of the listing, no report added, `SaveCount` 1; the detector says spam (`new SpamCheck(true, 0.97)`) → success, the listing is `Active`, exactly one report with `IsAutomatic` true, the listing id, `SpamProbability` 0.97 and `CreatedAt` = `Now`, `SaveCount` 1; unknown listing, not the owner and not a draft → `CallCount` 0 and no report.
- `ReportListingHandler`: success → the response has the `Id` of the report, the listing id and `Now`, one report in the fake with this reporter and the trimmed reason, `SaveCount` 1; unknown listing → NotFound `Listing.NotFound`; unknown member → NotFound `Member.NotFound`; both unknown → `Listing.NotFound`; the seller → `Report.OwnListing`; a draft → `Report.ListingNotAvailable`; empty reason → `Report.ReasonEmpty`; the member already reported this listing → Conflict `Report.AlreadyExists`, still one report; another member reporting the same listing → success (two reports); `RejectAdd = true` → Conflict `Report.Duplicate`. In every failure `SaveCount` 0 and nothing new in the fake.
- `GetReportedListingsHandler`: no reports → empty `Items`, `Count` 0, `TotalCount` 0; one listing with two user reports → `ReportCount` 2, `FlaggedAsSpam` false, `SpamProbability` null, `LastReportedAt` the newest, `Reasons` newest first, and every other field from the listing; a listing with an automatic report → `FlaggedAsSpam` true and its probability; a listing with more reports comes first; same `ReportCount` → newest `LastReportedAt` first; same count and same time → ordered by `ListingId`; the reports of a hidden listing are not returned and not counted; a report whose listing is not in `FakeListingRepository` is not returned and not counted; three reported listings with `Page` 2 and `PageSize` 2 → one item, `Count` 1, `TotalCount` 3; `PageSize` 100 → success; `Page` 0 → `Moderation.InvalidPage`; `PageSize` 0 and 101 → `Moderation.InvalidPageSize`; the default query → `Page` 1, `PageSize` 20. The fakes are unchanged after every call.
- `HideListingHandler`: an active listing → success, returns its id, the listing is `Hidden`, `UpdateCount` 1, `SaveCount` 1; unknown listing → NotFound `Listing.NotFound`; a hidden listing → `Listing.AlreadyHidden`; a draft → `Listing.CannotHide`. In every failure `UpdateCount` 0 and `SaveCount` 0.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`. Build the length message from `Report.MaxReasonLength`. The message of `Report.Duplicate` contains the generated Id: use `.Match("A report with id '*' already exists.")`, as for `Favorite.Duplicate`.
- Never use the real `SpamDetector` or a `.zip` model in unit tests. Never use real time: `Now` is fixed.

## Feature: Seller reviews (Anca)

Production code (read it before generating):
- `Domain/Reviews/Review.cs`: `Review.Create(reviewerId, listing, rating, comment, createdAt)` checks, in this order: `Guid.Empty` → `Review.ReviewerEmpty`; the listing is not `Sold` → `Review.SaleNotCompleted`; the reviewer is not the member of the completed reservation (the buyer) → `Review.NotBuyer` (`ErrorType.Forbidden`, also for the seller); rating below `Review.MinRating` (1) or above `Review.MaxRating` (5) → `Review.InvalidRating`; comment longer than `Review.MaxCommentLength` (500) after trim → `Review.CommentTooLong`. Otherwise it returns a review with a new `Id`, `listing.Id`, `listing.SellerId`, the reviewer, the rating, the **trimmed** comment (null, empty or blank → `null`) and `createdAt`.
- `Domain/Reviews/IReviewRepository.cs`: `AddAsync` (false = duplicate Id), `ExistsForListingAsync(listingId)`, `GetBySellerAsync(sellerId)`.
- `Features/Reviews/AddReview/`: `AddReviewCommand(ReviewerId, ListingId, Rating, Comment)`. Order in the handler: 404 `Listing.NotFound`, 404 `Member.NotFound`, the errors of `Review.Create`, 409 `Review.AlreadyExists` (the listing already has a review), 409 `Review.Duplicate` (`AddAsync` returned false), then one `CommitAsync`. Returns `AddReviewResponse(Id, ListingId, SellerId, Rating, Comment, CreatedAt)`.
- `Features/Reviews/GetMemberReviews/`: `GetMemberReviewsQuery(MemberId, Page = 1, PageSize = 20)`, `MaxPageSize` 100. `Page` < 1 → `Reviews.InvalidPage`; `PageSize` < 1 or > 100 → `Reviews.InvalidPageSize` (both checked before the member); unknown member → 404 `Member.NotFound`. The reviews where the member is the seller, newest first (`CreatedAt` descending, then `Id`), then paged. `AverageRating` = the average of **all** their ratings (not only the page), rounded to two decimals (`MidpointRounding.AwayFromZero`), `null` when there are none; `ReviewCount` = all their reviews; `Count` = items on the page. Each item: `Id`, `ListingId`, `ReviewerId`, `Rating`, `Comment`, `CreatedAt`.

New fake `Fakes/FakeReviewRepository.cs`, built like `FakeFavoriteRepository`: a `List<Review>` exposed as `Items`; `RejectAdd` (bool, default false: `AddAsync` returns false without storing); `ExistsForListingAsync` and `GetBySellerAsync` read `Items`; `GetBySellerAsync` returns a snapshot (`ToList()`).

Files: `Domain/Reviews/ReviewTests.cs`, `Features/Reviews/AddReview/AddReviewHandlerTests.cs`, `Features/Reviews/GetMemberReviews/GetMemberReviewsHandlerTests.cs` (all new).

Arrange: a sold listing is `Listing.Create(SellerId, ...).Value`, then `Publish()`, then `var reservation = listing.Reserve(BuyerId, Now).Value`, then `listing.ConfirmSale(reservation.Id, Now)`. A reserved (not sold) listing stops after `Reserve`. For `AddReview` the reviewer must exist in `FakeMemberRepository`: create the buyer with `Member.Create(...)` and use its `member.Id` as the buyer of the reservation. For `GetMemberReviews` the seller must exist in `FakeMemberRepository`, and the reviews in the fake are `Review.Create(buyerId, soldListing, rating, comment, createdAt).Value` on listings of that seller, with `createdAt` chosen by the test (for example `Now.AddHours(-2)`) to control the order. Fixed, named ids: `SellerId`, `BuyerId`, `OtherMemberId`. `Now` is fixed and reaches the handler through `FixedTimeProvider`.

Tests to add:
- `Review.Create`: the buyer of a sold listing, rating 5, comment `"  Great seller  "` → success with a non-empty `Id`, `listing.Id`, `listing.SellerId`, the reviewer, 5, `"Great seller"` and `createdAt`; comment `null`, `""` and `"   "` → success with `Comment` null; rating 1 and 5 → success; rating 0 and 6 → `Review.InvalidRating`; comment of 500 characters → success; 501 → `Review.CommentTooLong`; 500 characters with spaces around → success; `Guid.Empty` → `Review.ReviewerEmpty`; a draft, an active and a reserved listing → `Review.SaleNotCompleted`; another member and the seller → `Review.NotBuyer` with `ErrorType.Forbidden`. Order: `Guid.Empty` on a draft → `Review.ReviewerEmpty`; another member with rating 0 → `Review.NotBuyer`; the buyer with rating 0 on a reserved listing → `Review.SaleNotCompleted`. Two calls give two different `Id`s.
- `AddReviewHandler`: success → the response has the `Id` of the review, the listing id, `SellerId`, the rating, the trimmed comment and `Now`; one review in the fake; `SaveCount` 1; unknown listing → NotFound `Listing.NotFound`; unknown member → NotFound `Member.NotFound`; both unknown → `Listing.NotFound`; not sold → `Review.SaleNotCompleted`; another member → Forbidden `Review.NotBuyer`; rating 6 → `Review.InvalidRating`; the listing already has a review → Conflict `Review.AlreadyExists`, still one review; `RejectAdd = true` → Conflict `Review.Duplicate`. In every failure `SaveCount` 0 and nothing new in the fake.
- `GetMemberReviewsHandler`: a member with no reviews → `AverageRating` null, `ReviewCount` 0, empty `Items`, `Count` 0; ratings 5 and 4 → `AverageRating` 4.5, `ReviewCount` 2, newest first, every field of the items checked; ratings 5, 4 and 4 → `AverageRating` 4.33 (two decimals); two reviews with the same `CreatedAt` → ordered by `Id`; reviews of another seller are not returned and not counted; three reviews with `Page` 2 and `PageSize` 2 → one item (the oldest), `Count` 1, but `ReviewCount` 3 and `AverageRating` over all three; `PageSize` 100 → success; `Page` 0 → `Reviews.InvalidPage`; `PageSize` 0 and 101 → `Reviews.InvalidPageSize`; unknown member → NotFound `Member.NotFound`; `Page` 0 for an unknown member → `Reviews.InvalidPage` (paging is checked first); the default query (only `MemberId`) → `Page` 1, `PageSize` 20. The fakes are unchanged after every call.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`. Build the messages from `Review.MinRating`, `Review.MaxRating` and `Review.MaxCommentLength`, the same way the domain does. The message of `Review.Duplicate` contains the generated Id: use `.Match("A review with id '*' already exists.")`, as for `Favorite.Duplicate`.
- Never use real time: `Now` is fixed.

## Feature: Condition filter (Carina)

Production code: `Features/Listings/GetListings/GetListingsHandler.cs`, the new optional `Condition` filter of `GetListingsQuery`. `Condition` is the **last** parameter of `GetListingsQuery` (after `PageSize`), so existing positional calls keep their meaning; set it with a named argument (`Condition: Condition.Good`).

File: `Features/Listings/GetListings/GetListingsHandlerTests.cs` (add to it, keep every existing test).

Tests to add:
- three Active listings (New, Good, Worn), query with `Condition = Good` → only the Good one, `TotalCount` 1.
- `Category` and `Condition` together → only the listings that match both.
- no `Condition` → every Active listing comes back (unchanged behaviour).

Rules for this feature:
- The handler is a query handler: the repository content is unchanged after every call.
- Never use real time; every `createdAt` is a fixed `DateTimeOffset`.

## Feature: My profile (Carina)

Production code (written by hand, read it before generating):
- `Features/Members/GetMyProfile/GetMyProfileHandler.cs`: a query handler with only `IMemberRepository` (no `IUnitOfWork`). Unknown member → `NotFound` `Member.NotFound`; otherwise `GetMyProfileResponse.From(member)` with `Id`, `Name`, `Email` (the email value), `Role` (as text) and `CreatedAt`. There is no password field in the response.
- `Features/Members/UpdateMyProfile/UpdateMyProfileHandler.cs`: unknown member → `NotFound` `Member.NotFound`; otherwise `member.Update(command.Name, member.Email.Value)`, so only the name changes and the email stays the same; a validation error from `Update` is returned as it is; then `repository.Update` and one `CommitAsync`.

Files (new): `Features/Members/GetMyProfile/GetMyProfileHandlerTests.cs`, `Features/Members/UpdateMyProfile/UpdateMyProfileHandlerTests.cs`.

Arrange: `FakeMemberRepository`, `FakeUnitOfWork`, a fixed `Now`. Build members with `Member.Register(name, email, new FakePasswordHasher().Hash("secret12"), Now)` (or with `MemberRole.Admin` as the last argument for an Admin) and put them straight into `FakeMemberRepository.Items`.

Tests to add:
- GetMyProfile:
  - a stored Student → success with its `Id`, `Name`, `Email` (lower case), `Role` `"Student"` and `CreatedAt` = `Now`; the repository is unchanged.
  - a stored Admin → `Role` is `"Admin"`.
  - unknown id → `NotFound` `Member.NotFound`.
- UpdateMyProfile:
  - a valid new name → success, returns the member's `Id`; the stored member has the new (trimmed) name and the same email; `UpdateCount` 1; `SaveCount` 1.
  - a name of 1 character → `Validation` `Member.InvalidName`; the name is unchanged; `UpdateCount` 0; `SaveCount` 0.
  - unknown id → `NotFound` `Member.NotFound`; `SaveCount` 0.

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code).
- Never use real time.
- Then run the tests with coverage and generate the report as this skill says: `Domain/` and the handlers stay at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Public profile (Carina)

Production code (written by hand, read it before generating): `Features/Members/GetPublicProfile/GetPublicProfileHandler.cs`, a query handler with `IMemberRepository`, `IListingRepository` and `IReviewRepository` (no `IUnitOfWork`).
- Unknown member → `NotFound` `Member.NotFound`.
- Otherwise: only the listings with `SellerId` = the member's id and `Status` = `Active`, ordered by `CreatedAt` descending, then `Id` ascending, as `PublicListingItem(Id, Title, Price.Amount)`.
- Reviews come from `GetBySellerAsync`: `AverageRating` is null when there are none, otherwise `Math.Round(average, 2)`; `ReviewCount` is their number.
- The response has no email and no role.

File (new): `Features/Members/GetPublicProfile/GetPublicProfileHandlerTests.cs`.

Fakes: `FakeMemberRepository`, `FakeListingRepository`, and `FakeReviewRepository`. If `FakeReviewRepository` already exists in `Fakes/`, use it as it is; if not, add one in the same hand-written style (`Items` by `Id`, `GetBySellerAsync` returns the reviews with that `SellerId`).

Arrange (read `Listing.cs` and `Review.cs` for the exact signatures; fixed `Now` and fixed, named ids such as `SellerId`, `BuyerId`, `OtherSellerId`):
- Draft listing: `Listing.Create(...).Value`. Active: then `Publish()`. Sold: `Publish()`, `Reserve(BuyerId, Now)`, then `ConfirmSale(reservationId, Now)`.
- A review: `Review.Create(BuyerId, soldListing, rating, null, Now).Value` (the sold listing's completed reservation belongs to `BuyerId`).
- Give the listings different fixed `createdAt` values, so the expected order is known.

Tests to add:
- the member has 2 Active, 1 Draft and 1 Sold listing → only the 2 Active ones, newest first, with the right title and price.
- an Active listing of another seller is not included.
- no reviews → `AverageRating` null, `ReviewCount` 0.
- reviews with ratings 5 and 4 → `AverageRating` 4.5, `ReviewCount` 2.
- reviews with ratings 5, 4 and 4 → `AverageRating` 4.33 (rounded to 2 decimals).
- unknown id → `NotFound` `Member.NotFound`.
- In every test the repositories are unchanged after the call (it is a query).

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code).
- Never use real time.
- Then run the tests with coverage and generate the report as this skill says: `Domain/` and the handlers stay at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Minimum category confidence (Diana)

Production code: `Features/Listings/SuggestCategory/SuggestCategoryHandler.cs`, `MinConfidence` = 0.7; `SuggestCategoryResponse.Category` is now nullable.

File: `Features/Listings/SuggestCategory/SuggestCategoryHandlerTests.cs` (add to it). The fake suggester returns a configurable confidence.

Tests to add:
- confidence 0.9 → `Category` is the suggested one.
- confidence exactly 0.7 → `Category` is the suggested one.
- confidence 0.69 → `Category` is null, `Confidence` is still 0.69.
- If existing tests use a confidence below 0.7 and expect a category, raise the fake's confidence in them to 0.9; change nothing else.

## Feature: Maximum price (Diana)

Production code: `Domain/Listings/Money.cs`, `Money.MaxAmount` = 100000.

File: `Domain/Listings/MoneyTests.cs` (add to it).

Tests to add:
- exactly `Money.MaxAmount` → success.
- `Money.MaxAmount + 0.01m` → `Validation`, code `Money.TooHigh`, exact message from the code.
- `Listing.Create` with a price above the maximum → the same error.

## Feature: Price feedback loop (Sara)
Production code (written by hand, read it before generating):
- `Domain/Listings/SaleRecord.cs`: `SaleRecord.Create(...)` returns `Result<SaleRecord>`; prices must be > 0.
- `Features/Listings/ConfirmSale/ConfirmSaleHandler.cs`: new dependencies `ISaleRecordRepository`, `IPriceSuggester`, `ILogger<ConfirmSaleHandler>`. After a successful confirm sale it asks the suggester and adds a `SaleRecord` (same `CommitAsync`). If the suggester throws or returns a price <= 0, the sale still succeeds and no record is added.
- `Features/Admin/PriceAccuracy/PriceAccuracyHandler.cs` (query handler): count, MAE, MAPE, per category, last 10 newest first; with no records count 0 and null metrics.
  Existing tests: `Features/Listings/ConfirmSale/ConfirmSaleHandlerTests.cs` no longer compiles (the constructor has 6 parameters). Update every `new ConfirmSaleHandler(...)` to pass a `FakeSaleRecordRepository`, a fake price suggester and `NullLogger<ConfirmSaleHandler>.Instance`; change nothing else in those tests.
  Fakes: reuse the existing fake price suggester if there is one; otherwise add `FakePriceSuggester` (configurable price, or throws when asked) in the same hand-written style. Add `FakeSaleRecordRepository` (`Items`, `Add`, `GetAllAsync`).
  Tests to add:
- `SaleRecord.Create`: valid → success with all fields; suggested price 0 → validation error; sold price 0 → validation error (exact code and message from the code).
- ConfirmSale success with suggester returning 120 → one record with suggested 120, sold = listing price, the listing's category and condition; `SaveCount` 1.
- ConfirmSale success with suggester throwing → sale succeeds, no record, `SaveCount` 1.
- ConfirmSale success with suggester returning 0 → sale succeeds, no record.
- ConfirmSale failure (any existing failure case) → no record.
- PriceAccuracy: no records → count 0, metrics null; records (suggested 100/sold 80, suggested 50/sold 50) → count 2, MAE 10, MAPE 12.5; per category grouped; last records newest first, at most 10.
  Rules: every error checks type, code and exact message; fixed `Now`; Domain and handlers stay at 100% lines and branches.

## Feature: Change password (Carina)

Production code (written by hand, read it before generating):
- `Domain/Members/Member.cs`, `ChangePassword(newPasswordHash)`: an empty or whitespace hash → `Validation` `Member.PasswordRequired` ("A password is required."); otherwise it sets `PasswordHash` and returns the same member.
- `Features/Auth/ChangePassword/ChangePasswordHandler.cs`, in this order: member not found → `NotFound` `Member.NotFound`; `PasswordHash` null or `Verify` false → `Validation` `Auth.InvalidCredentials` ("The email or the password is wrong."); new password null, empty or shorter than `RegisterHandler.MinPasswordLength` (8) → `Validation` `Auth.WeakPassword`; `NewPassword` different from `ConfirmNewPassword` → `Validation` `Auth.PasswordsDoNotMatch`; otherwise `Hash(new)`, `ChangePassword`, `repository.Update`, one `CommitAsync`, and it returns the member's `Id`.

Files: `Domain/Members/MemberTests.cs` (add to it), `Features/Auth/ChangePassword/ChangePasswordHandlerTests.cs` (new). Use the existing `FakeMemberRepository`, `FakePasswordHasher` and `FakeUnitOfWork`; read them first to see how the fake hash is built and what they count.

Tests to add:
- `Member.ChangePassword`: a hash → stored, the same instance is returned; `""` and `"   "` (`[Theory]`) → `Member.PasswordRequired`, the hash is unchanged.
- success → `PasswordHash` is the fake hash of the new password, returns the member's `Id`, `UpdateCount` 1, `SaveCount` 1.
- wrong current password → `Auth.InvalidCredentials`; hash unchanged; `UpdateCount` 0; `SaveCount` 0.
- a member without a password (`Member.Create`) → `Auth.InvalidCredentials`.
- new password of 7 characters → `Auth.WeakPassword`; exactly 8 characters → success (the boundary); empty → `Auth.WeakPassword`.
- confirmation different → `Auth.PasswordsDoNotMatch`; `SaveCount` 0.
- unknown member → `NotFound` `Member.NotFound`; `SaveCount` 0.
- the `changed.IsFailure` branch of the handler: `FakePasswordHasher` never returns an empty hash, so add one small private class in `ChangePasswordHandlerTests.cs` that implements `IPasswordHasher`, whose `Hash` returns `""` and `Verify` returns `true` → `Validation` `Member.PasswordRequired` (from `Member.ChangePassword`); the hash is unchanged; `UpdateCount` 0; `SaveCount` 0. This extra hasher is allowed only for this test.

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code).
- Never use real time.
- Then run the tests with coverage and generate the report as this skill says: `Domain/` and the handlers stay at 100% for lines and branches.
- If a branch of `ChangePasswordHandler` or `Member.ChangePassword` still cannot be reached with these fakes, stop and report it; do not remove code from `src/`.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Validation thresholds (Anca)

Production code (read it before generating): the minimum confidence of the category suggestion is no longer a constant in the handler. `SuggestCategoryHandler(ICategorySuggester suggester, IOptions<CategorySuggestionOptions> options)` returns the category only when `suggestion.Confidence >= options.Value.MinConfidence`, otherwise `Category` null (the confidence is always returned). `CategorySuggestionOptions` is in `Common/Categories/`, property `MinConfidence` (default 0.6). `SuggestCategoryHandler.MinConfidence` does not exist any more.

File: `Features/Listings/SuggestCategory/SuggestCategoryHandlerTests.cs` (update it).

Changes to the existing tests (keep every test, its name and its assertions):
- Build the handler with `new SuggestCategoryHandler(suggester, Options.Create(new CategorySuggestionOptions { MinConfidence = 0.7 }))` in every test, through one private helper in the class, so the tests written for the 0.7 limit keep their meaning (0.9 and exactly 0.7 give the category, 0.69 gives null).

Tests to add:
- `MinConfidence` 0.6 and a suggestion with confidence 0.6 → the category is returned (the limit is inclusive); with confidence 0.59 → `Category` null and `Confidence` 0.59.
- `MinConfidence` 0.95 and confidence 0.9 → `Category` null: the handler uses the configured value, not a fixed one.
- Each of these checks that the fake suggester was called once with the trimmed title.

Rules for this feature:
- `Options.Create` comes from `Microsoft.Extensions.Options`; never mock `IOptions`.
- Never use real time.

## Feature: Similar listings (Anca)

Production code (read it before generating): `Features/Listings/GetSimilarListings/GetSimilarListingsHandler.cs`, a query handler with `IListingRepository` and `ISimilarListingsFinder` (`Common/Similarity/`, method `Similarities(ListingText target, IReadOnlyList<ListingText> candidates)` returning one similarity per candidate, in the same order). `GetSimilarListingsQuery.MaxResults` is 5.
- Unknown id → `NotFound` `Listing.NotFound` (message `Listing '<id>' was not found.`); the finder is not called.
- The target can have any status. The candidates are every `Active` listing except the target; when there is none, the result is an empty list and the finder is not called.
- The finder gets the target's title and description and the candidates' titles and descriptions. The items are ordered by similarity (highest first), equal similarities by `CreatedAt` (newest first) then by `Id`; at most 5; each item has id, title, category, condition, price and the similarity rounded to two decimals (`MidpointRounding.AwayFromZero`).

New fake: `Fakes/FakeSimilarListingsFinder.cs` implementing `ISimilarListingsFinder`: the test gives a similarity per candidate title (a dictionary; a title not in it gets 0); it records the call count, the last target and the last candidates.

File: `Features/Listings/GetSimilarListings/GetSimilarListingsHandlerTests.cs` (new).

Tests to add:
- unknown id → `NotFound` `Listing.NotFound` with the exact message; finder call count 0.
- the target is the only `Active` listing (the others are a draft, a reserved, a sold and a hidden listing) → success with an empty list; finder call count 0.
- with a draft target and three `Active` listings → success; the finder got the target's title and description and exactly the three `Active` listings (never the target, never a non-`Active` one).
- seven `Active` candidates with different similarities → five items, highest similarity first.
- two candidates with the same similarity and different `CreatedAt` → the newer first; same similarity and same `CreatedAt` → the smaller `Id` first.
- similarities 0.456 and 0.125 → 0.46 and 0.13 in the items; the item carries the candidate's id, title, category, condition and price.
- the repository content is unchanged after every call.

Rules for this feature:
- Never use real time: every `createdAt` is a fixed `DateTimeOffset`.
- Build hidden, reserved and sold listings through the domain methods (`Publish`, `Reserve`, `ConfirmSale`, `Hide`), like the other handler tests.

## Feature: Password reset (Sara)

Production code (written by hand, read it before generating):
- `Domain/Members/PasswordResetToken.cs`: the factory returns the token and the plain value once; only the SHA-256 hash is stored; the token is valid 30 minutes; `MarkUsed` makes it single use; an expired or already used token is rejected by `IsValid(now)`.
- `Features/Auth/ForgotPassword/ForgotPasswordHandler.cs`: unknown email → success (the same response as a known email), no email is sent, no token saved; known email → one token saved and one email sent whose body contains the plain token; if the handler invalidates old tokens, it does so before saving the new one.
- `Features/Auth/ResetPassword/ResetPasswordHandler.cs`: a valid token and a matching strong password → the member's hash is the fake hash of the new password, the token is used, `SaveCount` 1; an unknown, expired or already used token → `Validation` `Auth.InvalidResetToken`; a weak password → `Validation` (`Auth.WeakPassword`).
- `Infrastructure/Mailing/LoggingEmailSender.cs` and `Infrastructure/Mailing/SmtpEmailSender.cs`: only what can be tested without a real SMTP server (the logger message for `LoggingEmailSender`; for `SmtpEmailSender` only what reads from `IOptions<EmailOptions>` and the environment variables, never an actual `SendAsync` to a server).

Fakes: `FakePasswordResetTokenRepository` (new, same hand-written style, `Items` by `Id`, `AddAsync`, `GetByHashAsync`, `Update`, `GetActiveByMemberAsync` if the handler uses it; `UpdateCount`), `FakeEmailSender` (new: implements `IEmailSender` and records every sent email in a public `List<(string To, string Subject, string Body)> Sent`), the existing `FakeMemberRepository`, `FakeRefreshTokenRepository`, `FakePasswordHasher`, `FakeUnitOfWork`; fixed `Now` through `FixedTimeProvider`.

Files:
- `Domain/Members/PasswordResetTokenTests.cs` (new).
- `Features/Auth/ForgotPassword/ForgotPasswordHandlerTests.cs` (new).
- `Features/Auth/ResetPassword/ResetPasswordHandlerTests.cs` (new).
- `Infrastructure/Mailing/LoggingEmailSenderTests.cs` (new).
- `Infrastructure/Mailing/SmtpEmailSenderTests.cs` (new).

Tests to add:
- `PasswordResetToken`:
  - the stored hash is not the plain token and equals SHA-256 of it (hex, upper case).
  - `IsValid(now)` is true one second before 30 minutes, false exactly at 30 minutes, false after 30 minutes.
  - `MarkUsed(now)` sets `UsedAt`; a used token is rejected by `IsValid`; calling `MarkUsed` twice keeps the first time.
  - an expired token is rejected even when it has not been used; a used token is rejected even when it has not expired.
- `ForgotPasswordHandler`:
  - unknown email → the response is the same shape as for a known email; no email captured (`FakeEmailSender.Sent` is empty); no token saved; `SaveCount` 0.
  - known email → one token saved for this member, one email to that address whose body contains the plain token; `SaveCount` 1.
  - if the handler invalidates old tokens: an existing active token of the same member is marked used before the new one is saved; a token of another member is not touched. If the handler does not do this, omit these two tests.
- `ResetPasswordHandler`:
  - valid token and matching strong password → success; the member's `PasswordHash` is `FakePasswordHasher.Hash(new)`; the token is marked used; `UpdateCount` 1; `SaveCount` 1.
  - unknown token → `Validation` `Auth.InvalidResetToken` with the exact message, nothing changed, `SaveCount` 0.
  - expired token → `Validation` `Auth.InvalidResetToken` with the exact message, nothing changed, `SaveCount` 0.
  - already used token → `Validation` `Auth.InvalidResetToken` with the exact message, nothing changed, `SaveCount` 0.
  - weak password (shorter than `RegisterHandler.MinPasswordLength`, `[Theory]` with `""` and a 7-character value) → `Validation` with the exact code and message, nothing changed, `SaveCount` 0; a confirmation different from the new password → `Validation` `Auth.PasswordsDoNotMatch`, nothing changed, `SaveCount` 0.
- `LoggingEmailSender`: `SendAsync` writes one log entry at `Information` level whose message contains the `To` and the `Subject`, and never the `Body` (assert through a captured `ILogger<LoggingEmailSender>` or an `ITestOutputHelper`-style logger). No other side effect; `Task` completed.
- `SmtpEmailSender`: only what can be tested without a real server: `IOptions<EmailOptions>` with `Host` null → a `SendAsync` call throws (the constructor or the call rejects a missing host, depending on the production code — assert what the code actually does); when `Email__Username` is set, the credentials are read from the environment variables (through a small seam if `SmtpEmailSender` exposes one; otherwise this test is not written). Never open an SMTP connection and never write the port or the TLS flag to disk.

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code). Build the length and lifetime constants from the production code (`PasswordResetToken.Lifetime`, `RegisterHandler.MinPasswordLength`), not hard-coded values.
- Never use real time: `Now` is a fixed `DateTimeOffset`, given to `FixedTimeProvider`. The 30-minute boundary is `Now.Add(PasswordResetToken.Lifetime)` and `Now.Add(PasswordResetToken.Lifetime).AddSeconds(-1)`.
- Never send a real email and never open an SMTP connection: the handler tests use `FakeEmailSender` and the `SmtpEmailSender` tests stop at what the class does without a server.
- Then run the tests with coverage and generate the report as this skill says: `Domain/` and the handlers stay at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.


## Feature: Conversations (Anca)

Production code (read it before generating):
- `Domain/Conversations/Conversation.cs`:
    - `Conversation.Start(buyerId, listing, text, now)` checks, in this order: `Guid.Empty` → `Conversation.BuyerEmpty`; the buyer is the seller → `Conversation.OwnListing`; the listing is not `Active` or `Reserved` (draft, sold, hidden) → `Conversation.ListingNotAvailable`; then the errors of the first message (see `Send`). Otherwise a conversation with a new `Id`, `listing.Id`, `listing.SellerId`, the buyer, `CreatedAt` and `LastMessageAt` = `now`, and one message (number 1, from the buyer).
    - `Send(senderId, text, now)` checks, in this order: the sender is neither the buyer nor the seller → `Conversation.NotParticipant` (`ErrorType.Forbidden`); the text is null, empty or blank → `Message.Empty`; longer than `Message.MaxTextLength` (1000) after trim → `Message.TooLong`. Otherwise it adds a message with a new `Id`, the conversation id, `Number` = count + 1, the sender, the **trimmed** text and `now`, sets `LastMessageAt` = `now` and returns the message. A failed `Send` changes nothing.
    - `IsParticipant(memberId)`; `OtherParticipant(memberId)`: the buyer for the seller, the seller for anyone else.
- `Domain/Conversations/IConversationRepository.cs`: `AddAsync`, `GetByIdAsync`, `GetByListingAndBuyerAsync(listingId, buyerId)`, `GetForMemberAsync(memberId)` (buyer or seller).
- `Features/Conversations/StartConversation/`: `StartConversationCommand(BuyerId, ListingId, Text)`. Order: 404 `Listing.NotFound`, 404 `Member.NotFound`; if the buyer already has a conversation about this listing, the message is sent in it (`Send` errors), otherwise `Conversation.Start` (its errors) and `AddAsync`; then one `CommitAsync`. Returns `MessageResponse(ConversationId, Id, Number, SenderId, Text, SentAt)`.
- `Features/Conversations/SendMessage/`: `SendMessageCommand(SenderId, ConversationId, Text)`. 404 `Conversation.NotFound`, then the `Send` errors, then one `CommitAsync`. Same `MessageResponse`.
- `Features/Conversations/GetConversation/`: `GetConversationQuery(MemberId, ConversationId)`. 404 `Conversation.NotFound`; not the buyer or the seller → Forbidden `Conversation.NotParticipant` (message `Only the buyer and the seller can read this conversation.`). Returns the id, listing id and title, seller id and name, buyer id and name, `CreatedAt` and the messages ordered by `Number`.
- `Features/Conversations/GetMyConversations/`: `GetMyConversationsQuery(MemberId)`. Every conversation where the member is the buyer or the seller; each item: id, listing id and title, the other member's id and name, the text, sender and time of the last message (highest `Number`), `LastMessageAt`, the message count; ordered by `LastMessageAt` descending, then by `Id`. `Count` = number of items. No conversation → empty list (the listing and member repositories are not needed).

New fake `Fakes/FakeConversationRepository.cs`: a `List<Conversation>` exposed as `Items`; `AddAsync` stores; the three reads look in `Items` (`GetForMemberAsync` returns a snapshot with `ToList()`).

Files: `Domain/Conversations/ConversationTests.cs`, `Features/Conversations/StartConversation/StartConversationHandlerTests.cs`, `Features/Conversations/SendMessage/SendMessageHandlerTests.cs`, `Features/Conversations/GetConversation/GetConversationHandlerTests.cs`, `Features/Conversations/GetMyConversations/GetMyConversationsHandlerTests.cs` (all new).

Arrange: fixed, named ids `SellerId`, `BuyerId`, `OtherMemberId`; a published listing is `Listing.Create(SellerId, ...).Value` then `Publish()`; reserved, sold and hidden through the domain methods (`Reserve`, `ConfirmSale`, `Hide`). For the handlers the members must exist in `FakeMemberRepository` (create them with `Member.Create(...)` and use their `Id`s) and the listings in `FakeListingRepository`. `Now` is fixed and reaches the handlers through `FixedTimeProvider`; for later messages call `Send` with `Now.AddMinutes(...)`.

Tests to add:
- `Conversation.Start`: an active listing and `"  Mai e disponibil?  "` → success with a non-empty `Id`, `listing.Id`, `SellerId`, the buyer, `CreatedAt` and `LastMessageAt` = `Now`, one message with number 1, the buyer, `"Mai e disponibil?"`, `Now` and the conversation id; a reserved listing → success; a draft, a sold and a hidden listing → `Conversation.ListingNotAvailable`; the seller → `Conversation.OwnListing`; `Guid.Empty` → `Conversation.BuyerEmpty`; text `null`, `""`, `"   "` → `Message.Empty`; 1000 characters → success; 1001 → `Message.TooLong`. Order: `Guid.Empty` on a draft → `Conversation.BuyerEmpty`; the seller on a draft → `Conversation.OwnListing`; empty text on a draft → `Conversation.ListingNotAvailable`.
- `Send`: the seller answers → number 2, `LastMessageAt` is the new time; the buyer again → number 3; another member → Forbidden `Conversation.NotParticipant`, still the same messages and `LastMessageAt`; empty and too long text → their errors, nothing added; another member with an empty text → `Conversation.NotParticipant`. `IsParticipant` true for the two, false for another member; `OtherParticipant` gives the buyer for the seller and the seller for the buyer.
- `StartConversationHandler`: no conversation yet → the response (conversation id of the stored conversation, number 1, the buyer, the trimmed text, `Now`), one conversation in the fake, `SaveCount` 1; the buyer already has one → the message is added to it (number 2), still one conversation, `SaveCount` 1; the existing conversation's listing is now sold → the message is still sent; unknown listing → `Listing.NotFound`; unknown member → `Member.NotFound`; both unknown → `Listing.NotFound`; the seller → `Conversation.OwnListing`; a draft → `Conversation.ListingNotAvailable`; empty text, new and existing conversation → `Message.Empty`. In every failure `SaveCount` 0 and nothing new in the fake.
- `SendMessageHandler`: the seller answers → the response (number 2, the seller, the trimmed text, `Now`), the message is in the stored conversation, `SaveCount` 1; unknown conversation → `Conversation.NotFound`; another member → Forbidden `Conversation.NotParticipant`; empty and too long → their errors. In every failure `SaveCount` 0 and no new message.
- `GetConversationHandler`: as the buyer and as the seller → every field, messages in number order (seed three messages); another member → Forbidden `Conversation.NotParticipant`; unknown → `Conversation.NotFound`. The fakes are unchanged after every call.
- `GetMyConversationsHandler`: a member with no conversation → empty list, `Count` 0; the seller with two conversations (two buyers, two listings) → both, the one with the latest message first, other member = the buyer, every field checked (last message text and sender, count); the buyer → other member = the seller; conversations of other members are not returned; two conversations with the same `LastMessageAt` → ordered by `Id`. The fakes are unchanged after every call.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`; build the `Message.TooLong` message from `Message.MaxTextLength`.
- Never use real time: `Now` is fixed.

## Feature: Listing photos (Anca)

Production code (read it before generating):
- `Domain/Photos/PhotoFormat.cs`: `PhotoFormat.Detect(bytes)` → `PhotoFormat.Jpeg` (starts with `FF D8 FF`), `PhotoFormat.Png` (`89 50 4E 47 0D 0A 1A 0A`), `PhotoFormat.WebP` (`"RIFF"`, any 4 bytes, `"WEBP"`, so at least 12 bytes), otherwise `null`. Each format has `Extension` (`.jpg`, `.png`, `.webp`) and `ContentType` (`image/jpeg`, `image/png`, `image/webp`).
- `Domain/Photos/Photo.cs`: `Photo.Create(uploaderId, listing, existingCount, content, createdAt)` checks, in this order: the uploader is not the seller → `Photo.NotSeller` (`ErrorType.Forbidden`); `existingCount` ≥ `Photo.MaxPerListing` (5) → `Photo.LimitReached`; empty content → `Photo.Empty`; more than `Photo.MaxSizeBytes` (2 MB = 2 097 152 bytes) → `Photo.TooLarge`; unknown format → `Photo.InvalidFormat`. Otherwise a photo with a new `Id`, `listing.Id`, `FileName` = `<Id><extension>`, the format's `ContentType`, `SizeBytes` = content length and `createdAt`. Any listing status is accepted.
- `Common/Storage/IPhotoStorage.cs`: `SaveAsync(listingId, fileName, content)`, `OpenRead(listingId, fileName)` (null when missing), `Delete(listingId, fileName)`, `DeleteListing(listingId)`.
- `Features/Photos/UploadPhoto/`: `UploadPhotoCommand(UploaderId, ListingId, Content)`. 404 `Listing.NotFound`, then the errors of `Photo.Create` (with the listing's current photo count), then the file is saved, the photo added and one `CommitAsync`. Returns `PhotoResponse(Id, ListingId, Url = "/api/v1/photos/<Id>", ContentType, SizeBytes, CreatedAt)`.
- `Features/Photos/GetListingPhotos/`: 404 `Listing.NotFound`; the listing's photos ordered by `CreatedAt`, then `Id`; `Count`.
- `Features/Photos/GetPhotoFile/`: 404 `Photo.NotFound` when the photo is unknown **or** its file is missing in the storage; otherwise `PhotoFile(Content, ContentType)`.
- `Features/Photos/DeletePhoto/`: `DeletePhotoCommand(RequesterId, ListingId, PhotoId)`. 404 `Listing.NotFound`; not the seller → Forbidden `Photo.NotSeller`; unknown photo or a photo of another listing → 404 `Photo.NotFound`; then the photo is removed, one `CommitAsync`, then the file is deleted. Returns the photo id.
- `Features/Listings/DeleteListing/DeleteListingHandler.cs`: has a new third constructor parameter `IPhotoStorage`; after a successful delete (after `CommitAsync`) it calls `DeleteListing(listing.Id)`; on a failure it does not.

New fakes:
- `Fakes/FakePhotoRepository.cs`: a `List<Photo>` exposed as `Items`; `AddAsync` stores; the reads look in `Items` (`GetByListingAsync` returns a snapshot with `ToList()`); `Delete` removes.
- `Fakes/FakePhotoStorage.cs`: a dictionary `(listingId, fileName) → bytes` exposed as `Files`; `SaveAsync` stores, `OpenRead` returns a `MemoryStream` of the bytes or null, `Delete` removes the entry, `DeleteListing` removes every entry of the listing and records the id in `DeletedListings`.

Files: `Domain/Photos/PhotoFormatTests.cs`, `Domain/Photos/PhotoTests.cs`, `Features/Photos/UploadPhoto/UploadPhotoHandlerTests.cs`, `Features/Photos/GetListingPhotos/GetListingPhotosHandlerTests.cs`, `Features/Photos/GetPhotoFile/GetPhotoFileHandlerTests.cs`, `Features/Photos/DeletePhoto/DeletePhotoHandlerTests.cs` (all new); `Features/Listings/DeleteListing/DeleteListingHandlerTests.cs` (change, see below).

Arrange: fixed, named ids `SellerId`, `OtherMemberId`; a listing is `Listing.Create(SellerId, ...).Value`. Test images are byte arrays built from the signatures above followed by zeros (for example `[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, …]`); write them as `static readonly` fields. `Now` is fixed and reaches the handlers through `FixedTimeProvider`.

Tests to add:
- `PhotoFormat.Detect`: a JPEG, a PNG and a WebP start → the right format with its extension and content type; a GIF (`"GIF89a"`), random bytes, an empty array, only `"RIFF"` (4 bytes), `"RIFF"` + 4 bytes + `"WAVE"` → null; the first 2 bytes of a JPEG only → null.
- `Photo.Create`: the seller with a PNG and 0 photos → success with every field (`FileName` = `<Id>.png`, `image/png`, the length, `Now`); a JPEG → `.jpg`, a WebP → `.webp`; another member → Forbidden `Photo.NotSeller`; `existingCount` 4 → success, 5 → `Photo.LimitReached`; empty → `Photo.Empty`; exactly `MaxSizeBytes` bytes (PNG start) → success; one byte more → `Photo.TooLarge`; a GIF → `Photo.InvalidFormat`. Order: another member with 5 photos → `Photo.NotSeller`; the seller with 5 photos and empty content → `Photo.LimitReached`; empty content can never be checked for its format → `Photo.Empty`. Two calls give two different `Id`s.
- `UploadPhotoHandler`: success → the response (`Url` = `/api/v1/photos/<Id>`), one photo in the fake repository, the bytes in `FakePhotoStorage.Files` under `(listing.Id, FileName)`, `SaveCount` 1; the listing already has 5 photos in the fake → `Photo.LimitReached`; unknown listing → `Listing.NotFound`; another member → Forbidden `Photo.NotSeller`; a GIF → `Photo.InvalidFormat`. In every failure `SaveCount` 0, nothing in the repository and nothing in the storage.
- `GetListingPhotosHandler`: three photos with different `CreatedAt` (seed them in the wrong order) → in `CreatedAt` order with every field; the same `CreatedAt` → by `Id`; photos of another listing are not returned; a listing with no photo → empty list, `Count` 0; unknown listing → `Listing.NotFound`. The fakes are unchanged.
- `GetPhotoFileHandler`: a photo whose file is in the storage → the bytes (read the stream) and the content type; a photo whose file is not in the storage → `Photo.NotFound`; unknown id → `Photo.NotFound`.
- `DeletePhotoHandler`: the seller → success with the photo id, the photo removed from the repository and its file from the storage, `SaveCount` 1, the other photos and files untouched; another member → Forbidden `Photo.NotSeller`; unknown listing → `Listing.NotFound`; unknown photo → `Photo.NotFound`; a photo of another listing of the same seller → `Photo.NotFound`. In every failure `SaveCount` 0 and nothing removed.
- `DeleteListingHandlerTests` (change, keep every test and name): build the handler with a `FakePhotoStorage` as the third argument in every test; in the success test also check that `DeletedListings` contains the listing id; in every failure test check that `DeletedListings` is empty.

Rules for this feature:
- Every failure test checks `ErrorType`, the exact `Error.Code` and the exact `Error.Message`; build the messages from `Photo.MaxPerListing` and `Photo.MaxSizeBytes` the same way the domain does.
- Never use real time: `Now` is fixed.


## Feature: Full coverage (Carina)

Production code (read it before generating): `Features/Auth/ResetPassword/ResetPasswordHandler.cs`, the `changeResult.IsFailure` branch. After the token and the member are found, the handler calls `member.ChangePassword(passwordHasher.Hash(command.NewPassword))`; when it fails it returns the error right away, before `token.MarkUsed`, before the refresh tokens are revoked, before any `Update`, before `CommitAsync` and before the email.

File: `Features/Auth/ResetPassword/ResetPasswordHandlerTests.cs` (add to it, keep every existing test).

Arrange: `FakePasswordHasher` never returns an empty hash, so add one small private class in `ResetPasswordHandlerTests.cs` that implements `IPasswordHasher`, whose `Hash` returns `""` and `Verify` returns `true` (the same rule as the `EmptyHashPasswordHasher` of `ChangePasswordHandlerTests`). This extra hasher is allowed only for this test. Build the handler with `new ResetPasswordHandler(...)` directly, because the existing `BuildHandler` helper takes a `FakePasswordHasher`; do not change the helper. Seed a valid token, the member and one active refresh token of the member.

Test to add:
- valid token, matching strong password, hasher returns an empty hash → `Validation` `Member.PasswordRequired` ("A password is required.", from `Member.ChangePassword`); the member's `PasswordHash` is unchanged; the reset token is not marked used (`UsedAt` null); the refresh token is not revoked (`RevokedAt` null); `members.UpdateCount` 0, `tokens.UpdateCount` 0, `refreshTokens.UpdateCount` 0; `SaveCount` 0; no email sent.

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code).
- Never use real time.
- Then run the tests with coverage and generate the report as this skill says: `ResetPasswordHandler.cs` is at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

### Surviving mutants of issue #121 (Stryker run of 2026-11-22, 18 mutants)

Stryker (`dotnet stryker` in `tests/CampusMarket.UnitTests`) left 18 mutants alive (score 97.83%). The goal is 100%. Read the production code of every file below before writing a test and use its exact texts and values.

Rules for these mutants (they apply to every future test too):
- **Equivalent mutants.** Only when no test can kill a mutant (the changed value is never observable), mark it in `src/` with a comment on the line above: `// Stryker disable once <Mutator> : <reason>`, like `Listing.cs` and `Member.cs`. This comment is the only change allowed in `src/`. The new equivalent mutants:
  - `Domain/Conversations/Message.cs`, `Text = string.Empty` (String): the `internal` constructor always sets `Text`, and the private constructor is only used by EF Core, which overwrites it.
  - `Domain/Reviews/Review.cs`, `listing.Reservations.First(...)` (Linq, `First` vs `FirstOrDefault`): a `Sold` listing always has exactly one `Completed` reservation (`Listing.ConfirmSale`), so the predicate always matches.
  - `Domain/Photos/Photo.cs`, `FileName = string.Empty` and `ContentType = string.Empty` (String, 2 mutants, found after the merge with main, #120): `Photo.Create` always sets both (`FileName` = `<Id><extension>`, `ContentType` = the format's type), and the private constructor is only used by EF Core, which overwrites them. One comment above each property.
  - `Features/Listings/ExpireReservations/ExpireReservationsHandler.cs`, `r.Status == ReservationStatus.Active && r.IsExpired(now)` (Logical, `&&` → `||`, found after the merge with main, #111): `Reservation.IsExpired` already requires `Status == Active`, so the mutant is just `Status == Active`; a listing has at most one `Active` reservation (`Reserve` needs an `Active` listing and makes it `Reserved`), and the notification is written only when that reservation was expired, so both pick the same reservation.
  - `Features/Listings/GetListings/GetListingsHandler.cs`, `(query.Sort ?? "newest")` (String, `"newest"` → `""`, found after the merge with main, sorting): the switch has no `"newest"` arm, so `null`, `"newest"` and `""` all fall into the default arm (newest first), and `AllowedSorts` is checked before the fallback.
  - Before marking a mutant as equivalent, look for an input that behaves differently with the mutation (other statuses, older reservations, ties, the empty value); if one exists, write that test instead.
- **Boundaries of `< limit` checks.** For every `x < limit` (or `x > limit`) check, add a test with exactly the limit that succeeds; a value inside the limits that is far from it does not kill `<` → `<=`:
  - `ResetPasswordHandler`: a new password of exactly `RegisterHandler.MinPasswordLength` (8) characters → success.
  - `GetReportedListingsHandler` and `GetMemberReviewsHandler`: `PageSize` exactly 1 → success, `PageSize` 1 in the response.
  - `PhotoFormat.Detect` (`content.Length >= 12` for WebP): a WebP of exactly 12 bytes (`"RIFF"`, 4 size bytes, `"WEBP"`, nothing after) → `PhotoFormat.WebP`. The other WebP test images are longer, so they do not kill `>=` → `>`. Test name: `Given_WebPOfExactly12Bytes_When_Detecting_Then_ReturnsWebP` in `Domain/Photos/PhotoFormatTests.cs`.
- **Ordering with ties.** For every `OrderBy`/`ThenBy`, at least one test checks the exact order, and the tie-breaker (`ThenBy`) is tested with two items that have the same first key:
  - `PriceAccuracyHandler`: `PerCategory` comes back in `Category` order (records in `Electronics` and `Textbook` → `Textbook`, `Electronics`); two records with the same `SoldAt` → `LastRecords` ordered by the record `Id` descending.
  - `GetPublicProfileHandler`: two Active listings of the member with the same `CreatedAt` → ordered by `Id` ascending (`BeInAscendingOrder(i => i.Id)`).
  - Build the expected order from the ids in the test (`OrderBy`/`OrderByDescending` on the arranged objects), never from the handler's output.
- **`UpdateCount` on every updated fake.** A success test asserts `UpdateCount` on every fake repository the handler calls `Update` on (for example `ResetPasswordHandler`: `members.UpdateCount`, `tokens.UpdateCount`, `refreshTokens.UpdateCount` each 1). The fakes store the same instance, so a missing `Update` is only visible through the counter.
- **Emails: the full exact `Body`.** When a handler sends an email, the success test asserts `Body` with `.Be(...)` on the whole expected text (built with `Environment.NewLine` and the test's values, the same way the handler does), not only `Contain`/`NotContain`. This applies to `ForgotPasswordHandler` (the link is built from the plain token taken from the email) and `ResetPasswordHandler`.
- **Logs from `[LoggerMessage]`.** Add `Fakes/FakeLogger.cs`: `FakeLogger<T> : ILogger<T>`, hand-written, `IsEnabled` returns `true`, `BeginScope` returns `null`, `Log` stores the level and the message (`formatter(state, exception)`) in a public `List<(LogLevel Level, string Message)> Entries`. A handler that logs gets it instead of `NullLogger<T>.Instance` in the tests that check the log:
  - `ConfirmSaleHandler`, suggester throws → exactly one entry, `Warning`, message `"Price suggester threw while recording a sale; no SaleRecord saved for this confirmation."`.
  - `ConfirmSaleHandler`, suggester returns 0 → exactly one entry, `Warning`, message `$"Price suggester returned no usable suggestion (<= 0) for listing {listing.Id}; no SaleRecord saved."`.
  - A successful sale with a positive suggestion logs nothing (`Entries` empty).

Rules for these mutants:
- Keep every existing test and its name. In existing tests only add assertions (`UpdateCount`, the full `Body`); everything else is a new test.
- After the run, `dotnet test` passes, coverage of `Domain/` and the handlers stays 100%, and `dotnet stryker` in `tests/CampusMarket.UnitTests` reports 100%. If a mutant still survives, stop and report why; do not force it.

## Feature: Sorting (Diana)

Production code: `GetListingsHandler`, `GetListingsQuery.Sort` (null, "newest", "price_asc", "price_desc").

File: `Features/Listings/GetListings/GetListingsHandlerTests.cs` (add to it).

Tests to add:
- three Active listings priced 30, 10, 20 → `price_asc` gives 10, 20, 30; `price_desc` gives 30, 20, 10.
- `newest` and no sort → newest `CreatedAt` first (same as before).
- two listings with the same price → ordered by `Id` ascending.
- `Sort = "abc"` → `Validation` `Listings.InvalidSort`, exact message.

## Feature: Notifications (Sara)
Production code (read it before generating): `Domain/Notifications/Notification.cs` (Create returns Result, MarkAsRead idempotent), the notification lines added in `ReserveListingHandler`, `ConfirmSaleHandler`, `ExpireReservationsHandler`, and the handlers in `Features/Notifications/` (GetMyNotifications, MarkNotificationRead, MarkAllNotificationsRead).
Existing tests no longer compile (new constructor parameter): in `ReserveListingHandlerTests`, `ConfirmSaleHandlerTests` and `ExpireReservationsHandlerTests` pass a new `FakeNotificationRepository` (add it in `Fakes/`, same hand-written style: `Items`, `Add`, the query methods) wherever the handler is built; change nothing else in those tests.
Tests to add:
- `Notification.Create`: valid → all fields; invalid input → the exact error. `MarkAsRead` sets `ReadAt`; a second call keeps the first value.
- Reserve success → one notification for the seller, type ReservationCreated; reserve failure → none.
- Confirm sale success → one notification for the buyer, type ListingSold; failure → none.
- Expire → one notification per expired reservation, for its buyer; nothing to expire → none.
- GetMyNotifications: only mine, newest first, `unreadOnly` filter, `unreadCount`, paging.
- MarkNotificationRead: mine → read, `SaveCount` 1; unknown or someone else's → NotFound, `SaveCount` 0.
- MarkAllNotificationsRead: all my unread become read, others' unchanged.
  Rules: every error checks type, code and exact message; fixed `Now`; Domain and handlers stay at 100%.

## Feature: Admin categories (Carina)

Production code (written by hand, read it before generating):
- `Domain/Categories/ListingCategory.cs`: the admin settings of one of the 5 base categories (the `Category` enum). `Create(key, displayName, description = null, isActive = true)`: undefined key → NotFound `Category.NotFound`; then the text checks. Text checks, in this order: display name null, empty or blank → `Category.DisplayNameEmpty`; longer than `MaxDisplayNameLength` (100) after trim → `Category.DisplayNameTooLong`; description longer than `MaxDescriptionLength` (500) after trim → `Category.DescriptionTooLong`. Name and description are stored trimmed; an empty or blank description is stored as `null`. `Update(displayName, description, isActive, allCategories)`: the text checks, then deactivating the last active category (no other category in `allCategories` is active) → Conflict `Category.LastActive`; otherwise it changes the three values and returns the same instance. A failed `Update` changes nothing. `CheckAcceptsListings()`: `null` when active, otherwise the `Validation` error `Listing.CategoryInactive`.
- `Domain/Categories/ICategoryRepository.cs`: `GetAllAsync` (snapshot ordered by key), `GetByKeyAsync` (null when missing), `Update`.
- `Features/Categories/GetCategories/GetCategoriesHandler.cs`: query handler (no `IUnitOfWork`), returns every category as `GetCategoriesItem(Key, DisplayName, Description, IsActive)` and `Count`.
- `Features/Categories/UpdateCategory/UpdateCategoryHandler.cs`: `UpdateCategoryCommand(Category, DisplayName, Description, IsActive)`; `Category` is the key as text, matched by name only and case-insensitive (a number such as `"1"` is not a key). Order: unknown key or key not in the repository → NotFound `Category.NotFound`; `IsActive` null → `Validation` `Category.IsActiveRequired`; the errors of `Update`; then `repository.Update` and one `CommitAsync`; returns the key.
- `CreateListingHandler`, `UpdateListingHandler`, `PublishListingHandler`: a new **last** constructor parameter `ICategoryRepository`. Create: an inactive category → `Listing.CategoryInactive` before `Listing.Create`; a category not in the repository goes on to `Listing.Create` (`Listing.CategoryInvalid` for an undefined value, as before). Update: after 404 and 403, **only when the command changes the category**, an inactive target → `Listing.CategoryInactive`. Publish: after 404 and 403, **only for a draft**, an inactive category → `Listing.CategoryInactive` (a listing that is not a draft still gets `Listing.NotDraft`).

New fake `Fakes/FakeCategoryRepository.cs` (hand-written, no business logic):
- `Items`: a `Dictionary<Category, ListingCategory>` that starts with **all 5 categories active**, built with `ListingCategory.Create(key, displayName).Value` and the seed display names (`Manuale`, `Notițe`, `Electronice`, `Echipament de laborator`, `Altele`).
- `Deactivate(Category key)`: replaces the entry with `ListingCategory.Create(key, <same name>, null, isActive: false).Value`, so a test can make one category inactive without going through `Update`.
- `GetAllAsync` returns a snapshot ordered by key; `GetByKeyAsync` reads `Items` (null after `Items.Remove(key)`, the only way to reach the "not in the repository" branches); `Update` stores and increments a public `UpdateCount`.

Update the existing tests (do not delete any): in `CreateListingHandlerTests`, `UpdateListingHandlerTests` and `PublishListingHandlerTests` every `new ...Handler(...)` gets `new FakeCategoryRepository()` as the last argument. Only the constructor calls change: every existing publish, create and edit test keeps all its assertions.

Files (new): `Domain/Categories/ListingCategoryTests.cs`, `Features/Categories/UpdateCategory/UpdateCategoryHandlerTests.cs`, `Features/Categories/GetCategories/GetCategoriesHandlerTests.cs`; new tests in the three listing handler test files.

Tests to add:
- `ListingCategory`: `Create` valid (trimmed name and description, active by default), `isActive: false`, undefined key → `Category.NotFound`, blank description → `null`; display name `null`/`""`/`"   "` → `Category.DisplayNameEmpty`; exactly 100 characters → success, 101 → `Category.DisplayNameTooLong`; description of 500 → success, 501 → `Category.DescriptionTooLong`. `Update` changes the display name and the description (trimmed, same instance returned); each text error leaves the category unchanged; deactivate while another is active → inactive; reactivate → active; deactivating the last active one → Conflict `Category.LastActive`, still active and unchanged; an inactive category kept inactive while all the others are inactive → success (the rule is only about deactivating). `CheckAcceptsListings`: active → `null`, inactive → `Listing.CategoryInactive`.
- `UpdateCategoryHandler`: unknown key (`"Books"`, `"1"`) → `Category.NotFound`; a key removed from the fake → `Category.NotFound`; `IsActive` null → `Category.IsActiveRequired`; empty name → `Category.DisplayNameEmpty`; deactivating the last active one (the other 4 deactivated in the fake) → `Category.LastActive` and nothing saved; success (key in lower case) → values changed, returns the key, `UpdateCount` 1, `SaveCount` 1. In every failure `UpdateCount` 0, `SaveCount` 0, category unchanged.
- `GetCategoriesHandler`: the 5 categories in key order with display name, description and active flag, `Count` 5; an inactive one comes back with `IsActive` false; the fake is unchanged.
- `CreateListingHandler`: inactive category → `Listing.CategoryInactive`, nothing stored, `SaveCount` 0; undefined category → `Listing.CategoryInvalid` (the null branch).
- `UpdateListingHandler`: moving into an inactive category → `Listing.CategoryInactive`, listing unchanged, `UpdateCount` 0, `SaveCount` 0; moving into an active category → success; editing a listing that is already in an inactive category without changing the category → assert what the handler does (success, saved); moving to an undefined category → `Listing.CategoryInvalid`.
- `PublishListingHandler`: a draft in an inactive category → `Listing.CategoryInactive`, still `Draft`, `UpdateCount` 0, `SaveCount` 0, spam detector not called, no report; a draft whose category is not in the fake (`Items.Remove`) → published as before.

Rules for this feature:
- Every error assertion checks `Type`, `Code` and `Message` (exact text from the production code; build the length messages from `ListingCategory.MaxDisplayNameLength` and `ListingCategory.MaxDescriptionLength`).
- Never use real time.
- Then run the tests with coverage and generate the report as this skill says: every file in `Domain/Categories/`, the handlers in `Features/Categories/` and the three listing handlers are at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: My listings (Diana)

Production code (read it before generating):
- `Features/Members/GetMyListings/GetMyListingsQuery.cs`: `GetMyListingsQuery(Guid MemberId)`.
- `Features/Members/GetMyListings/GetMyListingsHandler.cs`: a query handler (no `IUnitOfWork`) with only `IListingRepository`. It reads `GetAllAsync`, keeps the listings whose `SellerId` is `MemberId`, in **every** status, orders by `CreatedAt` descending then `Id` ascending, and returns `GetMyListingsResponse(Items, Count)`. It never fails: the result is always a success.
- `Features/Members/GetMyListings/GetMyListingsResponse.cs`: `MyListingItem(Id, SellerId, Title, Category, Condition, Price, Status, CreatedAt)`, built by `MyListingItem.From(listing)`; `Price` is the `Amount` (a `decimal`).

File: `Features/Members/GetMyListings/GetMyListingsHandlerTests.cs` (new).

Arrange: use `FakeListingRepository` only (no time provider, no unit of work). Fixed, named ids: `MemberId`, `OtherMemberId`. A listing is `Listing.Create(sellerId, title, Category.Textbook, Condition.Good, price, createdAt).Value`, seeded straight into `FakeListingRepository.Items`. Reach every status in the domain before seeding: `Draft` is the new listing; `Active` after `Publish()`; `Reserved` after `Publish()` then `Reserve(BuyerId, createdAt)`; `Sold` after `Reserve` then `ConfirmSale(reservation.Id, createdAt)`; `Hidden` after `Publish()` then `Hide()`. A private static helper builds a listing in a given status; no `if` or loop in the tests, so use one helper per status or a `switch` expression inside the helper.

Tests to add:
- Only the member's listings: two listings of `MemberId` and one of `OtherMemberId` → `Items` has exactly the two ids, `Count` 2.
- Every status: one listing of the member in each of `Draft`, `Active`, `Reserved`, `Sold` and `Hidden` → all five come back, `Count` 5, and the `Status` of each item matches its listing.
- Order: three listings with three different `CreatedAt` (`Now`, `Now.AddHours(1)`, `Now.AddHours(2)`), seeded oldest first → returned newest first (check the ids with `Equal`).
- Tie: two listings with the same `CreatedAt` → ordered by `Id` ascending (`BeInAscendingOrder(i => i.Id)`).
- No listings for the member (the repository has only another member's listing) → success, empty `Items`, `Count` 0.
- Empty repository → success, empty `Items`, `Count` 0.
- Item content: one listing → `Id`, `SellerId`, `Title`, `Category`, `Condition`, `Price` (the amount), `Status` and `CreatedAt` all match the listing.

Rules for this feature:
- The handler is a query handler: in every test assert that the repository still has the same items after the call (same count, `UpdateCount` 0).
- Never use real time: every `createdAt` is a fixed `DateTimeOffset`.
- The handler has no failure branch: never invent an error test for it.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Generated images (Anca)

Production code (read it before generating):
- `Domain/Photos/Photo.cs`: new `IsGenerated`. New `CheckCanGenerate(listing, existingCount)` returns `null` when the listing can get a generated image, `Photo.ListingNotActive` (Validation) when its status is not `Active`, `Photo.ListingHasPhotos` (Conflict) when `existingCount > 0`. New `CreateGenerated(listing, existingCount, content, format, createdAt)`: the same two checks, then the size checks (`Photo.Empty` for 0 bytes, `Photo.TooLarge` above `MaxSizeBytes`), then a photo with `IsGenerated` true, `FileName` `{id}{format.Extension}`, `ContentType` from `format`, `SizeBytes`, `CreatedAt`. The size checks moved into a private `CheckSize` shared with `Create`; `Create` behaves exactly as before and its existing tests must still pass unchanged.
- `Domain/Photos/PhotoFormat.cs`: new `PhotoFormat.Svg` (`.svg`, `image/svg+xml`). `Detect` still never returns it (an SVG upload stays refused).
- `Features/Photos/GenerateListingImage/GenerateListingImageHandler.cs` (new command handler): listing not found → `Listing.NotFound`; `CheckCanGenerate` fails → that error, and the generator is **not** called; otherwise it reads the category (`ICategoryRepository.GetByKeyAsync`) and calls `IImageGenerator.GenerateAsync` with `ImagePrompt(Title, Description, Category, CategoryName)`, where `CategoryName` is the category's `DisplayName`, or `listing.Category.ToString()` when the repository has no row; `CreateGenerated` fails (empty or too large image) → that error, nothing saved; success → the file saved through `IPhotoStorage` (listing id, file name, the generator's bytes), the photo added, one commit, `PhotoResponse` with `IsGenerated` true.
- `Features/Photos/UploadPhoto/UploadPhotoHandler.cs` (changed): it now reads `GetByListingAsync`; generated photos do not count towards the limit (`existingCount` = real photos only). On success the generated photos are deleted from the repository before the commit and their files deleted from the storage after it. On failure nothing is deleted.
- `Features/Photos/PhotoResponse.cs`: new last field `IsGenerated`.

Files: `Domain/Photos/PhotoTests.cs` and `Domain/Photos/PhotoFormatTests.cs` (add the new tests, keep the old ones), `Features/Photos/GenerateListingImage/GenerateListingImageHandlerTests.cs` (new), `Features/Photos/UploadPhoto/UploadPhotoHandlerTests.cs` (add the new tests, keep the old ones).

Fakes: reuse `FakeListingRepository`, `FakePhotoRepository`, `FakePhotoStorage`, `FakeCategoryRepository`, `FakeUnitOfWork`, `FixedTimeProvider`. Add `Fakes/FakeImageGenerator.cs`: returns a fixed `GeneratedImage` set by the test (by default a small SVG byte array and `PhotoFormat.Svg`), records every `ImagePrompt` it got (`Prompts` list), so a test can check it was or was not called and with what.

Arrange: an `Active` listing is `Listing.Create(...).Value` then `Publish()`; a generated photo to seed is `Photo.CreateGenerated(activeListing, 0, bytes, PhotoFormat.Svg, now).Value`.

Tests to add:
- `Photo.CheckCanGenerate`: Active and 0 photos → null; Draft → `Photo.ListingNotActive` (Validation); Active with 1 photo → `Photo.ListingHasPhotos` (Conflict).
- `Photo.CreateGenerated`: success → every field (`IsGenerated` true, the format's content type and extension, size, time); Draft → `Photo.ListingNotActive`; existing photo → `Photo.ListingHasPhotos`; empty bytes → `Photo.Empty`; `MaxSizeBytes + 1` bytes → `Photo.TooLarge`; exactly `MaxSizeBytes` → success.
- `PhotoFormat.Svg`: extension and content type; `Detect` of an SVG text (`"<svg ..."` bytes) → null.
- `GenerateListingImageHandler`: unknown listing → `Listing.NotFound`, generator not called, nothing saved; Draft listing → `Photo.ListingNotActive`, generator not called; listing with a photo → `Photo.ListingHasPhotos`, generator not called; success → the prompt has the listing's title, description, category and the category's display name, the photo is saved with `IsGenerated` true, the storage has the generator's bytes under the photo's file name, one commit, the response matches; no category row in the repository → `CategoryName` is the enum name; the generator returns 0 bytes → `Photo.Empty`, nothing saved, no commit.
- `UploadPhotoHandler`: a listing with one generated photo, the seller uploads a PNG → success, the repository has only the new photo, the generated photo's file is gone from the storage and the new file is there, one commit; a listing with 5 real photos and 1 generated → `Photo.LimitReached` and the generated photo and its file are still there; a listing with 4 real photos and 1 generated → success (the generated one does not count), 5 real photos left.

Rules for this feature:
- Never use real time; never touch the disk (only the fakes).
- The existing tests of `Photo.Create`, `PhotoFormat` and `UploadPhotoHandler` keep all their assertions; only add tests, and only change an existing test if it no longer compiles (for example `PhotoResponse` got a new field).
- Then run the tests with coverage as this skill says: `Domain/Photos/Photo.cs`, `Domain/Photos/PhotoFormat.cs`, `GenerateListingImageHandler.cs` and `UploadPhotoHandler.cs` are at 100% for lines and branches.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Observability (Sara)
Production code (read it first): `Infrastructure/Observability/MetricsRegistry.cs` (`MetricsRegistry`, `EndpointBucket`, `PredictionBucket`), `MetricsSnapshot.cs` and `CampusMarketMeters.cs`.
- `MetricsRegistry(CampusMarketMeters meters, TimeProvider timeProvider, int reservoirCapacity = EndpointBucket.DefaultReservoirSize)` (1024). `RecordRequest(route, method, statusCode, durationMs)` counts per route and status; status >= 500 is an error. `MeasurePrediction(model, predict)` times the call with `timeProvider.GetTimestamp()` / `GetElapsedTime` (also when `predict` throws) and returns its value. `Snapshot()` orders routes and models by name (ordinal); `AverageMs` and `P95Ms` are rounded to 2 decimals; p95 is `samples[Math.Clamp(ceil(0.95 * n) - 1, 0, n - 1)]` over the reservoir.
- `EndpointBucket` and `PredictionBucket` are `internal` (visible through `InternalsVisibleTo`): a bucket is only created on the first record, so the empty state (`Requests`/`Count` 0, `AverageMs` and `P95Ms` null) is tested on a new bucket directly.

Why it is in the unit target although it is in `Infrastructure/`: it is pure in-memory logic with no database, HTTP or file access; every branch can be reached with a fake clock. This is the only exception to the `Infrastructure/` exclusion; `MetricsMiddleware`, `MetricsRouteExtensions` and `CampusMarketMeters` stay excluded.

New fake `Fakes/ManualTimeProvider.cs` (hand-written): `TimestampFrequency` = `TimeSpan.TicksPerSecond`, `GetTimestamp()` returns a counter that moves only with `Advance(TimeSpan)`. The `predict` lambda of a test calls `Advance`, so the measured duration is exact. Never use `Microsoft.Extensions.TimeProvider.Testing`.

File: `Infrastructure/Observability/MetricsRegistryTests.cs` (same folders as `src/`). Each test builds its own `using var meters = new CampusMarketMeters();`.

Tests to add:
- nothing recorded → `Endpoints` and `MlPredictions` empty; a new `EndpointBucket` → `Requests` 0, `Errors` 0, `AverageMs` and `P95Ms` null, no status codes; a new `PredictionBucket` → `Count` 0, `AverageMs` null.
- requests on two routes → one item per route in name order, `Requests` and `StatusCodes` (keys `"200"`, `"404"`, ...) per route.
- status 500 and 503 (`[Theory]`) → `Errors` 1; 200, 404 and 499 → `Errors` 0.
- durations 10, 20, ..., 100 ms recorded in a shuffled order → `AverageMs` 55, `P95Ms` 100; one request of 12.345 ms → `AverageMs` and `P95Ms` 12.35 (rounded to 2 decimals).
- capacity 3 and four requests (100, 10, 20, 30) → `Requests` 4 and `AverageMs` 40 over all four, `P95Ms` 30 over the kept values (the oldest, 100, was replaced).
- predictions of two models with durations from `ManualTimeProvider` → per model `Count` and `AverageMs`, in model name order; `MeasurePrediction` returns the value of `predict`; a `predict` that throws → the exception goes through and the call is still recorded.

Rules for this feature:
- Durations come only from `ManualTimeProvider` or are passed as numbers to `RecordRequest`: no real time, no `Stopwatch`, no `Thread.Sleep`, no `Task.Delay`.
- The "reservoir full" branch is tested with a small positive capacity (3); never pass 0 (the index is taken modulo the capacity).
- The endpoint `GET /api/v1/admin/metrics` and `MetricsMiddleware` are tested by the integration-testing skill, not here.
- `MetricsRegistry.cs` is in the coverage filter of this skill and must be at 100% lines and branches; `Domain/` and the handlers stay at 100%.

## Feature: Price text features (Diana)

Production code (read it before generating):
- `Infrastructure/ML/PriceTextFeatures.cs`: a static, pure class (no ML.NET, no API types). `Extract(string? title, string? description)` returns `PriceTextFeatures.Values(string Brand, float StorageGb, float CpuTier, float IsNewOrSealed)`. The same file is compiled into the training project as a linked file, so these tests also protect training. It reads `title + " " + description` after lowercasing and removing diacritics (`Normalize`, `internal`, visible through `InternalsVisibleTo`).
  - `Brand`: the first brand in the text (title first). One-word terms and two-word terms (`"audio technica"`, `"western digital"`) are looked up, two words first. A product line maps to its maker (`thinkpad` → `lenovo`, `macbook` → `apple`). No brand → `PriceTextFeatures.NoBrand` (`"none"`). `PriceTextFeatures.Brands` is the sorted, distinct list of the brands the model can see.
  - `StorageGb`: the **largest** number followed by `gb` or `tb`; `tb` counts as 1024 GB; a space before the unit is allowed (`512 GB`), a decimal part too (`1,5 TB` and `1.5 TB`); 0 when there is none. The number must not be glued to a letter or digit on the left, and the unit must not be followed by a letter or digit.
  - `CpuTier`: `i3`, `i5`, `i7`, `i9` and `Ryzen 3/5/7/9` (with or without a space) give 3, 5, 7 or 9; the highest one in the text wins; 0 when there is none. `i5` must not match inside another word, and `Ryzen 55` is not a tier.
  - `IsNewOrSealed`: 1 when `sigilat`, `nou`, `new` or `sealed` appears as a **whole word**, otherwise 0. Case and diacritics do not matter, and both Romanian forms of `ș`/`ț` (comma below and cedilla) fold to plain `s`/`t`.
- `Infrastructure/ML/PriceSuggester.cs`: `Suggest(...)` calls `PriceTextFeatures.Extract(title, description)` and fills `PriceInput.Brand`, `StorageGb`, `CpuTier`, `IsNewOrSealed` next to `Title`, `Description` (`null` becomes `""`), `Category` and `Condition`, then asks the pool. The result is `Math.Max(1, Math.Round(score, MidpointRounding.AwayFromZero))`.

Where the tests go and how they are named:
- `Infrastructure/ML/PriceTextFeaturesTests.cs` in `tests/CampusMarket.UnitTests` (same folders as `src/`), method names `Given_<state>_When_<action>_Then_<expected>`. Like `MetricsRegistry`, this file is the second exception to the `Infrastructure/` exclusion: it is pure logic, every branch is reachable without files, a database or a clock. Add `+*PriceTextFeatures.cs` to the `-filefilters` of the report; it must be at 100% lines and branches.
- `PriceSuggesterTests.cs` in `tests/CampusMarket.ML.Tests` (new). `PriceSuggester` needs a real `PredictionEnginePool<PriceInput, PricePrediction>` and a `MetricsRegistry`, so it cannot be a unit test with a fake, and it is not in the unit coverage report. Follow the naming of the other files of that project (a sentence, as in `SimilarListingsTests`), not `Given_When_Then`.

Tests to add in `PriceTextFeaturesTests` (use `[Theory]` with `[InlineData]` for the variants of one feature):
- Brand: a title with a known brand (`"Laptop Dell Latitude"` → `dell`); a product line (`"ThinkPad T480"` → `lenovo`, `"MacBook Air"` → `apple`); a two-word brand (`"Casti Audio Technica"` → `audio technica`); upper case and mixed case → same brand; no brand → `"none"`; two brands in the text → the first one wins; a brand only in the description → found; a brand as part of a longer word (`"dellicious"`) → `"none"`; every entry of `Brands` is lowercase and `"none"` is not in `Brands`.
- StorageGb: `"16GB"` → 16; `"512 GB"` → 512; `"1TB"` → 1024; `"2 TB"` → 2048; `"1,5 TB"` and `"1.5 TB"` → 1536; `"16GB RAM 512GB SSD"` → 512 (the largest, not the first); `"256GB"` in the title and `"1TB"` in the description → 1024; a number without the unit (`"Dell 15 inch 2020"`) → 0; the unit without a number (`"GB"`) → 0; the unit glued to a letter or digit (`"512GBs"`, `"x512GB"`) → 0.
- CpuTier: `"i3"`, `"i5"`, `"i7"`, `"i9"` → 3, 5, 7, 9 (also `"Core i7-8550U"`); `"Ryzen 5 3600"` and `"ryzen7"` → 5 and 7; `"i5 and i7"` → 7 (the highest); `"i2"`, `"i11"`, `"wifi5"` and `"Ryzen 55"` → 0; no processor → 0.
- IsNewOrSealed: each of `sigilat`, `nou`, `new`, `sealed` → 1; `"NOU"`, `"Sigilat"` → 1; the diacritic forms `"ȘIGILAT"` and `"ŞIGILAT"` (comma below and cedilla) and `"sigilat"` → 1 each; a word that only contains one of them (`"noua"`, `"nouă"`, `"renovat"`, `"newton"`, `"unsealed"`, `"anou"`) → 0; the word next to punctuation (`"Nou, in cutie"`, `"(sealed)"`, `"nou!"`) → 1; no such word → 0.
- Title and description are both read: the same feature in the title only, in the description only, and in both gives the same value; a brand in the title and another in the description → the title's brand; `StorageGb` takes the largest across both.
- Edge cases: `null` description, `""` description, `null` title and `""` title → success with `Brand` `"none"` and the three numbers 0; `Extract(null, null)` does not throw; whitespace only → all defaults.
- `Normalize`: `"ȘȚĂÂÎ şţ"` → `"staai st"` (both forms of `ș`/`ț` give `s`/`t`), and the result is lowercase. Test it only through `Extract` if `Normalize` is not visible.

Tests to add in `PriceSuggesterTests` (ML.Tests):
- Build the pool without a `.zip` file: register `AddPredictionEnginePool<PriceInput, PricePrediction>()` in a new `ServiceCollection` and give it an in-memory `ModelLoader` subclass (written in the test file) that returns a model made with a one-step ML.NET pipeline over `PriceInput`, so the score is a value copied from the column under test. Use four such models, one per column: `StorageGb` and `CpuTier` and `IsNewOrSealed` through an `Expression` (for example `x * 100 + 10`, so that 0 and 1 give different scores and are not hidden by the clamp to 1); `Brand` through a `MapValue` from brand to a number. `MetricsRegistry` is built with `new CampusMarketMeters()` and a `TimeProvider`.
- `Suggest("Laptop Lenovo ThinkPad i7 512GB sigilat", null, ...)` → the four columns reach the model as `lenovo`, 512, 7 and 1 (one assertion per column, so a swapped column is caught).
- A title with no feature (`"Scaun birou"`) → `none`, 0, 0, 0.
- The features come from the title **and** the description: the brand in the title and `"1TB"` in the description → both are filled.
- `description` `null` → no exception and the columns are filled from the title.
- The prediction is rounded away from zero (a score of 100.5 → 101) and never below 1 (a score of 0 or a negative one → 1).

Rules for this feature:
- Never use the real `price-regression.zip` in these tests, and never change `PriceTextFeatures.cs` to make a test pass. A test that finds a wrong value is reported, not fixed in production.
- Expected values are written as literals in the test (`1024f`, `"audio technica"`), never computed with the same code under test. Compare floats with `Should().Be(...)` on exact values: all of them are exact in `float`.
- Characters outside ASCII (`ș`, `ş`, `ț`, `ţ`, `ă`) are written as `\u` escapes in the test source (`"șigilat"` comma below, `"şigilat"` cedilla), so the file encoding cannot change the test.
- A new brand added to the list in `PriceTextFeatures.cs` needs one `[InlineData]` row; changing a rule (the `TB` factor, the CPU tiers) changes the model input, so the price model must be retrained (`docs/ml/price.md`).

## Feature: Demo seed (Diana)

Production code (read it before generating): `Infrastructure/Persistence/Seed/DemoListings.cs`, the static demo catalog. `BuildListings(List<Guid> sellerIds, DateTimeOffset now)` builds the 60 demo listings through the domain (`Listing.Create`, `Publish`, then `Reserve` and `ConfirmSale` for the Reserved and Sold ones). `BuildMembers(...)` is covered by the integration tests.

Why unit tests: Sonar adds the unit and the integration coverage together, and the integration tests start the real app, where the seller ids are always distinct and never empty. Three branches of `BuildListings` can only be reached by calling it directly with unusual ids: the empty-list check, the `continue` when `Listing.Create` fails, and the `return` when `Reserve` fails.

Where the tests go and how they are named:
- `Infrastructure/Persistence/Seed/DemoListingsTests.cs` in `tests/CampusMarket.UnitTests` (same folders as `src/`), method names `Given_<state>_When_<action>_Then_<expected>`. It is the third exception to the `Infrastructure/` exclusion, with `MetricsRegistry` and `PriceTextFeatures`: add `+*DemoListings.cs` to the `-filefilters` of the report, and keep it at 100% lines and branches.

Tests to add (one `[Fact]` each, a fixed `Now`, never `DateTimeOffset.UtcNow`):
- An empty `sellerIds` list → `ArgumentException` with `ParamName` `"sellerIds"`.
- A list with only `Guid.Empty` → `Listing.Create` fails for every template (`Listing.SellerEmpty`), so the result is empty.
- The same id twice (`[id, id]`) → the buyer is the seller, `Reserve` fails (`Reservation.OwnListing`), so the result has `DemoListings.ListingCount` listings and every one is `Active`.

Rules for this feature:
- Never change `DemoListings.cs` to make a test pass, and do not duplicate the integration checks (database content, paging) here.
- Use `DemoListings.ListingCount` for the expected count, never the literal 60.
- Do not add a unit test for `BuildMembers`: its failure branch (empty hash) is already covered by the integration tests with `EmptyHashPasswordHasher`.

## Feature: Price feedback export (Carina)

Production code (written by hand, read it before generating):
- `Features/Admin/PriceFeedback/PriceFeedbackHandler.cs`: a query handler (no `IUnitOfWork`) with `ISaleRecordRepository` and `IListingRepository`. It returns `PriceFeedbackResponse(RowCount, Csv)` and never fails. The CSV starts with `PriceFeedbackQuery.Header` (`Title,Description,Category,Condition,Price,IsSpam,TemplateId`, the same header as `ml/data/external.csv`), lines end with `\n` (LF). One row per `SaleRecord`, ordered by `SoldAt` then `Id`: `Title` and `Description` from the **listing** (the description `null` becomes `""`), `Category` and `Condition` from the **record** (the values at the time of the sale), `Price` = `SoldPrice`, `IsSpam` = `false`, `TemplateId` = `PriceFeedbackQuery.TemplateId` (`feedback`). A record whose listing is no longer in the repository is skipped and not counted in `RowCount`.
- Text fields (`Title`, `Description`, `TemplateId`) are always in double quotes and an embedded `"` is doubled; commas and new lines stay inside the quotes. `Price` uses the invariant culture and the format `0.##`: `100` for whole lei, `99.5` for a fractional price, never a thousands separator.
- `ml/CampusMarket.ML.Training/Evaluation/FeedbackEvaluation.cs`: `Run(mlContext, feedbackCsvPath, modelsDir)` does nothing when the file is missing; otherwise it loads the CSV with quoting and multiline values, drops the spam rows, prints `Feedback rows: 0, nothing to evaluate.` when none is left, or loads `price-regression.zip` from `modelsDir` and prints a table with `n`, MAE and R².

File: `Features/Admin/PriceFeedback/PriceFeedbackHandlerTests.cs` (new). ML tests: `tests/CampusMarket.ML.Tests/FeedbackEvaluationTests.cs` (new), named like the other files of that project (a sentence, not `Given_When_Then`).

Fakes: the existing `FakeSaleRecordRepository` and `FakeListingRepository`, used as they are. A listing is `Listing.Create(SellerId, title, category, condition, price, Now, description).Value` seeded straight into `FakeListingRepository.Items`; a record is `SaleRecord.Create(listing.Id, category, condition, suggested, sold, soldAt).Value` added to `FakeSaleRecordRepository.Items`. A deleted listing is a record whose `ListingId` is not in `FakeListingRepository.Items`.

Tests to add (`PriceFeedbackHandler`):
- no sales → success, `Csv` is exactly the header followed by `\n`, `RowCount` 0.
- the first line of `Csv` equals `PriceFeedbackQuery.Header` and the literal `"Title,Description,Category,Condition,Price,IsSpam,TemplateId"` (so a change of the constant is caught).
- one sale → `Csv` is exactly the header and one row: the listing's title and description, the record's category and condition (give the record a category and condition **different** from the listing's, so a swap is caught), `SoldPrice` (not `SuggestedPrice` and not the listing price), `false`, `"feedback"`; `RowCount` 1.
- a listing without a description → `""` in the description column.
- a record whose listing no longer exists → skipped: only the other rows, `RowCount` counts only them; with only such records → only the header, `RowCount` 0.
- escaping: a title with `"`, a title with a comma, a description with a new line → each written exactly as the expected quoted text (`"Laptop 15"" Dell"`, `"Casti, sigilate"`, `"Linia 1\nLinia 2"`).
- order: records seeded in the wrong order → rows by `SoldAt`; two records with the same `SoldAt` → ordered by the record `Id` (build the expected order from the arranged ids).
- culture: `99.5` and `1234.5` sold prices written as `99.5` and `1234.5` while `CultureInfo.CurrentCulture` and `CurrentUICulture` are `ro-RO` (where the default would be `99,5`); a whole price `100` is written as `100`.
- the repositories are unchanged after every call (same items, `UpdateCount` 0).

Tests to add (`FeedbackEvaluation`, ML.Tests):
- the CSV file does not exist → no exception and nothing is written to the console.
- a small CSV written to a temporary folder (header from `PriceFeedbackQuery.Header`, 3 rows, one description with a new line inside quotes, one row with a quote doubled, one spam row) with the real `ml/models` folder → no exception; the output contains `Feedback evaluation`, the table header `| n | MAE (lei) | R² |` and a row starting with `| 2 |` (the spam row is not counted), so the multiline row was read as one row.
- a CSV with only the header → `Feedback rows: 0, nothing to evaluate.`

Rules for this feature:
- Culture-independent: the tests must pass on a Romanian Windows. Set the culture in the test with a small private `IDisposable` class in the test file that saves `CultureInfo.CurrentCulture` and `CurrentUICulture`, sets `ro-RO` and restores both in `Dispose` (`using var _ = new CultureScope("ro-RO");`); never change the culture without restoring it. Expected prices are literals with a dot (`"99.5"`), never built with `ToString()` of the current culture.
- Expected CSV text is a literal string (with `\n`, never `Environment.NewLine`: the handler writes LF on every OS).
- The handler is a query: no unit of work, assert the repositories are unchanged.
- The ML tests that capture the console use `Console.SetOut` with a `StringWriter` and restore the old writer (an `IDisposable` in the test class); the class is in an xUnit collection with `DisableParallelization = true`, so no other test writes into the captured output. Temporary files go under `Path.GetTempPath()` in a new folder that the test deletes; never write into `ml/data/`.
- Never change `src/` or `ml/` to make a test pass; if the code behaves differently from this list, stop and report it.
- Then run the tests with coverage as this skill says: `PriceFeedbackHandler.cs` is at 100% for lines and branches; `Domain/` and the handlers stay at 100%.

## Feature: Publish with photos and draft (Diana)

Production code (read it before generating; commit 9371271):
- `Domain/Listings/Listing.cs`: `RequireClassification(Category?, Condition?)` returns `Listing.CategoryRequired` (category null, checked first) or `Listing.ConditionRequired` (condition null), both `Validation`; and `IsVisibleTo(Guid? requesterId, bool isAdmin)`: true unless the listing is `Draft`, in which case only an Admin or the seller sees it (a `null` requester never).
- `Features/Listings/CreateListing/CreateListingHandler.cs`, `UpdateListing/UpdateListingHandler.cs`, `SuggestPrice/SuggestPriceHandler.cs`: `Category` and `Condition` of the command/query are now nullable and go through `RequireClassification` before the category lookup, the validation and the price rules; in `UpdateListingHandler` this comes after the not found (`Listing.NotFound`) and not owner (`Listing.NotOwner`) checks, which run first.
- `Features/Listings/GetListingById/GetListingByIdHandler.cs` and `Features/Photos/GetListingPhotos/GetListingPhotosHandler.cs`: the query carries `RequesterId` (nullable) and `IsAdmin`; a missing listing, or a draft not visible to the requester, gives `Listing.NotFound` (NotFound). `GetListingPhotos` orders by `Position`, then `CreatedAt`.
- `Features/Photos/ReorderPhotos/ReorderPhotosHandler.cs`: `ReorderPhotosCommand(RequesterId, IsAdmin, ListingId, PhotoIds)`. Order of the checks: listing missing, or draft not visible → `Listing.NotFound`; requester is not the seller (an Admin too) → `Photo.NotSeller` (`Forbidden`); `PhotoIds` is not exactly the set of photo ids of the listing (wrong count, repeated, unknown or missing id) → `Photo.OrderMismatch` (`Validation`), nothing saved. On success: `Photo.MoveTo(i)` and `photos.Update` for each id, one `CommitAsync`, and the response lists the photos in the new order.
- `Features/Photos/UploadPhoto/UploadPhotoHandler.cs`: the new photo gets `Position` = max `Position` of the non-generated photos + 1 (0 when there are none; generated photos are ignored). `storage.SaveAsync` throwing (anything but `OperationCanceledException`) → `Photo.StorageFailed` (`Validation`).

Files (add to the existing test classes, never a second class for the same handler): `Domain/Listings/ListingTests.cs`, `Features/Listings/CreateListing/CreateListingHandlerTests.cs`, `UpdateListing/UpdateListingHandlerTests.cs`, `SuggestPrice/SuggestPriceHandlerTests.cs`, `GetListingById/GetListingByIdHandlerTests.cs`, `Features/Photos/GetListingPhotos/GetListingPhotosHandlerTests.cs`, `Features/Photos/UploadPhoto/UploadPhotoHandlerTests.cs`, and `Features/Photos/ReorderPhotos/ReorderPhotosHandlerTests.cs` (new). In `FakePhotoRepository` count `Update` in `UpdateCount`; the storage fake must be able to throw.

Tests to add:
- `RequireClassification`: both set → success with the values; category null → `Listing.CategoryRequired`; condition null → `Listing.ConditionRequired`; both null → `Listing.CategoryRequired` (category first).
- `IsVisibleTo`: `Draft` seller → true; `Draft` other member → false; `Draft` anonymous (`null`) → false; `Draft` Admin → true; `Active` anonymous → true.
- CreateListing, UpdateListing, SuggestPrice: null category → `Listing.CategoryRequired`; null condition → `Listing.ConditionRequired`; nothing is saved (`AddCount` and `UpdateCount` 0, no commit).
- GetListingById and GetListingPhotos: a `Draft` is returned to its seller and to an Admin; for another member and for an anonymous requester → `Listing.NotFound` (NotFound). An `Active` listing is returned to anyone. GetListingPhotos: photos come back in `Position` order, ties by `CreatedAt` (seed them out of order).
- ReorderPhotos: success (three photos, ids reversed) → each photo has its new `Position`, the response is in the new order, `UpdateCount` 3, one commit; the seller of a `Draft` can reorder; unknown listing → `Listing.NotFound`; a `Draft` of another member → `Listing.NotFound`; another member (and an Admin who is not the seller) on an `Active` listing → `Photo.NotSeller`, nothing saved; `PhotoIds` with a missing id, with an unknown id, with a repeated id, and an empty list on a listing with photos → each `Photo.OrderMismatch`, positions unchanged, no commit; an empty list on a listing without photos → success, empty `Items`.
- UploadPhoto: first photo → `Position` 0; with photos at 0 and 1 → the new one gets 2; with a gap (0 and 5) → 6; a generated photo is ignored in the computation; storage throws `IOException` → `Photo.StorageFailed`, the photo is not added and not committed; storage throws `OperationCanceledException` → it is not turned into `Photo.StorageFailed` (assert that the exception propagates).

Rules for this feature:
- Use fixed ids (`SellerId`, `OtherMemberId`, `AdminId`) and a fixed `DateTimeOffset`; never real time.
- Every failure test asserts the error code and the `ErrorType`, and that the repositories and the unit of work were not changed.
- Query handlers (`GetListingById`, `GetListingPhotos`) never commit.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.

## Feature: Listing expiry (Diana)

Production code (read it before generating; commit fa16910):
- `Domain/Listings/Listing.cs`: `Publish(expiresAt)` sets `ExpiresAt` (and `Version` + 1); `Expire(currentTime)` returns `true` only for an `Active` listing with `ExpiresAt` not null and `ExpiresAt <= currentTime`: it becomes `Expired`, `Version` + 1; otherwise `false`, nothing changes (Draft, Reserved, Sold, Hidden never expire). `Renew(expiresAt)`: an `Expired` listing becomes `Active` with the new `ExpiresAt`; any other status gives `Listing.NotExpired` (`Validation`, "Only an expired listing can be renewed.").
- `Features/Listings/ExpireListings/ExpireListingsHandler.cs`: `ExpireListingsCommand()` (a system job). Gets every listing and calls `Expire(now)` with the `TimeProvider`; for each expired one: `repository.Update` and a `Notification` (`NotificationType.ListingExpired`, to the seller, text `Your listing {Title} expired`, related id = listing id). No expired listing: returns 0, no commit. Otherwise one `CommitAsync`, and only after it one email per seller (`IEmailSender`, link `{FrontendOptions.BaseUrl}/my/listings`). A seller not found: no email. An email that throws (anything but `OperationCanceledException`) is swallowed: the expiry stays committed and the other emails are still sent. Returns the number expired.
- `Features/Listings/RenewListing/RenewListingHandler.cs`: `RenewListingCommand(Id, RequesterId)`. Order of the checks: missing listing gives `Listing.NotFound`; requester is not the seller (an Admin too) gives `Listing.NotOwner` (`Forbidden`); an `Expired` listing in an inactive category gives the error of `CheckAcceptsListings()`; then `Renew(now + ListingOptions.ExpiryDays)`, so any other status gives `Listing.NotExpired` (the category is not checked for it). Success: `Update`, one commit, returns the id.

Files: `Domain/Listings/ListingTests.cs` (add), `Features/Listings/ExpireListings/ExpireListingsHandlerTests.cs` and `Features/Listings/RenewListing/RenewListingHandlerTests.cs` (new); update `PublishListingHandlerTests` for the new `ExpiresAt`. Fakes: a fake `TimeProvider` (fixed `Now`, can be moved), a fake `IEmailSender` that records messages and can throw, a notification repository fake that counts `Add`, the member and category fakes, `IOptions<ListingOptions>` with `ExpiryDays` 30.

Tests to add:
- `Publish`: `ExpiresAt` equals the given value, `Version` + 1.
- `Expire`: `Active` at `ExpiresAt` minus 1 second gives `false`, unchanged; at exactly `ExpiresAt` gives `true`, `Expired`, `Version` + 1; `Draft`, `Reserved`, `Sold`, `Hidden` and `Expired` past `ExpiresAt` (`[Theory]`) give `false`, status and `Version` unchanged; a second call returns `false`.
- `Renew`: `Expired` becomes `Active` with the new `ExpiresAt` (different from the old one), `Version` + 1; `Draft`, `Active`, `Reserved`, `Sold`, `Hidden` (`[Theory]`) give `Listing.NotExpired` with the exact message, nothing changed.
- ExpireListings: no listings: 0, `SaveCount` 0, no email. One `Active` past `ExpiresAt`, one `Active` not yet expired, one `Reserved` and one `Draft` past `ExpiresAt`: result 1, only that one is `Expired`, `UpdateCount` 1, one notification (type, seller id, text, listing id), one commit, one email to the seller (address, subject, the body contains the title and the link). Two expired: 2, one commit, two notifications, two emails. The clock comes from the fake `TimeProvider` (move it to cross `ExpiresAt`). The email sender throws: the result is still 1, the listing is `Expired`, the commit happened and the notification was added; with two sellers and the first email throwing, the second email is still sent. A seller that does not exist: expiry committed, no email, no exception. The email is sent after the commit (record the order in the fakes).
- RenewListing: success: listing `Active`, `ExpiresAt` = `Now` + 30 days, `Version` + 1, `UpdateCount` 1, one commit, returns the id. Unknown id: `Listing.NotFound`. Another member and an Admin: `Listing.NotOwner`. An `Active` listing (owner): `Listing.NotExpired`. An `Expired` listing in a deactivated category: the category error, listing still `Expired`. An `Active` listing in a deactivated category: `Listing.NotExpired` (not the category error). Every failure: nothing saved.

Rules for this feature:
- Use fixed ids (`SellerId`, `OtherMemberId`, `AdminId`) and a fixed `DateTimeOffset`; never real time.
- Every failure test asserts `ErrorType`, the exact `Error.Code` and `Error.Message`, and that the repositories and the unit of work were not changed.
- If a test fails because the code behaves differently from this list, stop and report it; do not change `src/`.
