# PROMPTS.md

One entry per run of a skill. Everyone uses the same format. Newest entry at the bottom.
The professor grades the before and after, so write what went wrong and what you changed in the skill.

## Format (copy this block for every run)

### <YYYY-MM-DD> · <who> · <skill name> · run <number>

- **What I asked:** the exact prompt given to Claude Code
- **Skill that ran:** name and the commit of the SKILL.md used
- **Result:** build (errors and warnings), tests, Postman (for example 36/36), what was wrong
- **What I changed in the skill and why:** the rule that was added or fixed (write "nothing" if the first run was good)
- **Commits:** the skill commit and the generated-code commit (separate commits)

---

## Entries

### 2026-10-22 · Carina Coman · cqrs · review fixes

- **What I asked:** review by a teammate (CreateListing example built in the real project: 0 warnings, 90 tests pass)
- **Skill that ran:** cqrs (manual review, no generation yet)
- **Result:** four problems found in SKILL.md
- **What I changed in the skill and why:**
  1. "Never change Domain/" contradicted the new `IUnitOfWork.cs`; now only existing files are protected.
  2. Added `dotnet test` to the final check, because the existing tests prove the behaviour did not change.
  3. Added an UpdateListing example, the only slice that maps two error types (404 and 400).
  4. Wrote the `HandleAsync` signature as a decision (`Task<Result<T>>`, `ct` without default), different from the course.
- **Commits:** skill ec0b222 ("Fix cqrs skill after review: Domain rule, test check, update example, handler signature decision"); first version 617d8c2

### 2026-10-22 · Carina Coman (skill), Sara Matasel (run) · cqrs · run 1

- **What I asked:** Use the cqrs skill in .claude/skills/cqrs/SKILL.md to refactor all 10 slices in src/CampusMarket.Api/Features (Listings and Members) into commands, queries and handlers. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.
- **Skill that ran:** cqrs, commit a90b234 (written by Carina, run by Sara from her Claude Code account, on a shared call)
- **Result:** all 10 slices now have a command or query and a handler. Build: 0 errors, 0 warnings. Tests: 90/90 pass. Postman: 36/36 requests, 68/68 assertions. No endpoint uses a repository or IUnitOfWork; query handlers do not use IUnitOfWork; Domain/, tests/ and postman/ unchanged (only the new IUnitOfWork.cs).
- **What I changed in the skill and why:** nothing, the first run followed the skill. The skill was already improved before this run (see "review fixes" above).
- **Commits:** skill a90b234; generated code 9c099c5

### 2026-10-22 · Diana · unit-testing · skill v1

- **What I asked:** no generation yet; I only wrote the first version of the unit-testing skill (xUnit, FluentAssertions 7.*, naming, rules, never-do list, examples)
- **Skill that ran:** none yet (SKILL.md written by hand, commit 32a93f7)
- **Result:** .claude/skills/unit-testing/SKILL.md v1 created; the handler example is provisional until the handlers are on main
- **What I changed in the skill and why:** nothing, this is the first version
- **Commits:** skill 32a93f7; generated code: none yet

### 2026-10-23 · Diana · unit-testing · skill v2

- **What I asked:** no generation yet; I compared my v1 skill with the real code (cqrs skill, csproj, CI workflow) and rewrote it
- **Skill that ran:** none yet (SKILL.md edited by hand)
- **Result:** the v1 handler example did not match the real handlers: wrong time provider name, wrong constructor, wrong repository calls; the CI collects coverage differently from my commands
- **What I changed in the skill and why:** (1) real handler and repository signatures from the cqrs skill; (2) a `RejectAdd` switch on the fake repository, because the domain generates the Id and the Conflict branch is otherwise unreachable; (3) query handlers have no unit of work, so they are checked by repository state instead of SaveCount; (4) coverage collected with `coverlet.runsettings`, like the CI; (5) a test matrix per handler so every branch is covered
- **Commits:** skill 096e397; generated code: none yet

### 2026-10-24 · Diana · unit-testing · skill v3 (review fixes)

- **What I asked:** review by a teammate (no generation yet)
- **Skill that ran:** none yet (SKILL.md edited by hand after review)
- **Result:** six review points; five needed changes in the repo
- **What I changed in the skill and why:** one explicit exclusion list shared with the README and the report limited to Domain/ and handlers; rule to commit coverage-report/; Result.Value and Result.Error documented as throwing InvalidOperationException (and a test rule for both branches); no `!` on Result.Value; examples keep the blank lines between Arrange, Act and Assert
- **Commits:** skill 342e693; generated code: none yet

### 2026-10-24 · Sara Matasel · efcore-sqlite-repositories · run 1

