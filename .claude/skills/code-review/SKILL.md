---
name: code-review
description: Reviews a .NET vertical-slice API (CQRS handlers, domain, tests) and returns one Markdown table of problems. Read-only, it never changes a file. Use it when you want a code review of this repository or of another team's repository.
---

# Code review

Read the code of the repository and report the problems you find. You only read. You never change, create or delete a file.

## What to look at

- First read the project's own rules: `README.md` and every `.claude/skills/*/SKILL.md`. They say what the app must do and which conventions the team chose. Something these rules explicitly allow is not a problem.
- `src/`: the slices in `Features/`, the domain, the infrastructure, `Program.cs`.
- `tests/`: the unit tests and the integration tests.
- Skip generated files: `bin/`, `obj/`, `coverage-report/`, `TestResults/`, `Migrations/`.

## How to review

Open every file in the folders above, one by one, and go through this checklist. Do not stop after the first problems you find: the same repository must give the same table every time.

- **slice boundaries**: does a slice folder use a type from another slice folder? Does an endpoint call a repository, a DbContext or the domain directly, or contain an `if` on data?
- **CQRS violations**: does a query handler call Add, Update, Delete or `CommitAsync`? Does a command handler forget `CommitAsync`? Does a handler call another handler?
- **domain invariants**: does an entity have a public setter or a public constructor? Can a factory or method put the entity in an invalid state? Can a caller change a collection from outside?
- **test quality**, for every test method: does the name of the test match what the Arrange really sets up and what is asserted (for `Given_When_Then` names, check each part)? Does the test assert anything? Does it contain `if`, a loop, `DateTime.Now` or `Thread.Sleep`? Does it share state with other tests?
- **naming**: do classes, methods and private fields follow the same convention as the rest of the project (compare with at least two other files)?
- **requirements**: for every business rule written in the README (use cases, columns like "rules" or "constraints", words like *unique*, *must*, *only*, *at most*), find the code that enforces it. If no code enforces it, it is a problem: report it as **domain invariants**, at the line of the handler or domain method where the check is missing, and quote the README rule in `Problem`.
- **error handling**: does a handler throw for not found, invalid input or a duplicate? Does an endpoint map an error type to the wrong status (Validation 400, NotFound 404, Conflict 409)?

## Categories

Every problem has exactly one of these categories:

- **slice boundaries**: a slice uses code from another slice, or an endpoint does work that belongs in the handler.
- **CQRS violations**: a query changes data, a command returns data it should not, a handler calls another handler.
- **domain invariants**: a rule of the domain can be broken (public setters, missing checks, invalid state).
- **test quality**: a test does not really check the behaviour, depends on other tests, or has logic inside.
- **naming**: a name does not say what the thing does, or does not follow the project's convention.
- **error handling**: an expected failure throws an exception, or an error is mapped to the wrong HTTP status.

If a problem does not fit any of these six categories (for example the order of `using` lines or formatting), do not report it. Never put a problem in a category that does not describe it.

## Severity

- **Critical**: wrong behaviour or lost data, it must be fixed before delivery.
- **Major**: it works now, but it breaks a rule of the architecture or will cause bugs later.
- **Minor**: style, naming or readability.

## File and line

- `File:Line` is the path from the repository root and the exact line where the problem is.
- For a missing check, use the line with the name of the method where the check should be (for example `public async Task<...> HandleAsync(`), never a `{` line.
- Before you write a row, open the file again and check that this line really contains the code you describe. If it does not, fix the line number or drop the row.

## Output

Print everything inside one fenced code block that starts with ```` ```markdown ````, so the text can be copied into `REVIEW.md` exactly as it is. Nothing before or after the block.

1. One line with the counts, for example: `Found 5 problems: 1 Critical, 2 Major, 2 Minor.`
2. One Markdown table with the columns `#`, `File:Line`, `Severity`, `Category`, `Problem`, `Proposed fix`.
3. Rows sorted by severity: Critical first, then Major, then Minor.

If you find no problems, write `No problems found.` (inside the code block) and no table. Do not invent problems.

## Example

Found 2 problems: 1 Major, 1 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/Api/Features/Books/GetBooks/GetBooksHandler.cs:18 | Major | CQRS violations | The query handler calls `CommitAsync`. | Remove the call; a query must not change state. |
| 2 | tests/Api.UnitTests/BookTests.cs:12 | Minor | naming | The test is called `Test1`. | Rename it to `Given_EmptyTitle_When_Creating_Then_ReturnsValidationError`. |
