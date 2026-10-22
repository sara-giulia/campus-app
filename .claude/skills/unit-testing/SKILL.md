---
name: unit-testing
description: Generates xUnit unit tests with FluentAssertions for the domain and for CQRS handlers of CampusMarket. Use when adding or changing domain classes or handlers, or when coverage is below 100%.
---

# Unit testing skill

## Goal
Generate unit tests for `Domain/` and for every handler, reaching 100% coverage with assertions that check behaviour. Never edit production code to make a test pass.

## Tools
- xUnit
- FluentAssertions, pinned to `7.*` (Apache 2.0). Never use version 8 or newer.
- coverlet.collector for coverage, ReportGenerator for the HTML report
- No mocking library (no Moq, no NSubstitute)

## Test project
`tests/CampusMarket.UnitTests`. Test folders mirror production folders:
- `Domain/Listings/ListingTests.cs`, `Domain/Listings/MoneyTests.cs`, `Domain/Listings/ReservationTests.cs`
- `Domain/Members/MemberTests.cs`, `Domain/Members/EmailTests.cs`
- `Domain/Common/ResultTests.cs`, `Domain/Common/ErrorTests.cs`
- `Features/Listings/CreateListing/CreateListingHandlerTests.cs` (and the same for every handler)
- `Fakes/` for hand-written fakes

## Naming
- Class: `<ClassUnderTest>Tests`
- Method: `Given_<state>_When_<action>_Then_<expected>`
  Example: `Given_ActiveListing_When_SellerReservesOwnListing_Then_ReturnsValidationError`

## Rules
1. Arrange, Act, Assert, with one blank line between the blocks. No "// Arrange" comments needed.
2. One behaviour per test.
3. No logic inside tests: no `if`, no loops, no `try/catch`.
4. Use `[Theory]` with `[InlineData]` for input variants (for example invalid titles, invalid prices).
5. Cover every success path AND every failure branch of every method that returns a `Result` (check the error code and the error type).
6. Fakes are written by hand in `Fakes/`: an in-memory fake repository, a fake unit of work that counts `SaveChangesAsync` calls, and a fake `TimeProvider` with a fixed time. Never use `DateTime.Now` or `DateTimeOffset.UtcNow` in tests.
7. Tests never depend on each other or on execution order. No static mutable state.
8. Assert on behaviour: the returned `Result`, the state of the object, the data in the fake repository. Do not assert on implementation details.
9. Query handler tests also check that the state did not change (no save call).
10. Command handler tests check that the unit of work was saved after a successful change, and not saved on failure.
11. Private members are never tested directly; test them through the public API.
12. The old week 2 tests in `Domain/` are replaced by the generated ones (delete them, do not edit them).

## Never do
- Never use a mocking library.
- Never use `Thread.Sleep` or real time.
- Never put two behaviours in one test.
- Never change the production code to make a test pass.
- Never leave a test without an assertion.
- Never share one object between tests.

## Example (domain)
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
        result.Value!.Status.Should().Be(ListingStatus.Draft);
        result.Value.Version.Should().Be(1);
    }
}
```

## Example (handler)
```csharp
public class CreateListingHandlerTests
{
    [Fact]
    public async Task Given_ValidCommand_When_Handling_Then_AddsListingAndSaves()
    {
        var repository = new FakeListingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateListingHandler(repository, unitOfWork, new FakeTimeProvider());
        var command = new CreateListingCommand(Guid.NewGuid(), "Algebra book", Category.Textbook, Condition.Good, 25m);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.GetAll().Should().HaveCount(1);
        unitOfWork.SaveCount.Should().Be(1);
    }
} 
```
The handler example is provisional: adapt names and constructor to the real handlers once they exist.