- **What I asked:** Use the efcore-sqlite-repositories skill in .claude/skills/efcore-sqlite-repositories/SKILL.md to replace the in-memory repositories with EF Core + SQLite. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.
- **Skill that ran:** efcore-sqlite-repositories v1, commit b3ebc49 (a short first draft)
- **Result:** build OK, 90/90 tests, but: it edited `tests/.../Api/TestApi.cs` to make the old tests pass; changed the `AddCampusMarket` signature and hard-coded the connection string; created the database inside `MapCampusMarket`; left the in-memory repositories as dead code; put all mapping in one `OnModelCreating` with no configuration classes, no `ValueGeneratedNever`, no max lengths and no concurrency token; `GetAllAsync` without `AsNoTracking`; no `*.db` in `.gitignore`. The generated code was discarded.
- **What I changed in the skill and why:** see v2.
- **Commits:** skill b3ebc49; generated code: discarded

### 2026-10-24 · Sara Matasel · efcore-sqlite-repositories · run 2

- **What I asked:** same prompt as run 1
- **Skill that ran:** efcore-sqlite-repositories v2, commit b1f0378
- **Result:** the first two attempts ran in the old Claude Code session: Claude remembered run 1 and re-applied it instead of reading the new skill. In a new session: signature kept, connection string in `appsettings.json`, separate `EnsureCampusMarketDatabase`, in-memory code deleted, configuration classes with `ValueGeneratedNever`, max lengths and concurrency token. Still wrong: edited `TestApi.cs` again, no `AsNoTracking` in `GetAllAsync`, no `*.db` in `.gitignore`, empty `Update()` methods. The generated code was discarded.
- **What I changed in the skill and why:** v2 added rules only for 4 problems of run 1 (signature + appsettings, separate database creation, delete in-memory code, configuration classes), to see the effect of each rule.
- **Commits:** skill b1f0378; generated code: discarded

### 2026-10-24 · Sara Matasel · efcore-sqlite-repositories · run 3

- **What I asked:** same prompt, in a new Claude Code session
- **Skill that ran:** efcore-sqlite-repositories v3, commit 4bf0abf
- **Result:** build 0 errors, 0 warnings; 53/53 tests (domain only, as the skill says); Postman 36/36 twice in a row; data still there after restarting the API; no change in `Features/` or `Domain/`; old `Api/` and `Infrastructure/` tests deleted, not edited; no `.db` file in `git status`.
- **What I changed in the skill and why:** fixed the 4 problems of run 2 ("never edit tests/", delete the old API tests, `*.db` in `.gitignore`, `AsNoTracking` for `GetAll`, no empty repository method) and completed the skill with full examples, a complete "Never" list and a final check list.
- **Lesson:** every run starts in a new Claude Code session, otherwise Claude reuses the previous context.
- **Commits:** skill 4bf0abf; generated code 091296a

### 2026-10-24 · Diana · unit-testing · skill v4 (align with main)

- **What I asked:** no generation yet; after merging main I compared the skill with the real code (handlers, EF Core, deleted old tests)
- **Skill that ran:** none yet (SKILL.md edited by hand)
- **Result:** the skill still told Claude to avoid `Api/` and `Infrastructure/` tests that no longer exist, and described the in-memory repositories in the exclusions
- **What I changed in the skill and why:** (1) removed the rule about the deleted test folders; (2) Infrastructure exclusion now describes EF Core; (3) added that unit tests never use EF Core or SQLite; (4) new rule: every run starts in a new Claude Code session (lesson from the efcore runs, where Claude reused old context)
- **Commits:** skill 9356f6b; generated code: none yet

### 2026-10-24 · Diana (skill), Sara Matasel (run) · unit-testing · run 1

- **What I asked:** Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md to generate the unit tests for the domain and all handlers. Follow the skill exactly, including the "Never do" list. When you finish, run the tests with coverage and generate the HTML report as the skill says, then tell me the test results and coverage for Domain/ and the handlers.
- **Skill that ran:** unit-testing v4, commit 9356f6b (written by Diana, run by Sara from her Claude Code account, in a new session)
- **Result:** build 0 errors; 90/90 tests pass; 100% line and branch coverage on Domain/ (7 files) and on all 10 handlers; the week 2 domain tests were replaced by the generated ones (Given_When_Then naming, FluentAssertions 7, blank lines between Arrange, Act and Assert); hand-written fakes in Fakes/ (repositories with RejectAdd, unit of work, FixedTimeProvider); HTML report in coverage-report/; no change in src/.
- **What I changed in the skill and why:** nothing, the first run was good. The skill was already improved in v1 to v4 (see the entries above).
- **Commits:** skill 9356f6b; generated code d83fbab

### 2026-10-25 · Anca · integration-testing · run 1

