---
name: code-review
description: Reviews a .NET vertical-slice API (CQRS handlers, domain, tests) and returns one Markdown table of problems. Read-only, it never changes a file. Use it when you want a code review of this repository or of another team's repository.
---

# Code review

Read the code of the repository and report the problems you find. You only read. You never change, create or delete a file.

## What to look at

- `src/`: the slices in `Features/`, the domain, the infrastructure, `Program.cs`.
- `tests/`: the unit tests and the integration tests.

## Categories

Every problem has exactly one of these categories:

- **slice boundaries**: a slice uses code from another slice, or an endpoint does work that belongs in the handler.
- **CQRS violations**: a query changes data, a command returns data it should not, a handler calls another handler.
- **domain invariants**: a rule of the domain can be broken (public setters, missing checks, invalid state).
- **test quality**: a test does not really check the behaviour, depends on other tests, or has logic inside.
- **naming**: a name does not say what the thing does, or does not follow the project's convention.
- **error handling**: an expected failure throws an exception, or an error is mapped to the wrong HTTP status.

## Severity

- **Critical**: wrong behaviour or lost data, it must be fixed before delivery.
- **Major**: it works now, but it breaks a rule of the architecture or will cause bugs later.
- **Minor**: style, naming or readability.

## Output

1. One line with the counts, for example: `Found 5 problems: 1 Critical, 2 Major, 2 Minor.`
2. One Markdown table with the columns `#`, `File:Line`, `Severity`, `Category`, `Problem`, `Proposed fix`.
3. Rows sorted by severity: Critical first, then Major, then Minor.

If you find no problems, write `No problems found.` and no table. Do not invent problems.

## Example

Found 2 problems: 1 Major, 1 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/Api/Features/Books/GetBooks/GetBooksHandler.cs:18 | Major | CQRS violations | The query handler calls `SaveChangesAsync`. | Remove the call; a query must not change state. |
| 2 | tests/Api.UnitTests/BookTests.cs:12 | Minor | naming | The test is called `Test1`. | Rename it to `Given_EmptyTitle_When_Creating_Then_ReturnsValidationError`. |

