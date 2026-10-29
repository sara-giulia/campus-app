# Mutation testing (Stryker.NET)

Coverage shows which lines the tests run. Mutation testing shows whether the tests really check those lines: Stryker changes the code on purpose (for example `>` into `>=`) and runs the unit tests again. If a test fails, the mutant is **killed**; if every test still passes, the mutant **survived**, and a test is missing or too weak.

## How to run it

`tests/CampusMarket.UnitTests/stryker-config.json` limits the mutations to `Domain/` and the handlers (`Features/**/*Handler.cs`), the same files as the coverage target. A first run on the whole API scored 37.55%, because endpoints, `Infrastructure/` and `Program.cs` are not unit-tested on purpose (they are covered by the integration tests), so all their mutants counted as survivors.

The HTML report is in `mutation-report/mutation-report.html`. `StrykerOutput/` is in `.gitignore`.

## Result

| | Count |
|---|---|
| **Mutation score** | **82.13%** |
| Killed | 193 |
| Survived | 42 |
| No coverage | 0 |
| Compile error (not counted) | 3 |
| Ignored (not run, not counted) | 95 |

Score = killed / (killed + survived + no coverage) = 193 / 235.

No mutant has "no coverage", which matches the 100% line and branch coverage. The 42 survivors fall into five groups.

## Survivors that point to a missing test

| File:Line | Code | What Stryker changed | Why no test caught it | Missing test |
|---|---|---|---|---|
| `Listing.cs:137` | `title.Trim().Length > MaxTitleLength` | `>` into `>=` | No test uses a title of exactly 100 characters, the limit. | A title of exactly `MaxTitleLength` (100) characters is accepted. |
| `Member.cs:30` | `trimmedName.Length < 2` | `<` into `<=` | No test uses a name of exactly 2 characters. | A name of exactly 2 characters is accepted. |
| `Member.cs:30` | `trimmedName.Length > 100` | `>` into `>=` | No test uses a name of exactly 100 characters. | A name of exactly 100 characters is accepted. |
| `UpdateListingHandler.cs:27` | `repository.Update(listing);` | statement removed | The handler changes the listing loaded from the fake, and the fake keeps that same object, so the test sees the new values even without `Update`. | `FakeListingRepository` counts `Update` calls; the success test checks it was called once. |
| `UpdateMemberHandler.cs:35` | `repository.Update(member);` | statement removed | Same reason, with `FakeMemberRepository`. | `FakeMemberRepository` counts `Update` calls; the success test checks it was called once. |
| `LoginHandler.cs:21` | `email.IsFailure \|\| string.IsNullOrEmpty(command.Password)` | `\|\|` into `&&` | Without the early return, the later checks (unknown email, wrong password) still return the same `Auth.InvalidCredentials`, so the result does not change. | For an invalid email or an empty password, check that the password hasher is never called (`FakePasswordHasher` counts `Verify` calls). |

## Survivors that are accepted

**Error messages (34 mutants).** Stryker replaces the text of an error message with `""`. The tests check `Error.Code` and `Error.Type`, never the message, which is a team decision (the integration-testing skill says the same): the code is the contract, the message is for people and may change. The same goes for the two exception messages in `Result.cs`, where the tests check only the exception type.

| File | Lines |
|---|---|
| `Listing.cs` | 34, 76, 88, 91, 94, 109, 129, 132, 136, 139 |
| `GetListingsHandler.cs` | 15, 19, 23 |
| `RegisterHandler.cs` | 21, 37, 42 |
| `CreateMemberHandler.cs` | 26, 31 |
| `UpdateMemberHandler.cs` | 19, 27 |
| `Email.cs` | 17, 23 |
| `Result.cs` | 19, 21 (exception messages) |
| `CreateListingHandler.cs` | 28 |
| `UpdateListingHandler.cs` | 19 |
| `DeleteListingHandler.cs` | 19 |
| `DeleteMemberHandler.cs` | 19 |
| `GetListingByIdHandler.cs` | 17 |
| `GetMemberByIdHandler.cs` | 17 |
| `LoginHandler.cs` | 16 |
| `Member.cs` | 68 |
| `Money.cs` | 14 |
| `Reservation.cs` | 35 |

If we decide messages matter, one assertion on `Error.Message` per error is enough to kill them.

**Default values (2 mutants, equivalent).** `Listing.cs:14` (`Title = string.Empty`) and `Member.cs:8` (`Name = string.Empty`) became `"Stryker was here!"`. The factories always overwrite these values, so no test can see the difference.

## Next week

1. Boundary tests for `Listing.cs:137` and `Member.cs:30` (3 tests).
2. An `Update` counter in both fake repositories and one assertion in each update success test (2 tests).
3. A `Verify` counter in `FakePasswordHasher` and the login test for the early return (1 test).
4. Decide whether error messages are part of the contract.

The new tests go through the unit-testing skill, as usual.
