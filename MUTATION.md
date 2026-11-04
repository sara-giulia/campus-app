# Mutation testing (Stryker.NET)

Coverage shows which lines the tests run. Mutation testing shows whether the tests really check those lines: Stryker changes the code on purpose (for example `>` into `>=`) and runs the unit tests again. If a test fails, the mutant is **killed**; if every test still passes, the mutant **survived**, and a test is missing or too weak.

## How to run it

```
dotnet tool install -g dotnet-stryker
cd tests/CampusMarket.UnitTests
dotnet stryker
```


`tests/CampusMarket.UnitTests/stryker-config.json` limits the mutations to `Domain/` and the handlers (`Features/**/*Handler.cs`), the same files as the coverage target. Endpoints, `Infrastructure/` and `Program.cs` are covered by the integration tests, not by unit tests.

The HTML report is in `mutation-report/mutation-report.html`. `StrykerOutput/` is in `.gitignore`.

## Result

| | Week 4 | Week 5 |
|---|---|---|
| **Mutation score** | 82.13% | **100%** |
| Killed | 193 | 326 |
| Survived | 42 | 0 |
| No coverage | 0 | 0 |

At the start of week 5, the current `main` had 58 survivors, because new handlers had been merged (PublishListing, ReserveListing, CancelReservation, GetMyReservations) and the ownership checks added new error messages.

## How the survivors were killed

The tests were generated with the unit-testing skill (section "Feature: Surviving mutants"). Production code did not change.

| Group | Mutants | Test added |
|---|---|---|
| Error messages replaced with `""` | 47 | every failure test also checks `Error.Message`; the `Result` tests check the exception message |
| `repository.Update(...)` removed in UpdateListing, UpdateMember, PublishListing, CancelReservation | 4 | the fake repositories count `Update` calls; each success test checks the count is 1 |
| Length limits (`>` into `>=`, `<` into `<=`) in `Listing` and `Member` | 3 | a title of exactly 100 characters, a name of exactly 2 and of exactly 100 characters are accepted |
| `\|\|` into `&&` in `LoginHandler` | 1 | an empty password with a valid email returns `Auth.InvalidCredentials` without calling the password hasher |
| `ThenBy` into `ThenByDescending` in `GetMyReservationsHandler` | 1 | two reservations made at the same time come back ordered by id |

## Equivalent mutants

Two mutants cannot be killed by any test, because they do not change the behaviour. They are marked in the code with `// Stryker disable once String` and the reason:

- `Listing.cs`: `Title = string.Empty`
- `Member.cs`: `Name = string.Empty`

The factories always set `Title` and `Name`, so the default value is never observable.