- **What I asked:** Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md to write the integration tests for CampusMarket. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.
- **Skill that ran:** integration-testing v1, commit 6df1d61 (draft with three `TODO(efcore)` lines)
- **Result:** Claude reported build 0 warnings, 90/90 unit tests, 20/20 integration tests three runs in a row; the test code was good (status, body and database checked in every test, nothing in `src/` changed). But: the skill said not to run it until the TODOs were filled, and Claude filled them in the skill itself and changed its status; it wrote this run in `PROMPTS.md` by itself (the skill asked it to); `Guid.NewGuid()` for `SellerId` in 11 places, against the "fixed test data" rule, because the skill's own examples did that; section 8 asked to delete old test files that no longer exist. The generated code was discarded.
- **What I changed in the skill and why:** see v2.
- **Commits:** skill 6df1d61; generated code: discarded

### 2026-10-25 · Anca · integration-testing · run 2

- **What I asked:** same prompt as run 1, in a new Claude Code session
- **Skill that ran:** integration-testing v2 (committed as dcb03e4 right after the run; the content that ran was the same)
- **Result:** Claude reported build 0 warnings, 90/90 unit tests, 20/20 integration tests three runs in a row. The run 1 problems were gone: the skill and PROMPTS.md were not edited, fixed SellerId and CreatedAt, Guid.NewGuid() only for unknown ids. Still wrong: its first draft used a Condition value that does not exist (LikeNew), because the skill does not list the enum values; it picked FluentAssertions 7.2.0 on its own, because the skill only said 7.*; only the Delete 404 tests seeded an entity, the GetById ones did not; the listing update test did not change the category. The generated code was discarded.
- **What I changed in the skill and why:** see v3.
- **Commits:** skill dcb03e4; generated code: discarded

### 2026-10-25 · Anca · integration-testing · run 3

- **What I asked:** same prompt as run 1, in a new Claude Code session
- **Skill that ran:** integration-testing v3, commit c6b3868
- **Result:** Claude reported build 0 errors, 0 warnings and 20/20 integration tests three runs in a row. I ran `dotnet test` myself afterwards: 110/110 (90 unit, 20 integration). The run 2 problems were gone: only real enum values; FluentAssertions 7.2.0; every 404 test seeds an entity and checks it is unchanged; the update tests change every field. The factory is the same as in the skill; `Guid.NewGuid()` only for unknown ids; nothing changed in `src/`, `tests/CampusMarket.UnitTests`, the skill or `PROMPTS.md`; only `CampusMarket.sln` and the new test project.
- **What I changed in the skill and why:** nothing, the run was good.
- **Commits:** skill c6b3868; generated code 98cbece

### 2026-10-25 · Sara Matasel · code-review · run 1

- **What I asked:** Use the code-review skill in .claude/skills/code-review/SKILL.md to review this repository. Follow the skill exactly. Do not edit any file. Give me only the output the skill asks for.
- **Skill that ran:** code-review v1, commit c0d576f (a short first draft)
- **Result:** 2 rows, both real, no file changed. But: the output was not a Markdown table that can be copied into REVIEW.md (the terminal showed it as blocks); one row had the wrong line (`using` is on line 2, not 1); the same row was a `using` order problem forced into the "naming" category, because the skill did not say what to do with a problem outside the six categories. Claude Code then suggested "apply the two fixes"; I did not run it, the review skill is read-only.
- **What I changed in the skill and why:** see v2.
- **Commits:** skill c0d576f; output: not kept

### 2026-10-25 · Sara Matasel · code-review · run 2

- **What I asked:** same prompt, in a new Claude Code session
- **Skill that ran:** code-review v2, commit 858de47
- **Result:** the run 1 problems were gone: the output was one Markdown block, the line was exact, the forced row was not reported. Still wrong: only 1 row (private field `reservations` without `_`); the real test naming problem from run 1 (`ListingTests.cs:289`) was missed, although the code did not change. The skill only said "read the code", with no checklist, so each run looked at different things.
- **What I changed in the skill and why:** v2 added three rules for the run 1 problems: problems outside the six categories are not reported, `File:Line` must be the exact line checked again in the file, the output is one ```markdown block.
- **Commits:** skill 858de47; output: not kept

### 2026-10-25 · Sara Matasel · code-review · run 3

- **What I asked:** same prompt, in a new Claude Code session
- **Skill that ran:** code-review v3, commit 506d7c0
- **Result:** 2 rows, both checked by hand: `Reservation.cs:32` (public `Cancel()` can break the Listing/Reservation invariant, Major) and `ListingTests.cs:289` (test name does not match the Arrange, now in the right category "test quality"). Exact lines, no invented row, no file changed. The table is in REVIEW.md.
- **What I changed in the skill and why:** added a "How to review" checklist for each category, gone through file by file (for tests: the name must match what the Arrange sets up and what is asserted), and skip generated folders (`bin/`, `obj/`, `coverage-report/`, `TestResults/`). Without a checklist the review changed from run to run.
- **Commits:** skill 506d7c0; review output in REVIEW.md



