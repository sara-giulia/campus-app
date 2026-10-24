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
- **Commits:** "Fix cqrs skill after review: Domain rule, test check, update example, handler signature decision"

### 2026-10-22 · Carina Coman (skill), Sara Matasel (run) · cqrs · run 1

- **What I asked:** Use the cqrs skill in .claude/skills/cqrs/SKILL.md to refactor all 10 slices in src/CampusMarket.Api/Features (Listings and Members) into commands, queries and handlers. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.
- **Skill that ran:** cqrs, commit a90b234 (written by Carina, run by Sara from her Claude Code account, on a shared call)
- **Result:** all 10 slices now have a command or query and a handler. Build: 0 errors, 0 warnings. Tests: 90/90 pass. Postman: 36/36 requests, 68/68 assertions. No endpoint uses a repository or IUnitOfWork; query handlers do not use IUnitOfWork; Domain/, tests/ and postman/ unchanged (only the new IUnitOfWork.cs).
- **What I changed in the skill and why:** nothing, the first run followed the skill. The skill was already improved before this run (see "review fixes" above).
- **Commits:** skill a90b234, generated code <hash>

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