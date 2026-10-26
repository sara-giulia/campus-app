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
- `IUnitOfWork.SaveChangesAsync(CancellationToken ct = default)` returns `Task<int>`.
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

