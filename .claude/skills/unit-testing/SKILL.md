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

Stryker.NET left mutants alive in `Domain/` and the handlers. The goal is a mutation score of 100%: add or extend unit tests so that each mutant below makes at least one test fail. Production code does not change.

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

### 6. Equivalent mutants (2): no test
`Listing.cs:14` (`Title = string.Empty`) and `Member.cs:8` (`Name = string.Empty`) are always overwritten by the factories, so no test can see the change. They are marked in the code with `// Stryker disable once` by hand. Do not write tests for them.

Rules for this feature:
- Keep every existing test and its name. In existing tests, only add the assertions described in points 1 and 2; everything else is a new test.
- Never change production code.
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