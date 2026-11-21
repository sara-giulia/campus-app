# PROMPTS.md

Log of every skill run: what was asked, which version of the skill ran, what was wrong in the output, and what we changed in the skill.
Every run starts in a new Claude Code session. The generated code is never fixed by hand: when the output is wrong, we change the skill and run it again.
Bad outputs were discarded (not committed), so they are shown here as excerpts. Every skill version has its own commit, so the before/after of each skill can also be seen with `git diff <old> <new> -- .claude/skills/<name>/SKILL.md`.

## Format

For each skill, one section per run:

- **Prompt:** the exact prompt given to Claude Code
- **Skill version:** the version and the commit of the SKILL.md that ran
- **Result:** build, tests, Postman, what the output looks like
- **Problems seen (bad output):** what was wrong, with an excerpt of the generated code
- **Change to SKILL.md (vX → vY) and why:** a before/after table of the rules that changed
- **Commits:** the skill commit and the generated-code commit (separate commits)

For a change to a skill without a run (for example a rename after a merge), a section **Skill update vX → vY** under that skill: a table with the columns `| What | Before | After |`, the reason, who made the change and the commit.

---

## cqrs

Skill written by Carina Coman, run by Sara Matasel from her Claude Code account on a shared call.

### Review before the first run: v1 → v2

- **What happened:** a teammate built the CreateListing example from the skill in the real project (0 warnings, 90 tests pass) and reviewed the rest of the skill against the code.
- **Skill version:** v1, commit 617d8c2

#### Problems seen in v1

1. The "Never" list said not to change `Domain/`, but the same skill asks for a new `Domain/Common/IUnitOfWork.cs`. Claude would get two rules that contradict each other.
2. The final check only had `dotnet build`, so nothing proved the behaviour stayed the same.
3. The examples only showed a create slice and a query slice. UpdateListing is the only slice that maps two error types (404 and 400), and it had no example.
4. Our `HandleAsync` signature is different from the course (`Task<Result<T>>`, `ct` without a default), but the skill did not say it is on purpose, so Claude could "fix" it back to the course version.

#### Change to SKILL.md (v1 → v2) and why

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | *"Never change `Domain/`, the repositories, `tests/` or `postman/` to make the refactor easier."* | *"Never change existing files in `Domain/`; the only new file there is `IUnitOfWork.cs`. Never change the repositories, `tests/` or `postman/` to make the refactor easier."* | The old rule forbade a file the skill itself asks for. |
| 2 | Final check: `dotnet build` only. | Added `dotnet test`: all existing tests must still pass. | The existing tests prove the refactor did not change the behaviour. |
| 3 | Examples: one command slice, one query slice. | New section *"9. Example: update slice (`UpdateListing`)"* with the command, the handler and an endpoint that maps `NotFound` → 404 and everything else → 400. | It is the only slice with two error types; without an example Claude has to guess the mapping. |
| 4 | Nothing about the signature. | New *"Decision: handler signature"*: *"`HandleAsync` returns `Task<Result<TResult>>` and takes `CancellationToken ct` without a default value. The course uses `Task` and `CancellationToken cancellationToken = default`; we do not. … Both differences are intentional."* | So Claude does not copy the course signature. |

- **Commits:** skill ec0b222 (one more small edit before the run: a90b234)

### Run 1, skill v2

- **Prompt:** `Use the cqrs skill in .claude/skills/cqrs/SKILL.md to refactor all 10 slices in src/CampusMarket.Api/Features (Listings and Members) into commands, queries and handlers. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v2, commit a90b234
- **Result:** all 10 slices have a command or a query and a handler. Build: 0 errors, 0 warnings. Tests: 90/90. Postman: 36/36 requests, 68/68 assertions. No endpoint uses a repository or `IUnitOfWork`; query handlers do not use `IUnitOfWork`; `Domain/`, `tests/` and `postman/` unchanged (only the new `IUnitOfWork.cs`).
- **Problems seen:** none. The problems were already fixed in the review above.
- **Change to SKILL.md:** none for this run. Later we found a missing rule (see run 2).
- **Commits:** skill a90b234; generated code 9c099c5

### Run 2, skill v3

- **Why a new version:** testing the API in Postman, we could create two members with the same email, although our README says `CreateMember` needs a **unique email**. Nothing checked it: `Email.Create` only checks the format, and `AddAsync` only checks duplicate Ids, which never happen because the domain creates a new Guid. All tests passed and coverage was 100%, because the check had never been written, so there was no code to cover. Our code-review skill missed it too, because it does not compare the code with the README.

#### Change to SKILL.md (v2 → v3) and why

| # | v2 (before) | v3 (after) | Why |
|---|---|---|---|
| 1 | Handler errors: not found, invalid data, *"duplicate Id (`AddAsync` returned false) → `Error.Conflict(...)`"*. Nothing about email. | *"Unique email … `CreateMemberHandler`: after `Member.Create` succeeded and before `AddAsync`, if any member returned by `GetAllAsync` has the same `Email`, return `Error.Conflict("Member.EmailTaken", …)`"* + *"`UpdateMemberHandler`: … **before** `member.Update` … if any **other** member (`m.Id != member.Id`) has that `Email`, return the same conflict, without changing the member … Keeping your own email is allowed."* | Uniqueness needs all members, so the domain cannot check it; the handler can ask the repository. The check runs before `member.Update`, so a refused update leaves the member unchanged. |
| 2 | *"If it can fail with two, use a `switch`"* | *"`UpdateMember` can now also fail with `Conflict` … `Results<NoContent, NotFound<Error>, BadRequest<Error>, Conflict<Error>>` … `ErrorType.Conflict => TypedResults.Conflict(result.Error)`"* | The endpoint must map the new error to 409. |
| 3 | *"Never change routes, status codes …"* | *"… The only new status is 409 `Member.EmailTaken` in `CreateMember` and `UpdateMember`."* | The old rule forbade the fix itself. |
| 4 | Nothing about existing handlers. | *"If the slices already have their command, query and handler, keep them and only add what this skill asks for that is missing."* | So Claude does not rewrite the other 8 slices. |

- **Prompt:** `Use the cqrs skill in .claude/skills/cqrs/SKILL.md. The handlers already exist: keep them and only add what the skill asks for that is missing (the unique email check in CreateMember and UpdateMember). Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v3, commit 657431f
- **Result:** 3 files changed, 16 lines: `CreateMemberHandler`, `UpdateMemberHandler`, `UpdateMemberEndpoint`. Nothing changed in `Domain/`, the repositories or `tests/`. Build 0 errors, 0 warnings; all existing tests pass.
- **Known limit:** the check loads all members with `GetAllAsync`, which is fine for our size. Two requests at the exact same moment could still both pass; a unique index on `Members.Email` (efcore skill and a new migration) would be the final guarantee.

#### After (excerpt of the final code)

`UpdateMemberHandler.cs`:

```csharp
var newEmail = Email.Create(command.Email);
if (newEmail.IsSuccess)
{
    var existingMembers = await repository.GetAllAsync(ct);
    if (existingMembers.Any(m => m.Id != member.Id && m.Email == newEmail.Value))
        return Result<Guid>.Failure(
            Error.Conflict("Member.EmailTaken", $"A member with email '{newEmail.Value.Value}' already exists."));
}

var updated = member.Update(command.Name, command.Email);
```

- **Change to SKILL.md:** none; v3 is the final version.
- **Commits:** skill 657431f; generated code cd78cfe

### Skill update v3 → v4 (groundwork PR)

| What | Before (v3) | After (v4) |
|---|---|---|
| Unit of work method | `SaveChangesAsync(CancellationToken ct = default)` | `CommitAsync(CancellationToken ct)`, no default value |
| Error mapping rule | `Validation`, `NotFound`, `Conflict` | also `Forbidden` → 403 (`TypedResults.Json(result.Error, statusCode: StatusCodes.Status403Forbidden)`, type `JsonHttpResult<Error>` in `Results<...>`) |
| "Update and Delete" rule | a broken word left by the first rename | fixed: "then `CommitAsync`" |

- **Why:** the groundwork PR renames the method (week 4 contract); a run with v3 would generate code that does not compile. `Forbidden` is needed for the ownership rules.
- **Who:** Carina Coman (edit only, no generation)
- **Commit:** `9c8de00`

---

## efcore-sqlite-repositories

Skill written and run by Sara Matasel.

### Run 1, skill v1

- **Prompt:** `Use the efcore-sqlite-repositories skill in .claude/skills/efcore-sqlite-repositories/SKILL.md to replace the in-memory repositories with EF Core + SQLite. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v1, commit b3ebc49 (a short first draft)
- **Result:** build OK, 90/90 tests, but the code broke several rules of our architecture. Discarded.

#### Problems seen (bad output from v1)

**1. It changed the signature of `AddCampusMarket` and hard-coded the connection string**, in `CampusMarketSetup.cs`:

```csharp
public static IServiceCollection AddCampusMarket(
    this IServiceCollection services,
    string connectionString = "Data Source=campusmarket.db")
{
    ...
    services.AddDbContext<CampusMarketDbContext>(options =>
        options.UseSqlite(connectionString));
```

**2. It created the database inside `MapCampusMarket`, a method that should only map routes**, in `CampusMarketSetup.cs`:

```csharp
public static IEndpointRouteBuilder MapCampusMarket(this IEndpointRouteBuilder app)
{
    // Create the SQLite schema the first time the app runs. Safe to call on every startup.
    if (app is WebApplication webApp)
    {
        using var scope = webApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.Database.EnsureCreated();
    }
```

**3. It left the in-memory classes as dead code**: `Infrastructure/` still had `InMemoryListingRepository.cs`, `InMemoryMemberRepository.cs` and `InMemoryUnitOfWork.cs`, used by nothing.

**4. All the mapping in one `OnModelCreating`, no configuration classes, no `ValueGeneratedNever`, no max lengths, no concurrency token**, in `CampusMarketDbContext.cs`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    var listing = modelBuilder.Entity<Listing>();
    listing.ToTable("Listings");
    listing.HasKey(l => l.Id);
    listing.Property(l => l.Title).IsRequired();
    ...
    var member = modelBuilder.Entity<Member>();
    member.ToTable("Members");
    member.HasKey(m => m.Id);
    member.Property(m => m.Name).IsRequired();
```

**5. It edited the old tests so they would pass**, in `tests/CampusMarket.UnitTests/Api/TestApi.cs`:

```csharp
private readonly SqliteConnection _keepAlive;
...
builder.Services.AddCampusMarket(connectionString);
```

**6. `GetAllAsync` without `AsNoTracking`**, and no `*.db` in `.gitignore`.

#### Change to SKILL.md (v1 → v2) and why

In v2 we added rules **only for problems 1 to 4**, to see the effect of each rule on its own.

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | *"Use the connection string `Data Source=campusmarket.db`."* | New section **Configuration and startup**: *"Keep the signature `AddCampusMarket(this IServiceCollection services)` exactly as it is. Do not add parameters."* + *"The connection string lives in `appsettings.json`, never in the code"* + the `AddDbContext((sp, options) => … GetConnectionString("CampusMarket"))` snippet. | v1 gave the string, so Claude put it in code. |
| 2 | Nothing about where the database is created. | *"`MapCampusMarket` only maps routes. The database is created in a separate method … `EnsureCampusMarketDatabase(this WebApplication app)` … `Program.cs` calls it right after `builder.Build()`."* | One method, one job. |
| 3 | Nothing about the old code. | New section **Old in-memory code**: *"Delete `InMemoryListingRepository.cs`, `InMemoryMemberRepository.cs` and `InMemoryUnitOfWork.cs`. Nothing uses them after the switch, so they would be dead code."* | v1 only said what to add, never what to remove. |
| 4 | **Mapping** had three lines: value objects in one column, enums as text, a listing has reservations. | *"One configuration class per entity, in `Infrastructure/Persistence/Configurations/`"*, *"`OnModelCreating` only calls `ApplyConfigurationsFromAssembly`"*, *"Every `Id` … `ValueGeneratedNever()`"*, max lengths for `Title` and `Name`, *"`Listing.Version` is a concurrency token"*. | v1 did not say how to organise or what to configure, so Claude picked the shortest way. |

- **Commits:** skill b3ebc49; generated code: discarded

### Run 2, skill v2

- **Prompt:** same as run 1
- **Skill version:** v2, commit b1f0378
- **Result:** the first two attempts ran in the **old** Claude Code session: Claude remembered run 1 and produced the same code again (same signature with `connectionString`, same `EnsureCreated` in `MapCampusMarket`) instead of reading the new skill. In a new session, problems 1 to 4 were gone: signature kept, connection string in `appsettings.json`, separate `EnsureCampusMarketDatabase`, in-memory code deleted, three configuration classes with `ValueGeneratedNever`, max lengths and the concurrency token. Discarded because of the problems below.

#### Problems seen (bad output from v2)

**1. It edited the old tests again**, in `tests/CampusMarket.UnitTests/Api/TestApi.cs`:

```csharp
/// Each test gets its own SQLite file, so the data is empty at the start.
...
private readonly string _dbPath;
```

**2. `GetAllAsync` still without `AsNoTracking`**, in `EfMemberRepository.cs`:

```csharp
public async Task<IReadOnlyList<Member>> GetAllAsync(CancellationToken ct = default) =>
    await db.Members.ToListAsync(ct);
```

**3. Empty `Update` methods**, in `EfListingRepository.cs` (same in `EfMemberRepository.cs`):

```csharp
public void Update(Listing listing)
{
    // The listing was loaded through GetByIdAsync, so EF tracks it already.
    // SaveChangesAsync on the UnitOfWork persists the pending changes.
}
```

**4. No `*.db` in `.gitignore`**, so the database file could end up in Git.

#### Change to SKILL.md (v2 → v3) and why

| # | v2 (before) | v3 (after) | Why |
|---|---|---|---|
| 1 | *"Delete `tests/CampusMarket.UnitTests/Infrastructure/`"* (nothing about `Api/`) | *"Delete … `tests/CampusMarket.UnitTests/Infrastructure/` and `tests/CampusMarket.UnitTests/Api/`. The `unit-testing` and `integration-testing` skills write new ones."* + *"Never edit a file in `tests/`. Old tests are deleted, never adapted."* | If the AI changes tests to make them pass, the tests prove nothing. |
| 2 | Nothing about tracking. | *"`GetByIdAsync`: … tracked, because the handler may change the entity."* + *"`GetAllAsync`: `AsNoTracking()` and `ToListAsync`"* + a full `EfListingRepository` example. | `GetAll` only reads, so tracking is wasted work. |
| 3 | Only the small `AddAsync` example. | *"`Update` calls `db.Update(entity)` and `Delete` calls `db.Remove(entity)`. … Never leave a repository method empty."* | An empty method hides what the repository is supposed to do. |
| 4 | Nothing about Git. | *"`.gitignore` gets `*.db`, `*.db-shm` and `*.db-wal`"* + *"Never commit a `.db` file."* | Local data never belongs in the repository. |
| 5 | Short "Rules" list. | Full **Never** list, a mapping table with the exact conversions (`m => m.Amount` / `Money.Create(v).Value`), *"No foreign key from `SellerId` or `MemberId` to `Member`"*, a full `ListingConfiguration` example and a **Check when you are done** list. | Claude follows an example much better than a sentence. |

- **Lesson:** every run starts in a new Claude Code session, otherwise Claude reuses the previous context.
- **Commits:** skill b1f0378; generated code: discarded

### Run 3, skill v3

- **Prompt:** same as run 1, in a new Claude Code session
- **Skill version:** v3, commit 4bf0abf
- **Result:** build 0 errors, 0 warnings; 53/53 tests (domain only, as the skill says); Postman 36/36 twice in a row; data still there after restarting the API; no change in `Features/` or `Domain/`; old `Api/` and `Infrastructure/` tests deleted, not edited; no `.db` file in `git status`.

#### After (excerpts of the final code)

`EfListingRepository.cs`:

```csharp
public async Task<IReadOnlyList<Listing>> GetAllAsync(CancellationToken ct = default) =>
    await db.Listings
        .AsNoTracking()
        .Include(l => l.Reservations)
        .ToListAsync(ct);

public void Update(Listing listing) => db.Listings.Update(listing);
```

`CampusMarketSetup.cs`:

```csharp
public static IServiceCollection AddCampusMarket(this IServiceCollection services)
{
    ...
    services.AddDbContext<CampusMarketDbContext>((sp, options) =>
        options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("CampusMarket")));
```

`.gitignore`: `*.db`, `*.db-shm`, `*.db-wal`.

- **Change to SKILL.md:** none for the code. Later we decided to use migrations instead of `EnsureCreated` (see run 4).
- **Commits:** skill 4bf0abf; generated code 091296a

### Run 4, skill v4

- **Prompt:** `Use the efcore-sqlite-repositories skill in .claude/skills/efcore-sqlite-repositories/SKILL.md. The EF Core code already exists: keep it and only add what the skill asks for that is missing (the migrations). Follow the skill exactly, including the "Never" list. When you finish, run dotnet build, dotnet ef migrations list and dotnet test, and tell me the results.`
- **Skill version:** v4, commit dda1dcc (committed right after the run; the content that ran was the same)
- **Why a new version:** v3 created the tables with `EnsureCreated`, which can only build a database from zero. With migrations, every change of the model becomes a versioned file in Git, and an existing database can be updated without losing its data. This is also what the course shows.

#### Change to SKILL.md (v3 → v4) and why

| # | v3 (before) | v4 (after) | Why |
|---|---|---|---|
| 1 | *"Only one package … We do not use migrations. The tables are created with `EnsureCreated` when the app starts."* | Two packages: `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets` all, plus a new section **Migrations**: *"The `dotnet ef` tool is a local tool of the repository … The first migration is called `InitialCreate` and goes into `Infrastructure/Persistence/Migrations/` … Never edit them by hand … add a new migration … Never change an existing migration."* | `dotnet ef` needs the Design package; a local tool means everyone uses the same version. |
| 2 | *"It opens a scope, gets the `CampusMarketDbContext` and calls `Database.EnsureCreated()`."* | *"… calls `Database.Migrate()`, which applies the migrations that are not applied yet. … Keep the name of the method: `Program.cs` and the integration tests already use it."* | The schema now comes from the migrations; keeping the name means nothing else changes. |
| 3 | *"Never use migrations, `EnsureDeleted`, raw SQL or a second DbContext."* | *"Never call `EnsureCreated` in the app, never edit a generated migration, and never use `EnsureDeleted`, raw SQL or a second DbContext."* | The opposite decision. |
| 4 | Nothing about existing code. | *"If the EF Core code below already exists, keep it and only add what is missing (for example the migrations)."* | So Claude does not rewrite code that is already correct. |
| 5 | Check list without migrations. | *"`dotnet ef migrations list` shows `InitialCreate`"* + *"Delete the old local `campusmarket.db` once (it was created by `EnsureCreated` and has no migration history)…"* + *"`dotnet test`: all the tests pass (unit and integration)."* | An old database made by `EnsureCreated` cannot be migrated. |

- **Result:** build 0 errors, 0 warnings; `dotnet ef migrations list` shows `20261026061638_InitialCreate`; `dotnet test` 110/110 (90 unit, 20 integration); Postman 36/36. Changed only: the Design package in the `.csproj`, one line in `CampusMarketSetup.cs` (`EnsureCreated()` → `Migrate()`), the new `.config/dotnet-tools.json` (`dotnet-ef` 10.0.14) and the three generated migration files. The migration matches the configuration classes: max length 100 for `Title` and `Name`, a cascade foreign key from `Reservations` to `Listings`, no foreign key to `Members`. Nothing changed in `Features/`, `Domain/`, `tests/` or `Program.cs`.
- **Problems seen:** none.

#### After (excerpt of the final code)

`CampusMarketSetup.cs`:

```csharp
public static WebApplication EnsureCampusMarketDatabase(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
    db.Database.Migrate();
    return app;
}
```

- **Change to SKILL.md:** none; v4 is the final version.
- **Commits:** skill dda1dcc; generated code 9d9d397

### Skill update v4 → v5 (groundwork PR)

| What | Before (v4) | After (v5) |
|---|---|---|
| Unit of work method (line 19) | `SaveChangesAsync` | `CommitAsync` |
| Repository rule (line 148) | "Never call `SaveChangesAsync` in a repository." | "Never call `db.SaveChangesAsync` or `CommitAsync` in a repository. Only `EfUnitOfWork` saves." |
| "Never" list (lines 145 and 151) | several rules joined on one line with "and never" | one rule per line |

- **Why:** the groundwork PR renames the method. Line 103 stays as it is: `EfUnitOfWork.CommitAsync` returns EF Core's `db.SaveChangesAsync(ct)`. The rule on line 148 was ambiguous because both methods had the same name. The "Never" list is easier for Claude to follow with one rule per line.
- **Who:** Carina Coman (edit only, no generation)
- **Commits:** `a7326c6`, `07aa3ea`

---

## unit-testing

Skill written by Diana Preutu, run by Sara Matasel from her Claude Code account on a shared call.
The skill was improved three times before its run, by comparing it with the real code after each merge.

### v1 → v2: compared with the real code

- **Skill version:** v1, commit 32a93f7
- **Problems seen in v1:** the handler example did not match the real handlers (wrong time provider name, wrong constructor); the fakes were described only in one sentence; the `Conflict` branch of the create handlers can never be reached, because the domain creates the Id itself; the coverage command was different from the CI.

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | `new CreateListingHandler(repository, unitOfWork, new FakeTimeProvider())` | `new CreateListingHandler(repository, unitOfWork, new FixedTimeProvider(Now))` + *"Command handlers take the repository interface, `IUnitOfWork` and sometimes `TimeProvider`. Query handlers take only the repository interface."* | The example has to compile against the real handlers. |
| 2 | *"Fakes are written by hand in `Fakes/`: an in-memory fake repository, a fake unit of work that counts `SaveChangesAsync` calls, and a fake `TimeProvider`…"* | One rule per fake, and: *"Add a public property `RejectAdd` … when true, `AddAsync` returns `false` without storing. This is the only way to reach the `Conflict` branch, because the domain generates the Id itself."* | Without it the 100% coverage on handlers is impossible. |
| 3 | No list of cases per handler. | Test matrix: *"Create: success (item stored, `SaveCount` 1), invalid data (…`SaveCount` 0…), duplicate Id with `RejectAdd = true`…"*, the same for Update and Delete. | So every branch of every handler is covered. |
| 4 | Generic coverage command. | *"Collect with coverlet using the repo file `coverlet.runsettings` (the CI uses it too)"* | Local coverage and CI coverage must be the same. |

- **Commits:** skill 096e397

### v2 → v3: review by a teammate

| # | v2 (before) | v3 (after) | Why |
|---|---|---|---|
| 1 | *"Exclude only what cannot be unit-tested (`Program.cs`, `CampusMarketSetup.cs`, ...)"* | *"This is the single list of exclusions (the README copies it word for word)"* + the report limited with `-filefilters:"+*Domain*;+*Features*Handler.cs"` | One list, not two that can disagree. |
| 2 | Nothing about committing the report. | *"Commit `coverage-report/` (the HTML report is a deliverable of the assignment). Never commit `TestResults/`."* | The report is part of the delivery. |
| 3 | *"use `result.Value!` when in doubt"* | *"reading `Value` on a failed result throws `InvalidOperationException`, and reading `Error` on a successful result throws … Use `Action act = () => _ = result.Value;`"* | Otherwise those two branches of `Result` stay uncovered. |

- **Commits:** skill 342e693

### v3 → v4: aligned with main after the EF Core merge

| # | v3 (before) | v4 (after) | Why |
|---|---|---|---|
| 1 | *"Do not touch `Api/` and `Infrastructure/` in the test project: they belong to other issues."* | *"The old `Api/` and `Infrastructure/` test folders no longer exist: never recreate them (HTTP tests belong to the integration-testing skill)."* | The EF Core skill had already deleted them. |
| 2 | *"`Infrastructure/`: in-memory repositories and unit of work…"* | *"`Infrastructure/`: EF Core DbContext, configurations, repositories and unit of work, no domain logic, covered by the integration tests."* + *"Unit tests never use EF Core or SQLite."* | The exclusion described code that was gone. |
| 3 | Nothing about sessions. | *"Every run starts in a new Claude Code session, so that the current version of this file is read and not an older context."* | Lesson from the efcore-sqlite-repositories runs. |

- **Commits:** skill 9356f6b

### Run 1, skill v4

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md to generate the unit tests for the domain and all handlers. Follow the skill exactly, including the "Never do" list. When you finish, run the tests with coverage and generate the HTML report as the skill says, then tell me the test results and coverage for Domain/ and the handlers.`
- **Skill version:** v4, commit 9356f6b
- **Result:** build 0 errors; 90/90 tests; **100% line and branch coverage** on `Domain/` (7 files) and all 10 handlers. The week 2 domain tests were replaced by the generated ones (Given_When_Then names, FluentAssertions 7, a blank line between Arrange, Act and Assert). Hand-written fakes in `Fakes/` (repositories with `RejectAdd`, unit of work, `FixedTimeProvider`). HTML report in `coverage-report/`. No change in `src/`.
- **Problems seen:** none in the generated code. After pushing, SonarQube Cloud reported 161 issues and 23.5% duplication, all inside `coverage-report/` (generated HTML, CSS and JS). Fixed in the SonarQube settings (`sonar.exclusions = coverage-report/**`), not in the code.
- **Change to SKILL.md:** none; v4 is the final version.
- **Commits:** skill 9356f6b; generated code d83fbab

### Run 2, skill v5

- **Why a new version:** the cqrs skill v3 added the unique email check (see cqrs, run 2), so the handlers have new branches without tests.

#### Change to SKILL.md (v4 → v5) and why

| # | v4 (before) | v5 (after) | Why |
|---|---|---|---|
| 1 | Test matrix for Create and Update: success, not found, invalid data, duplicate Id. | `CreateMemberHandler`: *"email already used by another stored member → `ErrorType.Conflict`, code `Member.EmailTaken`, `SaveCount` 0, nothing new stored"*; `UpdateMemberHandler`: email taken → conflict and *"member unchanged"*, *"keeping the member's own email → success"*, *"an invalid new email → `Validation` (so both sides of the email check are covered)"*. | The new branches must be tested, and coverage must stay 100%. |
| 2 | Nothing about existing tests. | *"If unit tests already exist, keep them and only add the tests that are missing for the current code. Then generate the coverage report again."* | So Claude does not regenerate the 90 tests that are already correct. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests that are missing for the current code. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.`
- **Skill version:** v5, commit 931234c
- **Result:** 4 new `[Fact]` tests, and 0 lines removed from the existing tests:
    - `Given_EmailAlreadyUsedByAnotherMember_When_Handling_Then_ReturnsEmailTakenConflictAndDoesNotStoreOrSave`
    - `Given_NewEmailUsedByAnotherMember_When_Handling_Then_ReturnsEmailTakenConflictAndDoesNotSave`
    - `Given_ExistingMember_When_KeepingOwnEmail_Then_UpdatesAndSavesOnce`
    - `Given_ExistingMember_When_HandlingWithInvalidEmail_Then_ReturnsValidationErrorAndDoesNotSave`

  94 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 332/332 lines, 90/90 branches. The report in `coverage-report/` was generated again.
- **Change to SKILL.md:** none; v5 is the final version.
- **Commits:** skill 931234c; generated code f1af1a2

### Run 3, skill v6 (Feature: Authentication)

- **Why a new version:** week 4 added register and login (`Member.Register`, `RegisterHandler`, `LoginHandler`, `IPasswordHasher`, `ITokenService`). They had no unit tests.

#### Change to SKILL.md (v5 → v6) and why

| # | v5 (before) | v6 (after) | Why |
|---|---|---|---|
| 1 | Nothing about authentication. | New section *"Feature: Authentication (Sara)"*: the files to read, two new fakes (`FakePasswordHasher` with the `"hashed:"` prefix, `FakeTokenService` with a fixed `ExpiresAt`), and the test list for `Member.Register`, `RegisterHandler` and `LoginHandler`. | Without it Claude would have to guess the fakes and could use the real hasher or a real JWT in unit tests. |
| 2 | — | *"The error code is the same for every login failure. Test that it is the same code."* | So the tests prove the API never tells you whether an email exists. |

- **Honest note:** the section was used for the run, but it was saved and committed only after the run (we forgot Ctrl+S). The text in commit c67c85f is the one that was used.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests that are missing for the current code, following the "Feature: Authentication" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.`
- **Skill version:** v6, commit c67c85f
- **Result:** new files `Fakes/FakePasswordHasher.cs`, `Fakes/FakeTokenService.cs`, `Features/Auth/Register/RegisterHandlerTests.cs` (6 tests), `Features/Auth/Login/LoginHandlerTests.cs` (7 tests); 5 new tests added to `Domain/Members/MemberTests.cs`, and 0 lines removed from the existing tests. All tests pass. Coverage of `Domain/` and the handlers stays **100%**: 392/392 lines, 114/114 branches. The report in `coverage-report/` was generated again.
- **Change to SKILL.md:** none; v6 is the final version.
- **Commits:** skill c67c85f; generated code 596301a

### Run 4, skill v7 (Feature: Listing search and paging)

- **Why a new version:** week 4 added filters, text search and paging to `GetListingsHandler`, and search now returns only `Active` listings. The new branches had no unit tests.

#### Change to SKILL.md (v6 → v7) and why

| # | v6 (before) | v7 (after) | Why |
|---|---|---|---|
| 1 | Nothing about search. | New section *"Feature: Listing search and paging (Diana)"*: the files to read, the arrange rule (fixed `createdAt` per listing, listings published with `Publish()`), and the test list: each 400 error, the valid limits, each filter alone, two filters together, the order, the paging and `TotalCount`. | Without it Claude would only keep the two GetAll tests and miss most branches of the new handler. |
| 2 | — | *"Status: with one `Draft`, one `Active` and one `Reserved` listing seeded … only the `Active` one is returned"* | Proves the Active filter, which the README asks for. |

- **Honest note:** after the Active filter, the two existing `GetListingsHandlerTests` broke the CI because they seed `Draft` listings. They were fixed by hand before the run (two `Publish()` calls, commit 8d21e3e), so the CI was green before the skill ran.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Listing search and paging" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.`
- **Skill version:** v7, commit 8d21e3e; section written by Diana Preutu, run by Sara Matasel
- **Result:** 25 new tests in `Features/Listings/GetListings/GetListingsHandlerTests.cs`, and 0 lines removed from the existing tests. All 146 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 419/419 lines, 130/130 branches. The report in `coverage-report/` was generated again.
- **Change to SKILL.md:** none; v7 is the final version.
- **Commits:** skill 8d21e3e; generated code 0c5274d

### Skill update v7 → v8 (groundwork PR)

| What | Before (v7) | After (v8) |
|---|---|---|
| Unit of work method (lines 19 and 162) | `IUnitOfWork.SaveChangesAsync(CancellationToken ct = default)`, "`SaveChangesAsync`/`CommitAsync` never called" | `IUnitOfWork.CommitAsync(CancellationToken ct)`, "`CommitAsync` never called" |
| Groundwork tests | no section for `Error.Forbidden` and `CurrentUserExtensions` | new section "Feature: Groundwork (Carina)" with 5 cases (1 for `Error.Forbidden`, 1 for `GetMemberId`, 3 for `IsAdmin`) |

- **Why:** the groundwork PR renames the method; a run with the old name would generate tests that implement a method that no longer exists. The new code (`Error.Forbidden`, `CurrentUserExtensions`) had no tests, and SonarQube failed the quality gate on the PR (65.4% coverage on new code, minimum 80%).
- **Who:** Carina Coman (edit only, no generation). The run is done by Anca Ilenei on the branch `feature/groundwork` and will be logged as the next run of this skill.
- **Commits:** `b22b04d`, `b56c59a`

### Run 5, skill v8 (Feature: Groundwork)

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Groundwork (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session, on `feature/groundwork`)
- **Skill version:** v8, commit b56c59a; section written by Carina Coman, run by Ancuța Ilenei
- **Result:** 1 new test in `Domain/Common/ErrorTests.cs` (`Error.Forbidden`) and a new file `Common/Auth/CurrentUserExtensionsTests.cs` with 4 tests (`GetMemberId`, `IsAdmin` for Admin, Student and no role claim), users built by hand with `ClaimsPrincipal`, no JWT and no mocks. No existing test changed, nothing changed in `src/`. All 151 unit and 41 integration tests pass. Coverage of `Domain/` and the handlers stays **100%**: 420/420 lines, 130/130 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. `reportgenerator` is not installed as a tool in the repository; the run used `dotnet dnx dotnet-reportgenerator-globaltool`.
    2. The report filter (`Domain` + `*Handler.cs`) leaves out `Common/Auth/CurrentUserExtensions.cs`. The raw `coverage.opencover.xml` shows it at 100%, but the skill should say whether `Common/` belongs in the report.
    3. The folder layout in the skill lists 10 handler test files; there are 12 since Login and Register.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill b56c59a; generated code c20b3af

---

---
### Run 6, skill v9 (Feature: Ownership)

- **Why a new version:** week 4 added the ownership rules: `UpdateListing`, `DeleteListing`, `UpdateMember` and `DeleteMember` commands got `RequesterId` and `IsAdmin`, and the handlers return 403 (`Listing.NotOwner`, `Member.NotOwner`) to anyone who is not the owner or an Admin. `Reservation.Cancel()` became `internal`. The existing handler tests no longer compiled and the new branches had no tests.

#### Change to SKILL.md (v8 → v9) and why

| # | v8 (before) | v9 (after) | Why |
|---|---|---|---|
| 1 | Nothing about ownership. | New section *"Feature: Ownership (Anca)"*: the files to read, how to update the existing tests (the requester is the owner, `IsAdmin` false), and the test list per handler: another member → `Forbidden`, an Admin → success, unknown id → `NotFound`, not `Forbidden`. | Without it Claude would only fix the compile errors and miss the new branches. |
| 2 | — | *"The order in all four handlers: 404 ..., then 403, then the other checks"* + a test for a non-owner sending a taken email → `Member.NotOwner`, not `Member.EmailTaken`. | Proves the order of the checks, so a stranger never learns which emails exist. |
| 3 | — | Rename the test at `ListingTests.cs:289` (name only). | Problem 2 from the week 3 code review (`REVIEW.md`). |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them, update the ones that no longer compile, and add the tests that are missing for the current code, following the "Feature: Ownership (Anca)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v9, commit 5dd5624 (committed as "v8" on the ownership branch; renumbered because the groundwork PR made v8); section written and run by Ancuța Ilenei
- **Result:** 13 new tests and 13 updated tests in the four handler test files, 1 test renamed in `ListingTests.cs`, no test deleted, nothing changed in `src/`. All 159 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 432/432 lines, 146/146 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. `reportgenerator` is not installed as a tool in the repository; the run used `dotnet dnx dotnet-reportgenerator-globaltool`. That version differs from the one used before, so the report's icons and scripts changed too. The skill should say how to get the tool and pin its version.
    2. `Member.Create` generates the member's id, so the member tests use `member.Id` as owner; only `OtherMemberId` and `AdminId` are fixed there.
- **Change to SKILL.md:** none after the run; v9 is the final version.
- **Commits:** skill 5dd5624; generated code 888d779

### Run 7, skill v10 (Feature: Reservations)

- **Why a new version:** week 4 added four slices: `PublishListing`, `ReserveListing`, `CancelReservation` and `GetMyReservations`. Their handlers had no unit tests.

#### Change to SKILL.md (v9 → v10) and why

| # | v9 (before) | v10 (after) | Why |
|---|---|---|---|
| 1 | Nothing about reservations. | New section *"Feature: Reservations (Sara)"*: the order of the checks in each handler (404, then 403, then the domain errors), the four test files, the arrange rules (fixed `Now`, named ids, the buyer must exist in `FakeMemberRepository`) and the list of tests per handler. | Without it Claude would have to guess the order of the checks and which errors each handler returns. |
| 2 | — | *"the buyer already has 3 active reservations ... → `Reservation.LimitReached`; the buyer has 2 active and 1 cancelled reservation → success (cancelled ones do not count)"* | Proves the README rule "at most 3 active reservations" from both sides. |
| 3 | — | *"a reservation created more than 48 h before `Now` → status `Expired`, and the reservation in the repository is still `Active`"* | Proves the README rule "expired reservations shown as expired" without changing data. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Reservations (Sara)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v10, commit a6f5866; section written and run by Sara Matasel
- **Result:** four new test files (`PublishListingHandlerTests`, `ReserveListingHandlerTests`, `CancelReservationHandlerTests`, `GetMyReservationsHandlerTests`), no existing test changed. All 189 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 525/525 lines, 180/180 branches. The report in `coverage-report/` was generated again.
- **Problems seen:**
  1. After the tests in the list, branch coverage was 99.4%: `ReserveListingHandler.cs:36` (`r.MemberId == command.BuyerId`) was never false, because no test had a reservation of another member. The run added one test that is not in the list: *"another member has active reservations → they do not count against the buyer"*. The test is correct and kept; the list in the skill missed this case.
- **Change to SKILL.md:** none after the run; v10 is the final version.
- **Commits:** skill a6f5866; generated code 927334b

### Run 8, skill v11 (Feature: Authentication, confirm password)

- **Why a new version:** the professor asked that register also takes the password a second time. `RegisterCommand` got `ConfirmPassword`, and `RegisterHandler` returns `Auth.PasswordsDoNotMatch` when the two differ. The existing register tests no longer compiled.

#### Change to SKILL.md (v10 → v11) and why

| # | v10 (before) | v11 (after) | Why |
|---|---|---|---|
| 1 | Register tests built `RegisterCommand` with three values. | *"Update the existing `RegisterHandlerTests` so every command sends the same value in both (they must keep testing what they tested before)."* | So the old tests compile and keep their meaning. |
| 2 | — | *"`ConfirmPassword` different from `Password` (also a [Theory] with only the letter case different, `"secret123"` vs `"Secret123"`) → `Auth.PasswordsDoNotMatch`, nothing stored, `CommitAsync` never called; a weak password with a different confirmation → `Auth.WeakPassword` (the weak check comes first)."* | Tests the new rule, that the comparison is case-sensitive, and the order of the two checks. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them, update the RegisterHandlerTests that no longer compile, and add only the confirm-password tests from the "Feature: Authentication (Sara)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v11, commit ae11e33; written and run by Sara Matasel
- **Result:** in `RegisterHandlerTests.cs`, 6 existing tests updated (same value in `ConfirmPassword`), 2 new tests (a [Theory] with 2 cases for different passwords, a [Fact] for weak + different). No test deleted. All 192 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 528/528 lines, 182/182 branches. The report in `coverage-report/` was generated again.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v11 is the final version.
- **Commits:** skill ae11e33; generated code a79e111

### Run 9, skill v11 (Feature: Listing description)

- **Why a new run:** a listing gets an optional description (at most 1000 characters), which the ML models of week 5 (category and spam) need as input text. `Listing.Create`, `Listing.UpdateDetails`,- **Prompt:** `We are on branch feature/listing-description. Listings get an optional Description. The domain, the configuration, the migration and the CreateListing slice are already done. Finish the rest (UpdateListing slice, GetListingByIdResponse, unit tests, integration tests, Postman, README, PROMPTS.md). Rules: do not change existing tests except where a change is needed to compile; do not touch the ListingStatus or ReservationStatus enums; do not commit .idea or .db files; do not commit or push, I will do that. At the end run dotnet build and dotnet test, show me the results and list every file you changed.` (new session)
- **Skill version:** v11, commit ae11e33; run by Sara Matasel
- **Result:** 5 new tests in `Domain/Listings/ListingTests.cs` for `Listing.Create` with a description (no description → null, whitespace → trimmed, blank → null, exactly 1000 → success, 1001 → `Listing.DescriptionTooLong`), 5 new tests for `UpdateDetails` with a description (trimmed, blank → null, 1000 → success, 1001 → `Listing.DescriptionTooLong` and listing unchanged, no description → null), 3 new tests in `CreateListingHandlerTests` (passes trimmed, no description → null, 1001 → `DescriptionTooLong` with nothing stored or saved), 3 new tests in `UpdateListingHandlerTests` (passes trimmed, no description → null, 1001 → `DescriptionTooLong` with listing unchanged). No existing unit test deleted. All unit tests pass.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v11 is the final version.
- **Commits:** generated code on feature/listing-description

### Run 10, skill v12 (Feature: Surviving mutants)

- **Why a new run:** Stryker left 58 mutants alive on main; the goal is a mutation score of 100% without changing production code.
- **Prompt:** `Use the unit-testing skill, section "Feature: Surviving mutants (Diana)". Follow its rules exactly: never change production code, keep every existing test and its name, in existing tests only add the Error.Message assertions described in point 1. Point 1 also covers the new Listing.DescriptionTooLong error. At the end run dotnet test, show me the results and list every file you changed. Do not commit or push.` (new session)
- **Skill version:** v12 (section written by Diana Preutu); run by Sara Matasel
- **Result:** point 1 of the section: Error.Message assertions were added to the existing tests that check an error code (including the new Listing.DescriptionTooLong error). No new tests (210 unit tests before and after). Points 2–5 were not generated, because the rule "in existing tests only add Error.Message assertions" blocked point 2 (see Run 11). No production code changed. All unit tests pass. Stryker after the run: 9 mutants still alive, exactly points 2–5.
- **Change to SKILL.md:** after the run, the rule "only add Error.Message assertions in existing tests" contradicted point 2 (UpdateCount in the existing success tests). The rule was changed to also allow the UpdateCount assertions (commit b9b496b, Diana Preutu). The tests from this run already follow the new rule; points 2–5 were run in Run 11.
- **Commits:** skill fa9b3e3; generated code 22b1fc6

### Run 11, skill v13 (Feature: Surviving mutants, points 2–5)

- **Why a new run:** in Run 10 the rule "in existing tests only add Error.Message assertions" blocked point 2. The rule was changed to also allow the UpdateCount assertions (commit b9b496b, Diana Preutu), then points 2–5 were run.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them. In the "Feature: Surviving mutants" section, point 1 is already done; only implement points 2, 3, 4 and 5 (the UpdateCount and VerifyCount counters in the fakes and their assertions, the three boundary tests, the login test with an empty password, and the reservations order test). Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results.` (new session)
- **Skill version:** v13, commit b9b496b; run by Sara Matasel
- **Result:** UpdateCount in FakeListingRepository and FakeMemberRepository, VerifyCount in FakePasswordHasher; UpdateCount = 1 asserted in the success tests of UpdateListing, UpdateMember, PublishListing and CancelReservation; 5 new tests: title of exactly 100 characters, member name of exactly 2 and exactly 100 characters, login with an empty password (VerifyCount = 0), two reservations with the same CreatedAt ordered by ReservationId. 215/215 unit tests pass; coverage 100% lines (535/535) and 100% branches (188/188). No production code changed.  Stryker after the run: **100%** (326 mutants killed, 0 survived); the two equivalent mutants are marked with `// Stryker disable once String` (commit 7c61905).
- **Problems seen:** none.
- **Change to SKILL.md:** none.
- **Commits:** skill b9b496b; generated code 505b060

### Run 12, skill v14 (Feature: Code review v5 renames)

- **Why a new version:** code review v5 (`REVIEW.md`, rows 12 and 13, issue #47) found two test names that do not match what the test does or break the `Given_<state>_When_<action>_Then_<expected>` convention.

#### Change to SKILL.md (v13 → v14) and why

| # | v13 (before) | v14 (after) | Why |
|---|---|---|---|
| 1 | Nothing about these two tests. | New section *"Feature: Code review v5 renames (Carina)"*: rename `Given_CancelledReservation_When_CheckingExpirationAfterExpiry_Then_ReturnsFalse` to `..._When_CheckingExpirationAtExpiry_...` and keep `Now.AddHours(48)`; rename `Given_ActiveListing_When_BuyerIdIsEmpty_Then_ReturnsValidationError` to `..._When_ReservingWithEmptyBuyerId_...`. Change only the names. | Rows 12 and 13 of code review v5. For row 12 the name changes, not the value, because mutation testing needs the exact 48 h boundary. |
| 2 | *"If unit tests already exist, keep them ... Then generate the coverage report again."* | *"Only names change, so the coverage does not change: do not generate the coverage report again for this feature."* | A rename does not change the coverage; a new report would only change files in `coverage-report/`. |

- **Honest note:** the two renames were first made by hand and then reverted (commit f637052) before the run, because test changes only go through the skills.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only do what the "Feature: Code review v5 renames (Carina)" section asks for (two test names). Follow the skill exactly, including the "Never do" list. When you finish, run dotnet test, tell me the results and list every file you changed.` (new session)
- **Skill version:** v14, commit 14cdf0c; section written by Carina Coman, run by Sara Matasel
- **Result:** the two tests were renamed (ReservationTests: ..._When_CheckingExpirationAtExpiry_...; ListingTests: ..._When_ReservingWithEmptyBuyerId_...). No other change. 215/215 unit tests pass, the same number as before.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v14 is the final version.
- **Commits:** skill 14cdf0c; generated code dd316cc

### Run 13, skill v14 (Feature: Price suggestion)

- **Why a new run:** B3 added the SuggestPrice query handler and the IPriceSuggester interface; the handler needs unit tests and a fake for the suggester.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md, section "Feature: Price suggestion (Sara)". Unit tests already exist: keep them and add only what this section asks. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the results. Do not commit or push.` (new session)
- **Skill version:** v14 (new section "Feature: Price suggestion"); run by Sara Matasel
- **Result:** new FakePriceSuggester (SuggestedPrice, CallCount, last arguments); 14 new tests in SuggestPriceHandlerTests: valid query with the trimmed title passed to the suggester, null description, unknown category and condition, description and title limits (exactly at the limit, over the limit, at the limit with spaces around), empty title, and the order of the checks. Every failure test checks the code, the exact message and that the suggester was not called. 229/229 unit tests pass; coverage 100% lines (558/558) and branches; SuggestPriceHandler 100%.
- **Problems seen:** none.
- **Change to SKILL.md:** none.

### Run 14, skill v16 (Feature: Confirm sale)

- **Why a new run:** A1 added Reservation.Complete, Listing.ConfirmSale, the Listing.Sold check in UpdateDetails and the ConfirmSale handler.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md, section "Feature: Confirm sale (Sara)". Unit tests already exist: keep them and add only what this section asks. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the results and every file you changed. Do not commit or push.` (new session)
- **Skill version:** v16, commit ad2e9f5; run by Sara Matasel
- **Result:** 21 new tests: 2 in ReservationTests (Complete), 10 in ListingTests (ConfirmSale before and exactly at the 48 h expiry, unknown, cancelled and already completed reservation, a sold listing cannot be updated, published or reserved), 9 in the new ConfirmSaleHandlerTests (seller and Admin succeed, buyer and another member get Listing.NotOwner, 404 before 403, 403 before the reservation check, expired reservation). Every failure test checks the code, the exact message, SaveCount 0 and UpdateCount 0. 250/250 unit tests pass.
- **Problems seen:** coverage was 99.8%: Listing.ConfirmSale checked "reservation not Active" itself and then again through Reservation.Complete(), so the failure branch after Complete() could never run. The explicit check was removed from Listing.cs (same behaviour: IsExpired is false for a reservation that is not active, and Complete() returns the same Reservation.NotActive error). After that: coverage 100% lines and branches, all tests still pass.
- **Change to SKILL.md:** none.

### Run 15, skill v17 (Feature: Reservation expiry)

- **Why a new run:** A4 added Reservation.Expire, Listing.ExpireReservations and the ExpireReservations handler used by the background job.
- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md, section "Feature: Reservation expiry (Sara)". Unit tests already exist: keep them and add only what this section asks. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the results and every file you changed. Do not commit or push.` (new session)
- **Skill version:** v17, commit 6c1a3bf; run by Sara Matasel
- **Result:** 13 new tests: Reservation.Expire, Listing.ExpireReservations (one second before and exactly at the 48 h expiry, cancelled and sold reservations, a draft listing, calling it twice, another buyer can reserve after expiry) and the new ExpireReservationsHandlerTests (no listings, one expired among others, two expired with one commit, nothing expired). 263/263 unit tests pass.
- **Problems seen:** The fix (diagnosed in a separate Claude Code session, then applied there): GetAllAsync now tracks the listings, with a comment explaining why. All tests pass: 10 architecture, 263 unit, 4 ML, 115 integration.
- - **Change to SKILL.md:** none.

### Run 16, skill v18 (Feature: Favorites)

- **Why a new version:** A3 added the `Favorite` entity and three handlers (`AddFavorite`, `RemoveFavorite`, `GetMyFavorites`). They had no unit tests, and the Stryker config also mutates them.

#### Change to SKILL.md (v17 → v18) and why

| # | v17 (before) | v18 (after) | Why |
|---|---|---|---|
| 1 | Nothing about favorites. | New section *"Feature: Favorites (Anca)"*: the rules of `Favorite.Create`, the order of the checks in each handler, the new fake `FakeFavoriteRepository` (`Items`, `RejectAdd`, `DeleteCount`) and the list of tests per class. | Without it Claude would have to guess the order of the checks and which errors each handler returns. |
| 2 | — | Boundaries and order: `PageSize` 100 → success, 0 and 101 → error, `Page` 0 → error; two favorites with the same `CreatedAt` ordered by `ListingId`; page 2 of 2 → the oldest only. | The same kind of tests killed the paging mutants of `GetListings`; without them the mutation score would drop. |
| 3 | — | Removing another member's favorite → `Favorite.NotFound`, and that favorite is still there. | Proves that a member can only remove their own favorites. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Favorites (Anca)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v18, commit fa4e09a (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new fake `Fakes/FakeFavoriteRepository.cs` and four new test files: `FavoriteTests` (7), `AddFavoriteHandlerTests` (9), `RemoveFavoriteHandlerTests` (3), `GetMyFavoritesHandlerTests` (12 cases). No existing test changed, nothing changed in `src/`. All 294 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 727/727 lines, 256/256 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. The message of `Favorite.Duplicate` contains the Id generated inside the domain, so it cannot be checked exactly; the run used `.Match("A favorite with id '*' already exists.")`, as `CreateListingHandlerTests` does for `Listing.Duplicate`.
    2. The section names only `SellerId` and `OtherMemberId`; `RemoveFavorite` and `GetMyFavorites` also need the requester, so the run added a fixed `MemberId`.
    3. "A favorite whose listing is missing" cannot happen in the database (cascade delete), but it is the branch the section asks for and it is covered.
    4. `reportgenerator` is not installed; the run used `dotnet dnx dotnet-reportgenerator-globaltool`. Old files in `TestResults/` would have been merged into the report; the run moved them away first.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill fa4e09a; generated code 25c29b8

### Run 17, skill v19 (Feature: Refresh token and logout)

- **Why a new version:** extra task 6 (refresh token + logout). The access token now lasts 15 minutes; login also returns a refresh token (64 random bytes, valid 7 days, only its SHA-256 hash is stored); `POST /auth/refresh` rotates it and `POST /auth/logout` revokes it. `ITokenService` got `CreateRefreshToken()` and `LoginHandler` got three new dependencies, so `FakeTokenService` and `LoginHandlerTests` no longer compiled, and the new domain class and the two new handlers had no tests.

#### Change to SKILL.md (v18 → v19) and why

| # | v18 (before) | v19 (after) | Why |
|---|---|---|---|
| 1 | Nothing about refresh tokens. | New section *"Feature: Refresh token and logout (Carina)"*: the files to read, the order of the checks in `RefreshHandler` and `LogoutHandler`, a new `FakeRefreshTokenRepository`, `CreateRefreshToken()` in `FakeTokenService` (`"refresh-1"`, `"refresh-2"`, ...), and the test list for `RefreshToken`, `LoginHandler`, `RefreshHandler` and `LogoutHandler`. | Without it Claude would only fix the compile errors and miss the new branches. |
| 2 | — | *"a token created exactly 7 days before `Now` → `Invalid`; ... 7 days minus 1 second before `Now` → success"* and *"`IsActive`: ... false exactly at `ExpiresAt`"*. | Tests both sides of the expiry boundary, so a `<` → `<=` mutant is killed. |
| 3 | — | *"every `new LoginHandler(...)` also gets `new FakeRefreshTokenRepository()`, `new FakeUnitOfWork()` and `new FixedTimeProvider(Now)` at the end"*. | The existing login tests must compile and keep testing what they tested before. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them, update the LoginHandlerTests that no longer compile, and add the fakes and tests from the "Feature: Refresh token and logout (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results, the coverage for Domain/ and the handlers, and every file you changed.` (new session)
- **Skill version:** v19, commit 90e55b8 (committed as "v18"; renumbered because the favorites PR made v18, and in the merge with main the section was added again after "Feature: Favorites (Anca)"); section written and run by Carina Coman
- **Result:** `FakeTokenService` got `CreateRefreshToken()` and `RefreshTokenCount`; new `FakeRefreshTokenRepository`; the 8 existing `new LoginHandler(...)` calls in `LoginHandlerTests` were updated so they compile again (no test deleted or renamed). New tests: 2 in `LoginHandlerTests`, 10 in the new `Domain/Members/RefreshTokenTests.cs`, 10 test cases in `Features/Auth/Refresh/RefreshHandlerTests.cs` and 6 in `Features/Auth/Logout/LogoutHandlerTests.cs`. Every error assertion checks `Type`, `Code` and `Message`, and every test uses a fixed `Now`. Nothing changed in `src/`. 291/291 unit tests pass; coverage stays **100%**: `Domain/` 282/282 lines and 88/88 branches, handlers 421/421 lines and 162/162 branches.
- **Problems seen:**
  1. `reportgenerator` is not installed on this machine, and Claude Code's auto mode blocked downloading it with `dnx dotnet-reportgenerator-globaltool` (the same tool problem as Runs 5 and 6). The run read the coverage numbers from `coverage.opencover.xml` instead. The HTML report was generated by hand afterwards with the README command (`dotnet tool install -g dotnet-reportgenerator-globaltool`, then `reportgenerator ...`), so the report tool version may differ from the previous report.
  2. A first attempt to write the three new test files with one long shell command failed (unterminated quote). Nothing was written; the run used its Write tool instead.
- **Change to SKILL.md:** none after the run; v19 is the final version.
- **Commits:** skill 90e55b8; generated code 733b2ab

### Run 18, skill v20 (Feature: Category suggestion)

- **Why a new version:** B4 added the `SuggestCategory` query handler behind `ICategorySuggester`. It had no unit tests.

#### Change to SKILL.md (v19 → v20) and why

| # | v19 (before) | v20 (after) | Why |
|---|---|---|---|
| 1 | Nothing about category suggestion. | New section *"Feature: Category suggestion (Anca)"*: the order of the checks, the new fake `FakeCategorySuggester` (`Suggestion`, `CallCount`, `LastTitle`, `LastDescription`) and the list of tests. | Without it Claude would have to guess the order of the checks and what the fake returns. |
| 2 | — | A test with the fake set to `Notes` / `0.5`. | Proves the handler returns the suggester's values and does not use a fixed answer. |
| 3 | — | `CallCount` 0 in every failure test, and the boundaries 100/101 and 1000/1001 with and without spaces. | The same checks as in "Price suggestion (Sara)"; they kill the mutants on the length checks and on the trim. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Category suggestion (Anca)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v20, commit 57316ee (committed as "v19"; renumbered because the refresh token PR made v19) (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new fake `Fakes/FakeCategorySuggester.cs` and `Features/Listings/SuggestCategory/SuggestCategoryHandlerTests.cs` (11 tests, 12 cases). No existing test changed, nothing changed in `src/`. All 306 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 747/747 lines, 266/266 branches; `SuggestCategoryHandler` 20/20 lines, 10/10 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. The section says the messages are "the same as SuggestPrice" but does not write them out; the run took them from the handler.
    2. The folder layout at the top of the skill is out of date ("10 handlers", and the newer fakes are not listed).
    3. `reportgenerator` is not installed; the run used `dotnet dnx dotnet-reportgenerator-globaltool` and moved the old `TestResults/` folder away first. The skill does not say to empty `coverage-report/` before generating it again.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 57316ee; generated code f98a859

### Run 19, skill v21 (Feature: Spam and moderation)

- **Why a new version:** B5 added the `Report` entity, `Listing.Hide()`, three moderation handlers, the spam check in `PublishListing` (new constructor) and the hidden listings in favorites. None of it had unit tests, and the existing `PublishListingHandlerTests` no longer built.

#### Change to SKILL.md (v20 → v21) and why

| # | v20 (before) | v21 (after) | Why |
|---|---|---|---|
| 1 | Nothing about reports or moderation. | New section *"Feature: Spam and moderation (Anca)"*: the rules of `Report.Create`, `Listing.Hide()`, the order of the checks in each handler, the fakes `FakeSpamDetector` and `FakeReportRepository`, and the list of tests per class. | Without it Claude would have to guess the order of the checks and the error codes. |
| 2 | — | *"every `new PublishListingHandler(repository, unitOfWork)` becomes `new PublishListingHandler(repository, unitOfWork, spamDetector, reports, new FixedTimeProvider(Now))` … Do not change what the existing tests assert"*. | The new constructor broke the old tests; the skill says exactly what may change in them. |
| 3 | — | Boundaries and order: reason 500/501 with and without spaces, `PageSize` 100/101, ties ordered by `LastReportedAt` then `ListingId`, hidden listings left out of both lists. | The same kind of tests that kept the mutation score at 100% for favorites and the listing search. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Spam and moderation (Anca)" section (that section also says how to update the existing PublishListingHandlerTests, which currently do not compile). Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v21, commit b9967f5 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new fakes `FakeSpamDetector` and `FakeReportRepository`; new files `ReportTests` (18), `ReportListingHandlerTests` (10), `GetReportedListingsHandlerTests` (14), `HideListingHandlerTests` (4); added tests in `ListingTests` (+5), `FavoriteTests` (+1), `GetMyFavoritesHandlerTests` (+1), `PublishListingHandlerTests` (+2, and the new constructor in the 5 old ones, whose assertions did not change). Nothing changed in `src/`. All 389 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 945/945 lines, 334/334 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. `ReportedListingItem.From` is in the response file, so the coverage filter (`*Handler.cs`) does not show it; it is tested through the handler.
    2. The section does not give a type for `FakeSpamDetector.LastPrice`; the run used `decimal?`.
    3. A favorite or a report cannot be created for a hidden listing, so the "hidden" tests create them on an active listing and call `Hide()` afterwards; the section only hints at it.
    4. The failure checks for `PublishListing` (detector not called, no report) went into the three existing failure tests instead of new tests, as the change paragraph says.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill b9967f5; generated code ffc2895

### Run 20, skill v22 (Feature: Seller reviews)

- **Why a new version:** A2 added the `Review` entity and two handlers (`AddReview`, `GetMemberReviews`). They had no unit tests, and the Stryker config also mutates them.

#### Change to SKILL.md (v21 → v22) and why

| # | v21 (before) | v22 (after) | Why |
|---|---|---|---|
| 1 | Nothing about reviews. | New section *"Feature: Seller reviews (Anca)"*: the rules of `Review.Create`, the order of the checks in each handler, the new fake `FakeReviewRepository` (`Items`, `RejectAdd`) and the list of tests per class. | Without it Claude would have to guess who may review and in which order the errors come. |
| 2 | — | How to build a sold listing (`Publish`, `Reserve`, `ConfirmSale`) and that the buyer is the member of the completed reservation; `Review.NotBuyer` is `Forbidden`, also for the seller. | The buyer is not a field of `Listing`; without this the tests would not know who is allowed. |
| 3 | — | The average over **all** reviews, not only the page (three reviews, page 2 of size 2), 4.33 for 5, 4, 4, and paging checked before the member. | These are the places where a wrong average or a wrong order would go unnoticed. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Seller reviews (Anca)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results and the coverage for Domain/ and the handlers.` (new session)
- **Skill version:** v22, commit 4764342 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new fake `Fakes/FakeReviewRepository.cs` and three new test files: `ReviewTests` (21), `AddReviewHandlerTests` (9), `GetMemberReviewsHandlerTests` (13). No existing test changed, nothing changed in `src/`. All 432 unit tests pass. Coverage of `Domain/` and the handlers stays **100%**: 1024/1024 lines, 356/356 branches. The report in `coverage-report/` was generated again.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. For "another member → `Review.NotBuyer`" the other member must also be in `FakeMemberRepository`, because the handler checks `Member.NotFound` first; the section does not say so.
    2. The section names fixed ids (`SellerId`, `BuyerId`) and also asks for `member.Id`; the run used `member.Id` for the member that must exist and the fixed id only for the "unknown member" tests.
    3. Draft, active and reserved → `Review.SaleNotCompleted` became three tests instead of a `[Theory]`, because each needs a different arrange.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 4764342; generated code 1d76998

### Run 25, skill v27 (Feature: Minimum category confidence)

- **Why a new version:** `SuggestCategoryHandler` has a new rule: below `MinConfidence` (0.7) the response has `Category` null and keeps the confidence. `SuggestCategoryResponse.Category` is now nullable. The new branch had no tests, and one existing test used a confidence of 0.5 and expected a category.

#### Change to SKILL.md (v26 → v27) and why

| # | v26 (before) | v27 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the minimum confidence. | New section *"Feature: Minimum category confidence (Diana)"*: the file (`SuggestCategoryHandlerTests`, add to it) and 3 tests: 0.9 and exactly 0.7 → the suggested category, 0.69 → `Category` null and `Confidence` still 0.69. | Proves both sides of the `>=` check, at the exact boundary. |
| 2 | — | *"If existing tests use a confidence below 0.7 and expect a category, raise the fake's confidence in them to 0.9; change nothing else."* | The old test with 0.5 would fail for the right reason; the rule keeps its change to one value. |

- **Prompt:** `Run the unit-testing skill for the section "Feature: Minimum category confidence (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v27, commit dc876be (section written and run by Diana Preutu with Claude Code)
- **Result:** In `SuggestCategoryHandlerTests`: the existing Notes test raised from confidence 0.5 to 0.9 (nothing else changed in it), a `[Theory]` for 0.9 and 0.7 (2 cases) and one test for 0.69 → `Category` null, `Confidence` 0.69. `FakeCategorySuggester` was not changed (its `Suggestion` property already sets the confidence). Nothing in `src/` changed by the run. 435/435 unit tests pass. The coverage report was not generated again in this run (it is not part of the PR).
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v27 is the final version.
- **Commits:** skill dc876be; generated code 020f950

### Run 26, skill v28 (Feature: Maximum price)

- **Why a new version:** Week 6, task 2. `Money.Create` has a new branch: an amount above `Money.MaxAmount` (100000) returns `Money.TooHigh`. `Listing.Create` builds its price with `Money.Create`, so it gets the same rule. The new branch had no tests.

#### Change to SKILL.md (v27 → v28) and why

| # | v27 (before) | v28 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the maximum price. | New section *"Feature: Maximum price (Diana)"*: the file (`MoneyTests`, with one test added to `ListingTests`) and 3 tests. | Without it Claude would not know the limit, the error code or where the new tests go. |
| 2 | — | *"exactly `Money.MaxAmount` → success"* and *"`Money.MaxAmount + 0.01m` → `Validation`, code `Money.TooHigh`, exact message from the code"*. | Proves both sides of the `>` check, not only a value far above the limit. |
| 3 | — | *"`Listing.Create` with a price above the maximum → the same error"*. | Proves the rule reaches the listing, not only the value object. |

- **Prompt:** `Run the unit-testing skill for the section "Feature: Maximum price (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v28, commit 0d26845 (section written and run by Diana Preutu with Claude Code)
- **Result:** 3 new tests, nothing else changed in the tests and nothing in `src/` by the run: `MoneyTests` (exactly the maximum succeeds; maximum + 0.01 → `Money.TooHigh` with the exact message `Price must be at most 100000 lei.`) and `ListingTests` (`Listing.Create` above the maximum → the same error). 435/435 unit tests pass. The coverage report was not generated again in this run (it is not part of the PR).
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v28 is the final version.
- **Commits:** skill 0d26845; generated code c989e44

## integration-testing

Skill written and run by Ilenei Ancuta-Bianca.

### Run 1, skill v1

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md to write the integration tests for CampusMarket. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v1, commit 6df1d61 (a draft with three `TODO(efcore)` lines)
- **Result:** Claude reported build 0 warnings, 90/90 unit tests and 20/20 integration tests three runs in a row. The test code itself was good (status, body and database checked in every test, nothing changed in `src/`). Discarded because of the problems below.

#### Problems seen (bad output from v1)

1. The skill said not to run it until the TODOs were filled in; Claude filled them in **inside the skill** and changed its status by itself.
2. It wrote this run in `PROMPTS.md` by itself, because the skill's check list asked it to.
3. `Guid.NewGuid()` for `SellerId` in 11 places, against the "fixed test data" rule, because the skill's own examples did that:

   ```csharp
   SellerId: Guid.NewGuid(),
   ...
   var listing = Listing.Create(Guid.NewGuid(), "Manual POO", Category.Textbook, Condition.Good, 50m, DateTimeOffset.UtcNow).Value;
   ```

4. Section 8 asked to delete old test files that no longer existed.

#### Change to SKILL.md (v1 → v2) and why

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | *"DbContext: `TODO(efcore): class name and namespace`."* | *"DbContext: `CampusMarketDbContext` in `CampusMarket.Api.Infrastructure.Persistence`, registered … with `AddDbContext<CampusMarketDbContext>((sp, options) => options.UseSqlite(...))`"* | The EF Core code was merged, so the real names are known. |
| 2 | *"Write the run in `PROMPTS.md`"* and no rule about editing the skill. | *"Never edit `.claude/skills/` (this skill included) or `PROMPTS.md`. If something in this skill is wrong … stop and report it"* + *"Never commit or push; report the results."* | The log and the skill are written by people, not by the run. |
| 3 | *"not random values, except `Guid.NewGuid()` for an unknown id"*, but the examples used `Guid.NewGuid()` and `DateTimeOffset.UtcNow` | One fixed `SellerId = Guid.Parse("11111111-…")` and a fixed `CreatedAt`, used in every example. | Claude copies the examples, so the examples must follow the rules. |
| 4 | *"Delete exactly these files: `TestApi.cs`, `ListingsApiTests.cs`, `MembersApiTests.cs`"* | *"The week 2 API tests … were already deleted by the EF Core work. Do not recreate them…"* | The files were already gone. |

- **Commits:** skill 6df1d61; generated code: discarded

### Run 2, skill v2

- **Prompt:** same as run 1, in a new Claude Code session
- **Skill version:** v2 (committed as dcb03e4 right after the run; the content that ran was the same)
- **Result:** Claude reported build 0 warnings, 90/90 unit tests and 20/20 integration tests three runs in a row. The run 1 problems were gone. Discarded because of the problems below.

#### Problems seen (bad output from v2)

1. Its first draft used a `Condition` value that does not exist (`LikeNew`), because the skill did not list the enum values.
2. It picked the FluentAssertions version on its own, because the skill only said `7.*`.
3. Only the Delete 404 tests seeded an entity first; the GetById 404 tests did not, so they could not show that the failed call touched nothing.
4. The listing update test did not change the category.

#### Change to SKILL.md (v2 → v3) and why

| # | v2 (before) | v3 (after) | Why |
|---|---|---|---|
| 1 | No enum list. | *"Enum values (use only these, never guess): `Category` Textbook, Notes, Electronics, LabEquipment, Other; `Condition` New, Good, Worn; …"* | Claude guessed a value that does not exist. |
| 2 | *"`FluentAssertions` 7.* (never 8)"* | *"`FluentAssertions` 7.2.0, the same as `CampusMarket.UnitTests` (never 8)"* | Both test projects must use the same version. |
| 3 | `GetListingByIdTests` · unknown id · *"404 `Listing.NotFound`"* | *"404 `Listing.NotFound`; the seeded listing is unchanged"* + *"unknown id (404): seed one entity first, then check it is still there and unchanged"* | A 404 test on an empty database proves nothing about side effects. |
| 4 | `UpdateListingTests` · *"valid body"* | *"body that changes title, category, condition and price"* | Every field has to be checked. |

- **Commits:** skill dcb03e4; generated code: discarded

### Run 3, skill v3

- **Prompt:** same as run 1, in a new Claude Code session
- **Skill version:** v3, commit c6b3868
- **Result:** build 0 errors, 0 warnings; 20/20 integration tests three runs in a row; `dotnet test` on the whole solution: 110/110 (90 unit, 20 integration). Only real enum values; FluentAssertions 7.2.0; every 404 test seeds an entity and checks it is unchanged; the update tests change every field; `Guid.NewGuid()` only for unknown ids; nothing changed in `src/`, `tests/CampusMarket.UnitTests`, the skill or `PROMPTS.md`.

#### After (excerpt of the final code)

`Listings/GetListingByIdTests.cs`:

```csharp
var listing = Listing.Create(SellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
await SeedAsync(listing);

var response = await Client.GetAsync($"/api/v1/listings/{Guid.NewGuid()}");

response.StatusCode.Should().Be(HttpStatusCode.NotFound);
...
var saved = await QueryDbAsync(db => db.Set<Listing>().AsNoTracking().SingleAsync());
saved.Should().BeEquivalentTo(listing);
```

- **Change to SKILL.md:** none; v3 is the final version.
- **Commits:** skill c6b3868; generated code 98cbece

### Run 4, skill v4 (Feature: Authentication)

- **Why a new version:** week 4 added `POST /api/v1/auth/register` and `POST /api/v1/auth/login`, with rate limiting on login (5 per minute).

#### Change to SKILL.md (v3 → v4) and why

| # | v3 (before) | v4 (after) | Why |
|---|---|---|---|
| 1 | Nothing about authentication. | New section *"Feature: Authentication (Sara)"*: the test host settings (no Admin seed, test JWT key), 5 register tests and 6 login tests. | The new endpoints need tests for 201, 400, 409, 200 and 401. |
| 2 | — | *"rate limit: 5 login calls, then the 6th → 429 … helper `ExhaustLoginRateLimitAsync`. Every test gets a fresh factory, so the limit never leaks into other tests."* | In the first run the rate-limit test was missing, so this line was made explicit and the skill was run again. |

- **Honest note:** the section was used for the run, but it was saved and committed only after the run (we forgot Ctrl+S). The text in commit c67c85f is the one that was used.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Authentication" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v4, commit c67c85f
- **Result (first run):** `Auth/RegisterTests.cs` (5 tests) and `Auth/LoginTests.cs` (5 tests), all pass, but **the 429 test was missing**. Not kept.
- **Result (second run, same prompt, new session):** `Auth/LoginTests.cs` now has 6 tests, including `Given_FivePreviousLoginAttempts_When_LoggingInAgain_Then_Returns429`. Build 0 errors, 0 warnings; all tests pass; nothing changed in `src/` or in the existing tests.
- **Change to SKILL.md:** none after the second run; v4 is the final version.
- **Commits:** skill c67c85f; generated code 596301a

### Run 5, skill v5 (Feature: Listing search and paging)

- **Why a new version:** `GET /api/v1/listings` now takes query parameters, returns only `Active` listings and can return 400, while section 1 of the skill said GetAll has no failure path.

#### Change to SKILL.md (v4 → v5) and why

| # | v4 (before) | v5 (after) | Why |
|---|---|---|---|
| 1 | *"GetAll (`GET /`) · 200 + body (`items`, `count`) · none"* and *"Do not invent a failure for them."* | New section *"Feature: Listing search and paging (Diana)"*: *"For `GetListings` only, the 'no failure path' note in section 5 no longer applies."* + three seeded listings with fixed `CreatedAt`, each published before `SeedAsync`, and the test list (search, all filters, inclusive limits, default paging, page 2, the three 400 errors, unknown category). | Otherwise the skill forbids the 400 tests the feature needs. |
| 2 | — | *"Publish a listing in the domain before seeding it (`listing.Publish()`); never call the publish endpoint, which needs a login."* | Search hides drafts, and the publish endpoint needs authentication. |
| 3 | Arrows saved with the wrong encoding. | 9 arrows fixed, file saved as UTF-8. | Found in review by Sara. |

- **Honest note:** the existing test `Given_TwoListings_When_GettingListings_Then_Returns200WithBothListings` broke the CI after the Active filter, because it seeds `Draft` listings. It was fixed by hand before the run (two `Publish()` calls, commit 8d21e3e).
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Listing search and paging" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.`
- **Skill version:** v5, commit f88662b; section written by Diana Preutu, run by Sara Matasel
- **Result:** 10 new tests in `Listings/GetListingsTests.cs`. All 41 integration tests pass; nothing changed in `src/` or in the other tests.
- **Change to SKILL.md:** none; v5 is the final version.
- **Commits:** skill f88662b; generated code 0c5274d

---
### Run 6, skill v6 (Feature: Ownership)

- **Why a new version:** week 4 put `.RequireAuthorization()` on POST, PUT and DELETE, `RequireAuthorization("Admin")` on `CreateMember`, and owner checks (403) in the update and delete handlers. `CreateListingRequest` lost `SellerId`, which now comes from the token. The existing tests no longer compiled or got 401.

#### Change to SKILL.md (v5 → v6) and why

| # | v5 (before) | v6 (after) | Why |
|---|---|---|---|
| 1 | Nothing about login on the CRUD endpoints. | New section *"Feature: Ownership (Anca)"*: a table of who may call each slice and the new failures (401, 403 `Listing.NotOwner` / `Member.NotOwner`, the empty-body 403 of `CreateMember`), how to log in the existing tests with `AuthenticateAs`, and the list of tests to add. | Without it the existing tests get 401 and Claude would have to guess who logs in. |
| 2 | — | Tests for the gaps found earlier: 404 on update listing and update member, 409 `Member.EmailTaken` on create (different casing) and on update. | Planned in the week 4 issue. |
| 3 | — | Fixed ids `OtherStudentId` and `AdminId`; *"Members do not need to exist in the database to log in: the token is enough."* | Readable tests, and no extra seeding just to get a token. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them, update the ones the "Feature: Ownership (Anca)" section asks to update, and add the tests from that section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v6, commit 5dd5624; section written and run by Ancuța Ilenei
- **Result:** 12 existing tests updated (login added; `CreateListing` now checks that `SellerId` comes from the token) and 19 new tests in the six Create/Update/Delete test classes. No test deleted, nothing changed in `src/`. Build 0 errors, 0 warnings; all 60 integration tests pass (three runs in a row) and all 159 unit tests pass.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. Sections 1, 5 and 10 of the skill still describe the API before login (no 401/403 in the status table, "Test plan (20)"). The Ownership section overrides them, but the core sections should be updated.
    2. For 401 and for the 403 of `CreateMember`, the section says to check only the status; the run also checked that the body is empty (section 4 asks every test to check the body). Kept, it is stricter.
- **Change to SKILL.md:** none after the run; v6 is the final version.
- **Commits:** skill 5dd5624; generated code b0892bf

### Run 7, skill v7 (Feature: Reservations)

- **Why a new version:** the four new endpoints (`POST /listings/{id}/publish`, `POST /listings/{id}/reservations`, `DELETE /listings/{id}/reservations/{reservationId}`, `GET /members/me/reservations`) had no integration tests.

#### Change to SKILL.md (v6 → v7) and why

| # | v6 (before) | v7 (after) | Why |
|---|---|---|---|
| 1 | Nothing about reservations. | New section *"Feature: Reservations (Sara)"*: a table with route, who may call it, success and failures for each slice, the response fields, the four test files and the tests to add (status, body, then the database). | The endpoints are new, so Claude needs the contract to test it. |
| 2 | — | *"The reservation's `createdAt` comes from the real clock of the API: never compare it to a fixed time, only check `expiresAt - createdAt` is 48 h."* | The API uses the real `TimeProvider` in the tests, so a fixed time would make the test fail. |
| 3 | — | *"a reservation created 49 h ago ... → status `Expired`, while in the database it is still `Active`"* | Checks the expired display end to end. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Reservations (Sara)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v7, commit a6f5866; section written and run by Sara Matasel
- **Result:** four new test files with 24 tests (6 Publish, 7 Reserve, 7 Cancel, 4 GetMyReservations). No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; all 84 integration tests and all 189 unit tests pass. The reserve tests also prove that the new reservation is saved without `repository.Update` (see the comment in `ReserveListingHandler`).
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v7 is the final version.
- **Commits:** skill a6f5866; generated code 927334b

### Run 8, skill v8 (Feature: Authentication, confirm password)

- **Why a new version:** `RegisterRequest` got `confirmPassword`; the API answers 400 `Auth.PasswordsDoNotMatch` when it differs from `password`.

#### Change to SKILL.md (v7 → v8) and why

| # | v7 (before) | v8 (after) | Why |
|---|---|---|---|
| 1 | Register bodies had `name`, `email`, `password`. | *"Update the existing register tests so the body sends the same value as `password`. Add: `confirmPassword` different from `password` → 400 `Auth.PasswordsDoNotMatch`, nothing saved."* | The old tests must compile and keep their meaning; the new rule needs one HTTP test. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them, update the register tests so the body sends confirmPassword, and add only the confirm-password test from the "Feature: Authentication (Sara)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v8, commit ae11e33; written and run by Sara Matasel
- **Result:** in `Auth/RegisterTests.cs`, 5 existing tests updated and 1 new test (different passwords → 400, code and type checked, 0 members in the database). Build 0 errors, 0 warnings; all tests pass: 192 unit, 85 integration, 8 architecture.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v8 is the final version.
- **Commits:** skill ae11e33; generated code a79e111

### Run 9, skill v8 (Feature: Listing description)

- **Why a new run:** the optional description reaches the HTTP contract (`CreateListingRequest`, `UpdateListingRequest`, `CreateListingResponse`, `GetListingByIdResponse`), so the integration tests have to prove it end to end.
- **Prompt:** same feature prompt as the unit-testing Run 9 above (new session).
- **Skill version:** v8, commit ae11e33; run by Sara Matasel
- **Result:** 3 new tests in `Listings/CreateListingTests.cs` (POST with description → 201 with the description, GET by id returns it, row in SQLite has the trimmed description; POST without description → 201 with `description: null` and the row's `Description` is null; POST with a 1001-character description → 400 `Listing.DescriptionTooLong`, nothing saved) and 1 new test in `Listings/UpdateListingTests.cs` (PUT changes the description → 204 and the row's `Description` is the new value, `Version` is 2). The existing `GetListingByIdTests` body comparison was updated to pass `null` as the new `Description` field, the only change needed to compile. No other existing test changed. All integration tests pass.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v8 is the final version.
- **Commits:** generated code on feature/listing-description

### Run 10, skill v9 (Feature: Admin seed)

- **Why a new version:** issue #44 (code review v5, row 10): `SeedAdmin` compared the stored lower-case email with the raw `Seed:AdminEmail`, so an upper-case email in the config created a second admin and the startup failed on the unique index. The fix in `CampusMarketSetup.cs` was made by hand, as the week 5 task asks. The seed had no integration test, because the test factory turns it off. Code review v5, row 11 (issue #47): three login 401 tests did not check the database.

#### Change to SKILL.md (v8 → v9) and why

| # | v8 (before) | v9 (after) | Why |
|---|---|---|---|
| 1 | *"`CampusMarketApiFactory` sets `Seed:AdminEmail` to `""` (no default Admin in tests)"*; nothing about testing the seed. | New section *"Feature: Admin seed (Carina)"*: turn the seed on in one test with `Factory.WithWebHostBuilder(...)` and `UseSetting` for both seed settings, a helper `WithAdminSeed`, and 5 tests: upper case and spaces, the app started twice, an admin already in the database, an invalid email, an empty password. | Without it the seed cannot be tested without changing the factory. The "started twice" test is the case from issue #44. |
| 2 | Login: wrong password, invalid email format and empty password → 401, with no database check. | *"Add the database check at the end of each one ... and change nothing else in them"*: 1, 0 and 1 members. | Section 4 asks every test to check the database (code review v5, row 11). |

- **Honest note:** these tests were first written by hand and then reverted (commit f637052) before the run, because test changes only go through the skills.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add what the "Feature: Admin seed (Carina)" section asks for (the new SeedAdminTests file and the database checks in the three LoginTests). Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v9, commit edb9e2f; section written by Carina Coman, run by Sara Matasel
- **Result:** new SeedAdminTests with 5 tests (upper case and spaces in the config, the app started twice, an admin already in the database, an invalid email, an empty password) through a WithAdminSeed helper on the same in-memory connection; the database check added at the end of the three login 401 tests (1, 0 and 1 members), nothing else changed in them. dotnet build 0 warnings; 215 unit, 8 architecture and 94 integration tests pass.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v9 is the final version.
- **Commits:** skill edb9e2f; generated code dd316cc

### Run 11, skill v9 (Feature: Price suggestion)

- **Why a new run:** B3 added POST /api/v1/listings/price-suggestion, which uses the real ML model.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md, section "Feature: Price suggestion (Sara)". Integration tests already exist: keep them and add only what this section asks. Follow the skill exactly. Then run all tests and tell me the results. Do not commit or push.` (new session)
- **Skill version:** v9 (new section "Feature: Price suggestion"); run by Sara Matasel
- **Result:** 6 new tests in Listings/SuggestPriceTests with the real price-regression.zip: laptop New → 200 with a whole price ≥ 1; the laptop is priced higher than worn course notes; no token → 401; empty title → 400 Listing.TitleEmpty; description of 1001 characters → 400 Listing.DescriptionTooLong; unknown condition "Used" → 400. Every test checks that no listing was saved. All tests pass: 10 architecture, 229 unit, 4 ML, 95 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none.

### Run 12, skill v11 (Feature: Confirm sale)

- **Why a new run:** A1 added POST /api/v1/listings/{id}/reservations/{reservationId}/confirm and the Listing.Sold rule.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md, section "Feature: Confirm sale (Sara)". Integration tests already exist: keep them and add only what this section asks. Follow the skill exactly. Then run all tests and tell me the results and every file you changed. Do not commit or push.` (new session)
- **Skill version:** v11, commit ad2e9f5; run by Sara Matasel
- **Result:** 12 new tests in Listings/ConfirmSaleTests: the seller confirms (204, listing Sold, Version + 1, reservation Completed), the buyer's reservations list shows Completed, Admin 204, buyer and another member 403, no token 401, unknown listing and unknown reservation 404, confirming twice 400 Reservation.NotActive, a reservation made 49 h ago 400 Reservation.Expired, PUT on a sold listing 400 Listing.Sold, reserving a sold listing 400 Listing.NotActive. Every test checks the database. 376 tests pass (10 architecture, 250 unit, 4 ML, 112 integration).
- **Problems seen:** none.
- **Change to SKILL.md:** none.

### Run 13, skill v12 (Feature: Reservation expiry)

- **Why a new run:** A4 added the ReservationExpiryService background job, which is turned off in the normal test factory.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md, section "Feature: Reservation expiry (Sara)". Integration tests already exist: keep them and add only what this section asks. Follow the skill exactly. Then run all tests and tell me the results and every file you changed. Do not commit or push.` (new session)
- **Skill version:** v12, commit 6c1a3bf; run by Sara Matasel
- **Result:** 3 new tests in Listings/ReservationExpiryTests: with the job on (interval 200 ms) only the expired reservation becomes Expired and its listing Active with Version + 1; after that another member can reserve the listing (201); with the job off, an expired reservation stays Active in the database but GET /members/me/reservations shows it as Expired.
- **Problems seen:** the run stopped with 2 failing tests and reported a real bug: EfListingRepository.GetAllAsync used AsNoTracking, the handler changed those untracked listings, and on save EF Core used the new Version as the original one, so the UPDATE affected 0 rows (DbUpdateConcurrencyException) and the job never expired anything. The skill forbids changing src/, so the fix was made by hand: GetAllAsync now tracks the listings, with a comment explaining why. After the fix all tests pass: 10 architecture, 263 unit, 4 ML, 115 integration.
- **Change to SKILL.md:** none.

### Run 14, skill v13 (Feature: Error handling)

- **Why a new version:** A5 added a global exception handler and ProblemDetails. The 401, the 403 of the `Admin` policy, the 404 of an unknown route, the 400 of unreadable JSON and the 429 of the login limit now have a ProblemDetails body instead of an empty one, and a `DbUpdateConcurrencyException` is now 409 instead of 500. 12 existing tests that checked an empty body failed, and the new code had no tests.

#### Change to SKILL.md (v12 → v13) and why

| # | v12 (before) | v13 (after) | Why |
|---|---|---|---|
| 1 | *"401 always has an empty body"*; for 401 and the 403 of `CreateMember` *"check only the status code"* | New section *"Feature: Error handling (Anca)"* with a table of which errors keep `{ code, message, type }` (the handlers) and which are ProblemDetails (ASP.NET and exceptions), and *"this section replaces them"* | The old rules are in other people's sections, already logged; they are overridden here instead of being edited. |
| 2 | — | Helpers `ProblemResponse`, `ConcurrentEditUnitOfWork` (raises `Version` with SQL right before the save) and `ThrowingUnitOfWork`, used through `Factory.WithWebHostBuilder` in one test only | Two requests rarely collide in a test; this makes EF Core throw a real `DbUpdateConcurrencyException` every time. |
| 3 | — | The list of existing tests to update and the 6 new tests in `ErrorHandling/ErrorHandlingTests.cs` (404, 405, 400, 409, 500 without the exception message, 403 from a handler still in the old format) | Without it Claude would only fix the 12 failing tests and leave the new branches untested (Sonar checks `Infrastructure/` too). |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them, update the ones the "Feature: Error handling (Anca)" section asks to update, and add the tests from that section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v13, commit 6d8b7bb (committed as "v12"; renumbered because the reservation expiry PR made v12) (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** 6 new tests in `ErrorHandling/ErrorHandlingTests.cs`, 3 new helpers in `Infrastructure/`, 15 existing tests updated in 13 files (12 no-token tests, the student 403 of `CreateMember`, the 429 of `LoginTests`, the unknown condition of `SuggestPriceTests`). No test deleted or renamed, nothing changed in `src/`. Build 0 errors, 0 warnings; 382 tests pass (10 architecture, 250 unit, 4 ML, 118 integration), the integration tests three times in a row. Coverage of `Infrastructure/ErrorHandling/` from the integration tests: 56/56 lines, 17/17 branches.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. `ProblemResponse` has no `Detail`, but the 500 test must check `detail`; the run read it from the raw JSON with `JsonDocument`.
    2. `GetMyReservationsTests` had no database check in its no-token test, and the 404/405 tests have nothing to check; the run added a "0 listings" check.
    3. `AuthenticateAs` only sets the header on the base `Client`; for the second client the run set `Authorization: Bearer TestTokens.Create(SellerId)` by hand, as the section says.
    4. The section does not say to dispose the factory from `WithWebHostBuilder`; the run used `await using`, as the Admin seed section does.
    5. Sections 1 and 10 of the skill are out of date (errors only Validation/NotFound/Conflict, "both test projects" while there are four).
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 6d8b7bb; generated code 33c179b

### Run 15, skill v14 (Feature: Health check)

- **Why a new version:** the Docker PR added `GET /health`, and the review asked for tests. The endpoint had none.

#### Change to SKILL.md (v13 → v14) and why

| # | v13 (before) | v14 (after) | Why |
|---|---|---|---|
| 1 | Nothing about `/health`; section 1 says every route is under `/api/v1`. | New section *"Feature: Health check (Sara)"*: `GET /health` is outside `/api/v1`, needs no login and answers 200 with the plain text `Healthy`. | Without it Claude would look for the endpoint under `/api/v1` and read the body as JSON. |
| 2 | — | Three tests: no token → 200 `Healthy`, a student token → 200 `Healthy`, `GET /api/v1/health` → 404 ProblemDetails `Route.NotFound`. *"Do not try to make the database unreachable; the 503 case is not tested."* | Proves the endpoint is public and not part of the versioned API, without breaking the shared in-memory database. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Health check (Sara)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v14, commit 56fdb10 (section written by Ancuța Ilenei for the Docker PR of Sara Matasel, with Claude Code; run in a new session)
- **Result:** new file `Health/HealthCheckTests.cs` with the 3 tests, each also checking that the database still has 0 listings and 0 members. No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 401 tests pass (10 architecture, 263 unit, 4 ML, 124 integration), the integration tests three times in a row.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. The section does not name the student id; the run used a fixed `StudentId` (`22222222-...`).
    2. Sections 1 and 10 of the skill are out of date (all routes under `/api/v1`, "both test projects").
    3. Not from this run: `ReservationExpiryTests.Given_JobExpiredTheReservation_When_AnotherMemberReserves_Then_Returns201` failed once in about ten runs with 500. The expiry job keeps running every 200 ms during the Act and shares the one in-memory SQLite connection with the request. Reported to Sara, not changed here.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 56fdb10; generated code c1eebb4

### Run 16, skill v15 (Feature: Favorites)

- **Why a new version:** A3 added three endpoints under `/api/v1/members/me/favorites` and a `Favorites` table with foreign keys to `Members` and `Listings`. They had no integration tests.

#### Change to SKILL.md (v14 → v15) and why

| # | v14 (before) | v15 (after) | Why |
|---|---|---|---|
| 1 | Nothing about favorites. | New section *"Feature: Favorites (Anca)"*: a table with the three requests, their success and failure answers, the files and the list of tests. | Without it Claude would have to guess the routes and the error codes. |
| 2 | *"Members do not need to exist in the database to log in: the token is enough."* (Ownership) | *"the member who saves a listing **must be seeded** (unlike the other features, a token alone is not enough)"*, plus a test for a member that is not in the database → 404 `Member.NotFound`. | The foreign key to `Members` would make an unseeded member fail; the handler answers 404 before the save. |
| 3 | — | The cascade test: the seller deletes the listing → the member's favorite is gone from the database. | Proves the `OnDelete(Cascade)` of the configuration, which no unit test can see. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Favorites (Anca)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v15, commit fa4e09a (committed as "v14"; renumbered because the Docker PR made v14) (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** three new files in `Favorites/`: `AddFavoriteTests` (7), `RemoveFavoriteTests` (5, with the cascade test), `GetMyFavoritesTests` (7 cases). No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 448 tests pass (10 architecture, 294 unit, 4 ML, 140 integration), the integration tests three times in a row. The whole Postman collection was also run with `newman` on the running API: 89 requests, 178 assertions, 0 failures.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. Sections 1 and 3 say the schema comes from `EnsureCreatedAsync()`, but the app already runs `Migrate()` at startup; it works either way.
    2. Section 10 is out of date ("at least 20 tests", "both test projects", `git status` only in the test project).
    3. The 409 test and the "member not in the database" test do not say which `createdAt` and which id to use; the run used `CreatedAt.AddHours(1)` and `OtherStudentId`.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill fa4e09a; generated code 25c29b8

### Run 17, skill v16 (Feature: Refresh token and logout)

- **Why a new version:** extra task 6 (refresh token + logout). Login now also returns `refreshToken`, and `POST /auth/refresh` and `POST /auth/logout` are new public endpoints. The new table `RefreshTokens` (migration `AddRefreshTokens`) has to be checked through the DbContext: only the hash is stored, and refresh and logout set `RevokedAt`.

#### Change to SKILL.md (v15 → v16) and why

| # | v15 (before) | v16 (after) | Why |
|---|---|---|---|
| 1 | Nothing about refresh tokens. | New section *"Feature: Refresh token and logout (Carina)"*: a table with the success and failure of each slice, the stored hash, the arrange rules and the test list for `LoginTests` (one existing test extended), `RefreshTests` and `LogoutTests`. | The endpoints are new, so Claude needs the contract to test it. |
| 2 | *"Never assert on `CreatedAt` with `DateTime.Now` / `DateTimeOffset.UtcNow`"*. | *"Read `var before = DateTimeOffset.UtcNow;` in the Arrange and, after the call, check that `expiresAt` is between `before.AddMinutes(15)` and `DateTimeOffset.UtcNow.AddMinutes(15)`. This window is the only allowed use of the real clock in an assertion."* | The API uses the real clock, and a window around the call proves the 15 minutes without being flaky. |
| 3 | — | *"Never call `/auth/login` to get a refresh token in the refresh or logout tests"*; tokens are seeded with `RefreshToken.Create(...)`. | Arrange data is seeded, not made by another endpoint, and login has a rate limit. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them, extend Given_ValidCredentials in LoginTests and add RefreshTests and LogoutTests as the "Feature: Refresh token and logout (Carina)" section says. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v16, commit 0afb3d5 (committed as "v14"; renumbered because the health check and favorites PRs made v14 and v15, and in the merge with main the section was added again after "Feature: Favorites (Anca)"); section written and run by Carina Coman
- **Result:** in `Auth/LoginTests.cs` only `Given_ValidCredentials_When_LoggingIn_Then_Returns200WithToken` changed: it also checks a non-empty `refreshToken`, `expiresAt` inside the 15-minute window, and exactly one `RefreshToken` in the database for the member, with `TokenHash` = `RefreshToken.Hash(body.RefreshToken)` (so the plain token is not stored) and `RevokedAt` null. New `Auth/RefreshTests.cs` with 6 tests (active token → 200, the old token revoked and the member has 2 tokens; revoked, expired, unknown and empty token, and a token whose member does not exist → 401 `Auth.InvalidRefreshToken`) and `Auth/LogoutTests.cs` with 4 tests (active token → 204 with `RevokedAt` set; revoked, unknown and empty token → 401). Members and tokens are seeded directly; no test calls `/auth/login` or uses `AuthenticateAs`. Nothing changed in `src/`. `dotnet build` 0 warnings, 0 errors; 131/131 integration tests three runs in a row; full run: 10 architecture, 291 unit, 4 ML, 131 integration.
- **Problems seen:** none in the generated tests. The first full `dotnet test` had one failure in an existing test not touched by this run, in `Listings/ReservationExpiryTests` (job on, only the expired reservation becomes `Expired`). It waits for the background job, so it can fail when the machine is busy; it passed on its own, in the three integration runs and in the final full run. A single flaky failure was also seen on `main` before this branch. Reported to the team, not fixed here.
- **Change to SKILL.md:** none after the run; v16 is the final version.
- **Commits:** skill 0afb3d5; generated code 6db4020

### Run 18, skill v17 (Feature: Category suggestion)

- **Why a new version:** B4 added `POST /api/v1/listings/category-suggestion`, served by the real `category-classification.zip` model. It had no integration tests.

#### Change to SKILL.md (v16 → v17) and why

| # | v16 (before) | v17 (after) | Why |
|---|---|---|---|
| 1 | Nothing about category suggestion. | New section *"Feature: Category suggestion (Anca)"*: the request, the answers, the file and the list of tests, with the real model (no fake in the factory). | Without it Claude would have to guess the route, the body and the error codes. |
| 2 | — | *"Never assert an exact confidence"*; only the category of two clear examples and a confidence between 0 and 1. | The model can be retrained; an exact value would break the tests for no reason. |
| 3 | — | The 401 is checked as ProblemDetails, the 400s as `Error`, and the database still has 0 listings. | The same rules as the other sections since the error handling task (A5). |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Category suggestion (Anca)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v17, commit 57316ee (committed as "v16"; renumbered because the refresh token PR made v16) (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** one new file `Listings/SuggestCategoryTests.cs` (5 tests). No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 472 tests pass (10 architecture, 306 unit, 8 ML, 148 integration), the integration tests three times in a row. The whole Postman collection was also run with `newman` on the running API: 94 requests, 188 assertions, 0 failures.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. The section says `category` is a string; in JSON it is, but `SuggestCategoryResponse` has the `Category` enum, so the tests compare with `Category.Electronics` and `Category.Notes`.
    2. Section 10 is still out of date (it speaks of two test projects; there are four now).
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 57316ee; generated code f98a859

### Run 19, skill v18 (Feature: Spam and moderation)

- **Why a new version:** B5 added three endpoints (`POST /api/v1/listings/{id}/reports`, `GET /api/v1/moderation/listings`, `POST /api/v1/moderation/listings/{id}/hide`), a `Reports` table with foreign keys to `Listings` and `Members`, the real spam model in `PublishListing` and the hidden listings in favorites. They had no integration tests.

#### Change to SKILL.md (v17 → v18) and why

| # | v17 (before) | v18 (after) | Why |
|---|---|---|---|
| 1 | Nothing about moderation. | New section *"Feature: Spam and moderation (Anca)"*: a table with the three requests, who may call them, their answers, the files and the list of tests. | Without it Claude would have to guess the routes, the Admin rule and the error codes. |
| 2 | — | *"An Admin is logged in with `AuthenticateAs(id, "Admin")`"*, a 403 test as a student for both Admin endpoints, and the reporter **must be seeded**. | The Admin policy and the foreign key to `Members` are only visible over HTTP and in the database. |
| 3 | — | `SpamOnPublishTests` with the real model: a spam draft gets an automatic report with a probability between 0.5 and 1, a normal one gets none. *"Never assert an exact spam probability."* | Proves the model is wired into publishing without tying the tests to one trained model. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Spam and moderation (Anca)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v18, commit b9967f5 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** four new files in `Moderation/`: `ReportListingTests` (8), `GetReportedListingsTests` (6 cases), `HideListingTests` (8), `SpamOnPublishTests` (2), plus one test in `Favorites/AddFavoriteTests` and one in `Favorites/GetMyFavoritesTests`. No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 596 tests pass (10 architecture, 389 unit, 13 ML, 184 integration), the integration tests three times in a row. The whole Postman collection was also run with `newman` on the running API: 116 requests, 229 assertions, 0 failures.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. "After hiding, `GET /api/v1/listings` does not return it" cannot be one test (one HTTP call per Act); the run seeded a hidden listing and called the search, which is a weak check because the search only returns `Active` listings anyway.
    2. `Report.ReasonTooLong` is in the table but not in the list of tests, so it has no integration test (it is covered by the unit tests).
    3. A report or a favorite cannot be created for a hidden listing, so the run created them on a published listing and called `Hide()` before seeding; the section does not say this.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill b9967f5; generated code ffc2895

### Run 20, skill v19 (Feature: Seller reviews)

- **Why a new version:** A2 added `POST /api/v1/listings/{id}/reviews` and `GET /api/v1/members/{id}/reviews`, and a `Reviews` table with foreign keys to `Listings` and twice to `Members`. They had no integration tests.

#### Change to SKILL.md (v18 → v19) and why

| # | v18 (before) | v19 (after) | Why |
|---|---|---|---|
| 1 | Nothing about reviews. | New section *"Feature: Seller reviews (Anca)"*: a table with the two requests, who may call them, their answers, the files and the list of tests. | Without it Claude would have to guess the routes, who may review and the error codes. |
| 2 | — | The seller **and** the buyer must be seeded, and how to build a sold listing (`Publish`, `Reserve`, `ConfirmSale`). | Two foreign keys to `Members`; a token alone is not enough, as for favorites. |
| 3 | — | `GET .../reviews` without a token, the average over all reviews, and the cascade test (the seller deletes the sold listing → the review is gone). | Proves the endpoint is public and the `OnDelete(Cascade)` of the configuration, which no unit test can see. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Seller reviews (Anca)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v19, commit 4764342 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** two new files in `Reviews/`: `AddReviewTests` (11, with the cascade test) and `GetMemberReviewsTests` (7). No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 657 tests pass (10 architecture, 432 unit, 13 ML, 202 integration), the integration tests three times in a row. The whole Postman collection was also run with `newman` on the running API: 129 requests, 252 assertions, 0 failures.
- **Problems seen (reported by the run, not fixed in the skill):**
    1. `Review.CommentTooLong` is in the table but not in the list of tests, so it has no integration test (it is covered by the unit tests).
    2. The section asks for a seeded seller, so the fixed `SellerId` of the other sections is not used here; the run used `seller.Id`.
- **Change to SKILL.md:** none after the run.
- **Commits:** skill 4764342; generated code 1d76998

### Run 21, skill v20 (Feature: Whole project coverage)

- **Why a new version:** SonarCloud measures the coverage of the whole API, not only `Domain/` and the handlers (the team's final step). After all the tests, 8 files still had uncovered lines: the `AddAsync` duplicate check of the five EF repositories, the 409 of `CreateListingEndpoint`, the rate limit partition and the failed admin seed in `CampusMarketSetup.cs`, and the error paths of `ReservationExpiryService`. None of them can be reached with a normal request.

#### Change to SKILL.md (v19 → v20) and why

| # | v19 (before) | v20 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the lines Sonar still showed. | New section *"Feature: Whole project coverage (Anca)"*: for each uncovered line, the production code, the fake or setting needed, and the tests to add. | Without it Claude would not know which lines are missing or how to reach code that no request can reach. |
| 2 | — | Fakes through `WithWebHostBuilder` + `ConfigureTestServices`: a listing repository whose `AddAsync` returns false, a hasher with an empty hash, a fake expiry handler, a startup filter that sets the request IP, a logger provider that keeps the logs. | The same way `ThrowingUnitOfWork` is used; the factory and the base class stay unchanged. |
| 3 | Section 3 said the schema is created by `EnsureCreatedAsync()`; section 10 said "both test projects". | The schema is created by `Migrate()` at startup and `EnsureCreatedAsync()` does nothing; "all four test projects". | Both were out of date. |

- **Code changed by hand before the run:** two branches that can never run. `CampusMarketSetup.cs`: `Get<ReservationExpiryOptions>() ?? new ...` replaced by `GetValue("ReservationExpiry:Enabled", default)`, same behaviour. `ReservationExpiryService.cs`: `if (!await timer.WaitForNextTickAsync(...)) break;` became `await timer.WaitForNextTickAsync(...)`, because it returns false only after the timer is disposed, which happens when the method ends.
- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Whole project coverage (Anca)" section. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results.` (new session; it was also asked to run the integration tests three times and list the lines still uncovered when the coverage of all test projects is merged)
- **Skill version:** v20, commit a62a6b4 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** 17 new tests. New: `Infrastructure/RepositoryTests` (10), `Infrastructure/ReservationExpiryServiceTests` (3) and five helpers in `Infrastructure/` (`DuplicateIdListingRepository`, `EmptyHashPasswordHasher`, `TestRemoteIpStartupFilter`, `CapturingLoggerProvider`, `FakeExpireReservationsHandler`). Added: one test in `CreateListingTests` (409 `Listing.Duplicate`), two in `LoginTests` (another IP has its own limit, the same IP gets 429), one in `SeedAdminTests` (empty hash → no admin). No existing test changed, nothing changed in `src/`. Build 0 errors, 0 warnings; 674 tests pass (10 architecture, 432 unit, 13 ML, 219 integration). No uncovered line or branch left in the merged coverage (`Program.cs`, `ml/` and migrations excluded, as in the CI).
- **Problems seen (reported by the run):**
    1. The section contradicted two general rules: section 7 forbids calling repositories from a test, section 6 says the Act is one HTTP call. The run followed the section.
    2. The section said `AddScoped` for every fake, but `IPasswordHasher` is a singleton; the run registered the fake as a singleton.
    3. Section 10 says `git status` lists only `tests/`, but the two hand changes in `src/` are listed too.
    4. One full `dotnet test` out of about 25 runs (the run's, then 11 integration-only and 10 full runs checked afterwards) had one failed integration test; it could not be repeated and its name was not kept. Probably the known flaky `ReservationExpiryTests` test.
- **Change to SKILL.md after the run:** the rules of the section now say that it is the only exception to sections 6 and 7, that a fake keeps the lifetime of the real service, and that the section goes with the two changes in `src/`.
- **Commits:** skill a62a6b4; generated code 2872870

### Run 22, skill v21 (CA1859 rule)

- **Why a new version:** after PR #71, SonarCloud reported one issue in the generated tests: CA1859 on `ReservationExpiryServiceTests.ServiceErrors`, which was declared as `IReadOnlyList<...>` but always returns `.ToList()`.

#### Change to SKILL.md (v20 → v21) and why

| # | v20 (before) | v21 (after) | Why |
|---|---|---|---|
| 1 | Nothing about analyzer warnings. | Section 7: a private helper returns its concrete type (`List<T>`, not `IReadOnlyList<T>`); after generating, build with a SARIF log and check that the test files have no CA or IDE diagnostic. | Sonar imports these Roslyn suggestions as issues, but `dotnet build` does not show them, so the run could not see them. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them all. SonarCloud reports CA1859 on ReservationExpiryServiceTests.cs line 47. Apply the new rule in section 7 to the existing integration tests: fix that helper and any other helper that breaks the same rule. Do not change test names, assertions or behaviour.` (new session)
- **Skill version:** v21, commit 99f5fa3 (rule written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** one line changed: `ServiceErrors` returns `List<CapturingLoggerProvider.Entry>`. No other helper broke the rule (the other interface return types are public members of interfaces). The SARIF log of the test project has no diagnostics. 674 tests pass, the integration tests three times in a row.
- **Problems seen:** the rule says "the new test files"; when it is applied to existing tests it should mean the whole test project, which is what the run checked.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 99f5fa3; generated code 80b724d

### Run 28, skill v27 (Feature: Minimum category confidence)

- **Why a new version:** `POST /api/v1/listings/category-suggestion` can now answer `"category": null`, with a confidence below 0.7, when the model is not sure.

#### Change to SKILL.md (v26 → v27) and why

| # | v26 (before) | v27 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the minimum confidence. | New section *"Feature: Minimum category confidence (Diana)"*: the file (`Listings/SuggestCategoryTests.cs`, add to it) and 2 tests: title "Banana" with description "Am de vandut o banana buna" → 200, `category` null, `confidence` below 0.7; title "Notite Analiza matematica, an I" → 200, `Notes`. | Runs the real model through the real endpoint, so the 0.7 limit is proven on both sides with real inputs; a unit test with a fake cannot show that. |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Minimum category confidence (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v27, commit dc876be (section written and run by Diana Preutu with Claude Code)
- **Result:** 2 new tests in `Listings/SuggestCategoryTests.cs`: "Banana" → 200, `category` null, confidence below `SuggestCategoryHandler.MinConfidence`; "Notite Analiza matematica, an I" → 200, `Notes`, confidence at or above the limit. Both check that no listing was saved. No existing test changed. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 435 unit, 13 ML, 221 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v27 is the final version.
- **Commits:** skill dc876be; generated code 020f950

### Run 29, skill v28 (Feature: Maximum price)

- **Why a new version:** Week 6, task 2. `POST /api/v1/listings` with a price above 100000 now answers 400 `Money.TooHigh`; no integration test covered it.

#### Change to SKILL.md (v27 → v28) and why

| # | v27 (before) | v28 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the maximum price. | New section *"Feature: Maximum price (Diana)"*: the file (`Listings/CreateListingTests.cs`, add to it) and one test: POST with price 200000 → 400 `Money.TooHigh`, nothing saved. | The error reaches the client as 400 with the `Error` body only if the endpoint maps it; a unit test cannot see that. |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Maximum price (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v28, commit 0d26845 (section written and run by Diana Preutu with Claude Code)
- **Result:** One new test in `Listings/CreateListingTests.cs`: price 200000 → 400, `Money.TooHigh`, type `Validation`, the exact message, and the database still has no listing. No existing test or file in `src/` changed. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 435 unit, 13 ML, 220 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v28 is the final version.
- **Commits:** skill 0d26845; generated code c989e44


### Run 30, skill v29 (Feature: CORS for the frontend)

- **Why a new version:** Weeks 6–7, the frontend skeleton. The web frontend (`frontend/`, Vite on `http://localhost:5173`) calls the API from another origin, so the API now answers CORS for the origins in `Cors:AllowedOrigins`. No integration test covered it. (Sara's sections "Production configuration and demo data" and "Price feedback loop" were added to the skill before this run without a PROMPTS entry; v29 counts after them.)

#### Change to SKILL.md (v28 → v29) and why

| # | v28 (before) | v29 (after) | Why |
|---|---|---|---|
| 1 | Nothing about CORS. | New section *"Feature: CORS for the frontend (Anca)"*: the production code to read, the file (`Cors/CorsTests.cs`, new) and the tests: allowed preflight (204, origin, methods, headers, no credentials), preflight from another origin (no `Access-Control-Allow-Origin`), 201 with `Location` exposed, 401 and 500 ProblemDetails that still carry the CORS header, a request without `Origin` (no CORS headers), an origin set through `Cors:AllowedOrigins:0`. | CORS is middleware: only a real HTTP request through the whole pipeline shows the headers, and only an error answer shows that `UseCors` runs before the exception handler. |
| 2 | (after the run) the configured origin was one bullet with two preflights; the not-allowed preflight had no status; nothing said that `git status` lists the hand-made changes. | Two tests with one HTTP call each, from a private helper; the not-allowed preflight is still 204; a rule lists the files changed by hand before the run. | The run found these three gaps (section 6 allows one HTTP call per test). |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md (read the whole file first). Integration tests already exist: keep them all. Generate the tests for the section "Feature: CORS for the frontend (Anca)". Add only those tests. Then run dotnet build and dotnet test tests/CampusMarket.IntegrationTests three times in a row, and the whole solution once. Follow section 10, including the SARIF check from section 7. You may only change files under tests/CampusMarket.IntegrationTests/.` (new session)
- **Skill version:** v29, commit a6a5c1c (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** 8 tests in `Cors/CorsTests.cs`: preflight from the frontend (204, exact `Access-Control-Allow-Origin`, `POST` and `authorization` allowed, no `Access-Control-Allow-Credentials`), preflight from `http://evil.example` (204, no `Access-Control-Allow-Origin`), `POST /listings` with the origin (201, origin, `Location` exposed and set, listing saved), no token (401 `Auth.Unauthorized` with the origin), `ThrowingUnitOfWork` (500 `Server.Error` with the origin, listing still `Draft`), no `Origin` header (200, no CORS header), and two tests with `Cors:AllowedOrigins:0` = `https://campusmarket.example` (that origin allowed, `localhost:5173` no longer). Every test checks the database. No existing test changed. Build 0 warnings, 0 errors; the SARIF log of the test project is empty; full run: 10 architecture, 454 unit, 13 ML, 235 integration; the integration tests passed three times in a row.
- **Problems seen:**
    1. The section asked for two preflights in one test; split into two tests (fixed in the section after the run).
    2. One integration test failed once in the first full run with coverage and was not seen again in 8 more runs (5 integration, 3 full); its name was not printed. Most likely the old timing issue of the reservation expiry tests, not CORS.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill a6a5c1c; generated code 820290e

## code-review

Skill written and run by Sara Matasel. The skill is read-only: after every run `git status` was clean.
### Run 1, skill v1 (our repository)

- **Prompt:** `Use the code-review skill in .claude/skills/code-review/SKILL.md to review this repository. Follow the skill exactly. Do not edit any file. Give me only the output the skill asks for.`
- **Skill version:** v1, commit c0d576f (a short first draft)
- **Result:** `Found 2 problems: 2 Minor.` Both rows were real, no file changed.

#### Problems seen (bad output from v1)

1. **Not a Markdown table.** The terminal printed every row as a block, so it could not be copied into `REVIEW.md`:

   ```
   #: 2
   File:Line: src/CampusMarket.Api/Features/Listings/GetListingById/GetListingByIdResponse.cs:1
   Severity: Minor
   Category: naming
   ```

2. **Wrong line.** The `using` it reports is on line 2; line 1 is the `namespace`.
3. **Forced category.** A `using` placed after the `namespace` is a formatting problem, but it was reported as "naming", because the skill did not say what to do with a problem outside the six categories.
4. Claude Code then suggested "apply the two fixes". We did not run it: the review skill never changes code.

#### Change to SKILL.md (v1 → v2) and why

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | *"One Markdown table with the columns …"* | *"Print everything inside one fenced code block that starts with ```` ```markdown ````, so the text can be copied into `REVIEW.md` exactly as it is. Nothing before or after the block."* | Fixes problem 1. |
| 2 | Nothing about lines. | New section **File and line**: *"`File:Line` is the path from the repository root and the exact line where the problem is. Before you write a row, open the file again and check that this line really contains the code you describe. If it does not, fix the line number or drop the row."* | Fixes problem 2, and makes it harder to invent a row. |
| 3 | Only the list of six categories. | *"If a problem does not fit any of these six categories (for example the order of `using` lines or formatting), do not report it. Never put a problem in a category that does not describe it."* | Fixes problem 3. |

- **Commits:** skill c0d576f; output: not kept

### Run 2, skill v2 (our repository)

- **Prompt:** same as run 1, in a new Claude Code session
- **Skill version:** v2, commit 858de47
- **Result:** `Found 1 problem: 1 Minor.` The run 1 problems were gone: one Markdown block, exact line (`Listing.cs:9`, the private field `reservations` without `_`), no forced category.

#### Problems seen (bad output from v2)

1. **It missed a real problem from run 1.** `ListingTests.cs:289` (a test name that does not match its Arrange) was not reported, although the code had not changed. The skill only said "read the code", so each run looked at different things.

#### Change to SKILL.md (v2 → v3) and why

| # | v2 (before) | v3 (after) | Why |
|---|---|---|---|
| 1 | *"Read the code of the repository and report the problems you find."* | New section **How to review**: *"Open every file in the folders above, one by one, and go through this checklist. Do not stop after the first problems you find: the same repository must give the same table every time."* + one list of questions per category, for example for tests: *"does the name of the test match what the Arrange really sets up and what is asserted (for `Given_When_Then` names, check each part)?"* | A checklist makes the review the same from run to run. |
| 2 | Only `src/` and `tests/`. | *"Skip generated files: `bin/`, `obj/`, `coverage-report/`, `TestResults/`, `Migrations/`."* | SonarQube had already reported 161 false issues inside `coverage-report/`. |

- **Commits:** skill 858de47; output: not kept

### Run 3, skill v3 (our repository)

- **Prompt:** same as run 1, in a new Claude Code session
- **Skill version:** v3, commit 506d7c0
- **Result:** `Found 2 problems: 1 Major, 1 Minor.` Both checked by hand:
    - `Reservation.cs:32` (Major, domain invariants): `Cancel()` is public while `Create` is internal, so a caller can cancel a reservation directly and leave the listing in `Reserved`.
    - `ListingTests.cs:289` (Minor, test quality): found again, now in the right category.
    - Exact lines, no invented row, no file changed. The table is in `REVIEW.md`. The problems are reported, not fixed: code changes only go through our skills.
- **Change to SKILL.md:** none; v3 is the final version.
- **Commits:** skill 506d7c0; review output in REVIEW.md

### Run 4, skill v3 (another team's repository)

- **Prompt:** `Use the code-review skill in .claude/skills/campusmarket-code-review/SKILL.md to review this repository. Follow the skill exactly. Do not edit any file. Give me only the output the skill asks for.`
- **Skill version:** v3, commit 506d7c0, unchanged; copied into a local clone of https://github.com/ioana-onut/CampusMarket under a different folder name, so it did not overwrite their own code-review skill
- **Result:** `Found 1 problem: 1 Minor.` `Member.cs:9` (naming): the private fields `listingIds` and `reservationIds` have no `_`. Checked by hand: of the 7 `private readonly` fields in their project these are the only 2 without `_`, and every line cited as evidence is exact. One small mistake: in the "Proposed fix" column, line 81 is listed but it only has a `{`; the `File:Line` column is right. Checking the checklist by hand found nothing else either (no query that saves, no endpoint using a repository, no `DateTime.Now` in tests). Nothing changed, committed or pushed in their repository. The table is in `REVIEW.md`.
- **Change to SKILL.md:** none; the skill worked on a different project without changes.
- **Commits:** skill 506d7c0; review output in REVIEW.md

### Skill update v3 → v4 (groundwork PR)

| What | Before (v3) | After (v4) |
|---|---|---|
| Unit of work method, line 16 (checklist) | "Does a command handler forget `SaveChangesAsync`?" | "Does a command handler forget `CommitAsync`?" |
| Unit of work method, line 62 (example row) | "The query handler calls `SaveChangesAsync`." | "The query handler calls `CommitAsync`." |

- **Why:** the groundwork PR renames `IUnitOfWork.SaveChangesAsync` to `CommitAsync(CancellationToken ct)`; a review that looks for the old name would miss the real call.
- **Who:** Carina Coman (edit only, no generation)
- **Commit:** `129593d`

### Run 5, skill v5 (our repository)

Skill v5 written by Sara Matasel, run by Ancuța Ilenei.

#### Change to SKILL.md (v4 → v5) and why

| # | v4 (before) | v5 (after) | Why |
|---|---|---|---|
| 1 | No YAML header: the file started with `# Code review`. | `name: code-review` and `description: ...` between `---` at the top. | Without the header Claude Code did not find the skill by its name. |
| 2 | *"What to look at"* started with `src/`. | *"First read the project's own rules: `README.md` and every `.claude/skills/*/SKILL.md`. ... Something these rules explicitly allow is not a problem."* | v4 did not compare the code with the README. |
| 3 | Five checklist items. | New item **requirements**: *"for every business rule written in the README (... words like unique, must, only, at most), find the code that enforces it. If no code enforces it, it is a problem: report it as domain invariants ... and quote the README rule in `Problem`."* | The README rule "unique email" was not enforced in the code, and v3/v4 did not report it. |
| 4 | Only *"the exact line where the problem is"*. | *"For a missing check, use the line with the name of the method where the check should be ..., never a `{` line."* | A missing check has no line of its own. |

#### Run

- **Prompt:** `Use the code-review skill in .claude/skills/code-review/SKILL.md to review this repository. Follow the skill exactly. Do not edit any file. Give me only the output the skill asks for.` (new Claude Code session)
- **Skill version:** v5, commit 742806e (committed as "v4"; renumbered because the groundwork PR made v4)
- **Result:** `Found 14 problems: 0 Critical, 10 Major, 4 Minor.` Every row checked by hand: the file and line exist and the problem is real. No file changed. The table and the notes are in `REVIEW.md`, section "Week 4 – code-review v5".
    - The new **requirements** check works: 6 rows quote a README rule (409 on concurrent update, owner-only publish, max 3 reservations, buyer-only cancel, confirm sale, expiry back to `Active`). Some of them are planned for later (reservations this week, expiry in week 7).
    - The unique email is now enforced (fix/unique-email), so it was not reported as missing; instead v5 found the race between the check and the save (rows 2–4).
    - One row shows that the README is out of date: "cancel: only the buyer" (row 7), while the team decided "the buyer, the seller or an Admin".

#### Problems seen

1. **It missed the two v3 rows.** `Reservation.cs:32` (`Cancel()` is public) and `ListingTests.cs:289` (test name does not match its Arrange) are still in the code, but v5 did not report them. The run spent its attention on the README rules, so the same repository no longer gives the same table for the old checklist items. Not changed in the skill this week; the two problems are fixed in the ownership PR.

- **Change to SKILL.md:** none after this run.
- **Problems found:** reported only, not fixed in this PR.
- **Commits:** skill 742806e; review output in REVIEW.md

### v5 → v6: the same repository must give the same table

- **Why a new version:** v5 did not report the two v3 rows (`Reservation.cs:32`, `ListingTests.cs:289`), although they were still in the code (see run 5, "Problems seen"). Issue #47.

#### Change to SKILL.md (v5 → v6) and why

| # | v5 (before) | v6 (after) | Why |
|---|---|---|---|
| 1 | Nothing about earlier reviews. | *"Then read `REVIEW.md`, if it exists. For every row of an earlier review, open the file and check whether the problem is still in the code … If it is, report it again in this table."* | A problem that is still in the code must stay in the table, whatever the new checks find. |
| 2 | One checklist, done in any order. | *"Do the review in two passes … 1. the checklist items below except requirements, for every file; 2. the requirements check against the README. The second pass only adds rows."* | In v5 the requirements check took the attention of the run and the old checklist items were skipped. |
| 3 | **domain invariants:** public setters, public constructors, invalid states, collections changed from outside. | Also: *"Does a child entity (for example a `Reservation` inside a `Listing`) have a public method that changes its state, so a caller can skip the parent and break the parent's rules?"* | `Reservation.Cancel()` was found by v3 but not by v5, because no checklist item asked for it. |

- **Written by:** Diana Preutu
- **Check run:** v6 run on `c4191e6`, the commit v5 reviewed (a separate `git worktree` with the v6 SKILL.md copied in, new Claude Code session), with the same prompt as run 5: `Use the code-review skill in .claude/skills/code-review/SKILL.md to review this repository. Follow the skill exactly. Do not edit any file. Give me only the output the skill asks for.` Run by Diana Preutu.
- **Result:** `Found 9 problems: 5 Major, 4 Minor.` Both v3 rows are back: `Reservation.cs:32` (row 1, `Cancel()` is public) and `ListingTests.cs:289` (row 7, the test name does not match its Arrange). The other rows repeat v5: no 409 on concurrent update, max 3 reservations, buyer-only cancel, owner-only publish, `ListingTests.cs:240`, `LoginTests.cs:68`, plus `AspNetPasswordHasher.cs:10` without `_`. No file changed.
- **Known limit:** v6 gave 9 rows where v5 gave 14 on the same code; it did not repeat v5 rows 2-4, 8, 9, 10 and 12. At `c4191e6`, `REVIEW.md` held only the v3 rows, so v6 could re-check only those. On the current `main`, `REVIEW.md` holds every earlier row, so the next run re-checks the v5 rows too.
- **Commits:** skill cb61e43

---

## architecture-testing

Skill written by Diana Preutu, run by Sara Matasel.

### Review before the first run: v1 → v2

- **What happened:** Sara reviewed the skill in PR #36 before it was run.
- **Skill version:** v1, commit 24483a2

#### Change to SKILL.md (v1 → v2) and why

| # | v1 (before) | v2 (after) | Why |
|---|---|---|---|
| 1 | The `.csproj` had only the packages and the `ProjectReference`. | *"`<FrameworkReference Include="Microsoft.AspNetCore.App" />` (the API is a web project, so the test project does not build without it)"* and *"`<Using Include="FluentAssertions" />`"*, copying the structure of `CampusMarket.UnitTests.csproj`. | The API is a web project; without the framework reference the test project may not build. |
| 2 | 7 rules. | Rule 8 in `FeatureTests`: *"Types in `CampusMarket.Api.Features` have no dependency on `CampusMarket.Api.Infrastructure`: handlers and endpoints use only the interfaces."* The final check says 8 tests. | It is our main architecture rule: slices only talk to interfaces, never to EF Core classes. |

- **Commits:** skill d25ca00

### Run 1, skill v2

- **Prompt:** `Use the architecture-testing skill in .claude/skills/architecture-testing/SKILL.md to create the architecture test project. Follow the skill exactly, including the "Never do" list. When you finish, run dotnet build and dotnet test and tell me the results, including which way the handlers were selected.`
- **Skill version:** v2, commit d25ca00
- **Result:** new project `tests/CampusMarket.ArchitectureTests` (`ArchitectureTestBase`, `DomainTests`, `EndpointTests`, `HandlerTests`, `FeatureTests`), added to `CampusMarket.sln`. Build: 0 errors, 0 warnings. **8/8 tests pass**, one per rule. Handlers are selected **by name** (`HaveNameEndingWith("Handler").And().AreClasses()`; query handlers also `HaveNameStartingWith("Get")`), the fallback the skill allows, because NetArchTest does not reliably match the open generic interfaces. Every test also checks that its selection is not empty. `git status`: only `CampusMarket.sln` and the new folder.
- **Problems seen:** none.
- **Change to SKILL.md:** none; v2 is the final version.
- **Commits:** skill d25ca00; generated code 4029cc3

## ml-dataset

Dataset generator for the ML part (week 5, task B1). Run by Sara Matasel.

### Run 1 (synthetic listings dataset)

- **Why:** the ML models (price, category, spam) need thousands of labelled listings; the app has only a few test listings and there is no public Romanian campus-marketplace dataset.
- **Prompt:** (new session)

```
Create a synthetic dataset for the ML part of CampusMarket (week 5, task B1).

1. New console project ml/CampusMarket.ML.DatasetGenerator (net10.0, no NuGet packages), added to CampusMarket.sln.
   It writes ml/data/listings.csv. Use new Random(42) so every run produces exactly the same file.
2. CSV: UTF-8, header Title,Description,Category,Condition,Price,IsSpam, standard CSV quoting (quotes around text, "" for a quote inside).
   3000 rows, about 10% spam.
3. Values must match our domain exactly:
   - Category: Textbook, Notes, Electronics, LabEquipment, Other (names from Domain/Listings/Category.cs)
   - Condition: New, Good, Worn (Domain/Listings/Condition.cs)
   - Title: 3 to 100 characters (Listing.MaxTitleLength); Description: empty in about 15% of rows, otherwise at most 1000 characters (Listing.MaxDescriptionLength)
   - Price: whole lei, always > 0
4. Realistic Romanian student listings (mix of text with and without diacritics, some typos and uppercase):
   - Textbook: manuals for real subjects and years (Analiză matematică, POO, Baze de date, Fizică, Anatomie...), editions, authors
   - Notes: course notes, exam summaries, solved problems, per subject and year
   - Electronics: laptops, calculators (Casio fx-991EX...), headphones, monitors, keyboards, phones
   - LabEquipment: lab coats, goggles, multimeters, breadboards, Arduino kits, dissection kits
   - Other: bikes, desk lamps, chairs, dorm items, books that are not textbooks
5. Price: a base range per category and item type (a laptop costs much more than a mouse), multiplied by condition
   (New 1.0, Good 0.7, Worn 0.45), plus random noise of +/-15%. The price must be learnable from category, condition and title words.
6. Hard examples (about 10%), so the category model cannot reach 100% from a single word:
   notes for an electronics course ("Notițe Electronică digitală" is Notes), a textbook about computers is Textbook,
   an Arduino kit sold for a course could be LabEquipment, etc.
7. Spam (IsSpam = true): off-platform contact ("scrie-mi pe WhatsApp 07..", "link în bio", Telegram), easy money
   ("câștigă 500 lei pe zi"), fake giveaways, all caps, repeated "!!!", prices of 1 leu or absurdly high.
   Most spam is in Other, but about a third is disguised in normal categories with a normal-looking title and the spam only in the description.
   Phone numbers must be obviously fake (07xx xxx xxx) and no real names, links or brands of real people.
8. ml/data/README.md: says clearly that the data is synthetic and generated, the columns, the rules above in short,
   and how to regenerate it (dotnet run --project ml/CampusMarket.ML.DatasetGenerator).
9. Run it, commit nothing. Show me: row count, rows per Category, per Condition, spam percentage, min/avg/max price per Category,
   10 random rows, and run it twice to prove the file is identical (compare hashes).
10. SonarQube: the generator is a dev tool, not app code. Add ml/** to the coverage exclusions where the other exclusions are configured,
    and add it with its reason to the "Coverage exclusions" list in README.md.
Do not change anything in src/ or tests/. Do not commit or push.
```

- **Follow-up prompt:** `Show the same price statistics (min / avg / max per Category, and per Category + Condition) only for rows with IsSpam = false. Then show 3 non-spam examples per Category with their price. Do not change any file.`
- **Result:** 3000 rows, 300 spam (10.00%); the same file on two runs (same SHA-256). Non-spam prices: Notes 5–53 lei, Textbook 17–188, LabEquipment 8–364, Other 10–1403, Electronics 28–5022; the averages follow New > Good > Worn (Textbook 90 / 64 / 39, Electronics 963 / 710 / 529). No change in src/ or tests/.
- **Problems seen:** none.

---

## ml-training

Training project and ML structure in the API (week 5, task B2). Run by Sara Matasel.

### Run 1

- **Why:** B1 produced the dataset; B2 adds the console project that will train the three models (price in B3, category in B4, spam in B5), plus the ML wiring on the API side, without any concrete model yet.
- **Prompt:** (new session)

```
Week 5, task B2: the ML training project and the ML structure in the API. The dataset from B1 is in ml/data/listings.csv
(columns Title, Description, Category, Condition, Price, IsSpam). Do not add any concrete model yet:
PriceTrainer (B3), CategoryTrainer (B4) and SpamTrainer (B5) come later, each as one new class.

1. New console project ml/CampusMarket.ML.Training (net10.0), added to CampusMarket.sln under the ml folder,
   with the latest stable Microsoft.ML that supports net10.0.
   - ListingRow: the CSV columns with [LoadColumn]; load with hasHeader, allowQuoting, separator ','.
   - IModelTrainer: Name, ModelFileName, and a method that gets the MLContext, the train and the test IDataView
     and returns the trained ITransformer plus the metrics as text.
   - Program: MLContext(seed: 42), load the CSV, TrainTestSplit(testFraction: 0.2, seed: 42), print the number of
     train and test rows, run every trainer in one list (empty for now: print "No trainers registered yet"),
     print its metrics and save its model to ml/models/<ModelFileName> with the input schema.
     Find ml/ from AppContext.BaseDirectory, like the dataset generator does.
2. ml/models/README.md: what the folder holds, that the .zip files are committed so the API works without training,
   and how to retrain (dotnet run --project ml/CampusMarket.ML.Training).
3. API, src/CampusMarket.Api/Infrastructure/ML/:
   - add Microsoft.ML and Microsoft.Extensions.ML (PredictionEnginePool, thread safe) to the API project;
   - MlOptions with ModelsPath (config section "Ml", default "ml-models");
   - an extension AddCampusMarketMl(this IServiceCollection, IConfiguration) called from CampusMarketSetup,
     with a comment that each model registers its PredictionEnginePool here in B3, B4 and B5;
   - the API .csproj copies ../../ml/models/*.zip to the output folder ml-models (no error if there are no .zip files yet).
4. Architecture tests: a new rule that Domain and Features do not depend on Microsoft.ML
   (only Infrastructure may), in the same style as the existing rules, with a non-empty selection check.
5. README: a short "Machine learning" section (the ml/ folders, the flow dataset -> training -> models -> API,
   how to retrain), and PROMPTS.md: a "Run 2" entry under ml-dataset... no: a new section "## ml-training", Run 1, with this prompt.
Run dotnet build, dotnet test and dotnet run --project ml/CampusMarket.ML.Training, show me the output and list every file you changed.
Do not change existing tests. Do not commit or push.
```

- **Result:** `dotnet build` 0 warnings, 0 errors. `dotnet test`: 192 unit, 10 architecture (the 2 new rules among them), 85 integration, all pass. `dotnet run --project ml/CampusMarket.ML.Training` prints `Train rows: 2397`, `Test rows: 603` and `No trainers registered yet.` Package picked: `Microsoft.ML` 5.0.1 and `Microsoft.Extensions.ML` 5.0.1 (restored against net10.0). No `.zip` is produced yet.
- **Files changed:**
    - `ml/CampusMarket.ML.Training/CampusMarket.ML.Training.csproj` (new)
    - `ml/CampusMarket.ML.Training/ListingRow.cs` (new)
    - `ml/CampusMarket.ML.Training/IModelTrainer.cs` (new)
    - `ml/CampusMarket.ML.Training/Program.cs` (new)
    - `ml/models/README.md` (new)
    - `src/CampusMarket.Api/Infrastructure/ML/MlOptions.cs` (new)
    - `src/CampusMarket.Api/Infrastructure/ML/MlServiceCollectionExtensions.cs` (new, holds `AddCampusMarketMl`)
    - `src/CampusMarket.Api/CampusMarket.Api.csproj` (added `Microsoft.ML`, `Microsoft.Extensions.ML`, and a `<None Include="..\..\ml\models\*.zip" Link="ml-models\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />` block)
    - `src/CampusMarket.Api/CampusMarketSetup.cs` (added `using` for `Infrastructure.ML`; `AddCampusMarket` now also takes `IConfiguration` and calls `AddCampusMarketMl(configuration)` — no test references `AddCampusMarket` directly)
    - `src/CampusMarket.Api/Program.cs` (one line: `AddCampusMarket(builder.Configuration)`)
    - `tests/CampusMarket.ArchitectureTests/MlTests.cs` (new, two `[Fact]` tests: Domain and Features do not depend on `Microsoft.ML`)
    - `CampusMarket.sln` (project added under the `ml` solution folder via `dotnet sln add`)
    - `README.md` (new "Machine learning" section after the coverage exclusions)
    - `PROMPTS.md` (this entry)
- **Problems seen:** none.

### Run 2

- **Why:** B2 added the training project and the ML wiring on the API side without any concrete model. B3 adds the first real model (price suggestion): `PriceTrainer`, the `price-regression.zip` artefact, the `SuggestPrice` slice behind an `IPriceSuggester` interface, and a quality test project that guards the model on every CI run.
- **Prompt:** (new session)

```
Week 5, task B3: price suggestion. The training project (ml/CampusMarket.ML.Training, IModelTrainer) and the ML wiring
in the API (Infrastructure/ML, AddCampusMarketMl, models copied to ml-models) already exist from B2.

1. Training: new class PriceTrainer : IModelTrainer, registered in the trainers list of Program.cs.
   - Train only on rows with IsSpam = false (spam prices are absurd on purpose).
   - Label: Price. Features: Category and Condition one-hot encoded, Title and Description with FeaturizeText.
   - Use a regression trainer that handles prices from 5 to 5000 lei well (FastTree, add Microsoft.ML.FastTree with the same version as Microsoft.ML).
   - Metrics on the test split: R², MAE and RMSE, printed by Program.
   - ModelFileName: price-regression.zip, saved to ml/models/ and committed.
2. Run the training and show me the metrics. If R² is below 0.8, try one simple improvement (for example training on log(Price)
   and converting back in the API) and show both results; keep the better one.
3. API, a new slice Features/Listings/SuggestPrice/ following our cqrs skill (.claude/skills/cqrs):
   - POST /api/v1/listings/price-suggestion, RequireAuthorization, body { title, description?, category, condition }.
   - It is a query: it changes nothing, so no IUnitOfWork.
   - Validation with the same rules and error codes as Listing (title empty / too long, description too long, unknown category or condition) -> 400.
   - Response { suggestedPrice }: whole lei, at least 1.
   - The handler depends on an interface IPriceSuggester with no Microsoft.ML types; put it where our other service interfaces
     (like IPasswordHasher and ITokenService) live. The implementation PriceSuggester is in Infrastructure/ML and uses
     PredictionEnginePool, registered in AddCampusMarketMl with the model file from MlOptions.ModelsPath (path relative to AppContext.BaseDirectory).
   - The ML input and output classes for the API live in Infrastructure/ML and match the column names used in training.
4. A quality test for the model: a new test project tests/CampusMarket.ML.Tests (added to the solution) that loads
   ml/models/price-regression.zip and ml/data/listings.csv, uses the same non-spam rows and the same split (seed 42, test 20%),
   and fails if R² < 0.8. Add one more check: for the same title, New is priced higher than Worn.
5. Make sure the integration tests can start the API with the model (the .zip must reach their output folder). Do not write new
   unit or integration tests for the slice yet; we generate them with the skills in the next step.
6. Postman: one request "Price suggestion" (200) in the Listings folder, after login. README: the endpoint in the table and a short
   note in the "Machine learning" section with the metrics. PROMPTS.md: Run 2 under ml-training with this prompt.
Run dotnet build, dotnet test and the training, show me the metrics, three example suggestions (a laptop New, a used calculator,
course notes Worn) and list every file you changed. Do not change existing tests. Do not commit or push.
```

- **Result:** `dotnet build` on the whole solution: 0 warnings, 0 errors. `dotnet test`: 215 unit, 10 architecture, 89 integration and 4 new ML tests (`CampusMarket.ML.Tests`), all green. `dotnet run --project ml/CampusMarket.ML.Training` on the committed dataset prints: `R² 0.830`, `MAE 81.24 lei`, `RMSE 189.39 lei` — above the 0.8 bar, so the base FastTree model is kept and no `log(Price)` fallback is applied. Three sample suggestions (Laptop ThinkPad T480, Electronics, New → **2967 lei**; Casio fx-991EX, Electronics, Good → **84 lei**; Rezumat examen Analiza, Notes, Worn → **11 lei**) line up with the dataset's per-category ranges.
- **Files changed:**
    - `ml/CampusMarket.ML.Training/CampusMarket.ML.Training.csproj` (added `Microsoft.ML.FastTree` 5.0.1)
    - `ml/CampusMarket.ML.Training/PriceTrainer.cs` (new, FastTree regression; filters out spam, one-hot Category/Condition, `FeaturizeText` on Title and Description; supports a `logLabel: true` variant with price-space metrics for the fallback the task describes)
    - `ml/CampusMarket.ML.Training/Program.cs` (registers `new PriceTrainer()` in the trainers list)
    - `ml/models/price-regression.zip` (new committed artefact, ≈208 KB)
    - `src/CampusMarket.Api/CampusMarket.Api.csproj` (added `Microsoft.ML.FastTree` 5.0.1 — needed at runtime, because FastTree registers its own loadable classes and `PredictionEnginePool.Load` otherwise throws `FileNotFoundException` on `Microsoft.ML.FastTree`)
    - `src/CampusMarket.Api/Common/Pricing/IPriceSuggester.cs` (new, no `Microsoft.ML` types)
    - `src/CampusMarket.Api/Infrastructure/ML/PriceInput.cs`, `PricePrediction.cs`, `PriceSuggester.cs` (new; `PriceSuggester` calls `PredictionEnginePool<PriceInput, PricePrediction>.Predict` and rounds to whole lei with a `≥ 1` floor)
    - `src/CampusMarket.Api/Infrastructure/ML/MlServiceCollectionExtensions.cs` (now registers the `PredictionEnginePool` from `MlOptions.ModelsPath` + `AppContext.BaseDirectory` and the `IPriceSuggester → PriceSuggester` singleton)
    - `src/CampusMarket.Api/Features/Listings/SuggestPrice/SuggestPriceRequest.cs`, `SuggestPriceQuery.cs`, `SuggestPriceResponse.cs`, `SuggestPriceHandler.cs`, `SuggestPriceEndpoint.cs` (new slice, follows `.claude/skills/cqrs`: handler is `IQueryHandler<,>` with no `IUnitOfWork`, validation reuses the exact `Listing.*` error codes)
    - `src/CampusMarket.Api/Features/Listings/ListingsEndpoints.cs` (mapped `SuggestPriceEndpoint`)
    - `tests/CampusMarket.ML.Tests/CampusMarket.ML.Tests.csproj` (new xUnit + FluentAssertions project, references the training project so the FastTree assembly is on the test runner's path)
    - `tests/CampusMarket.ML.Tests/TestPaths.cs` (walks up to find `ml/`, so tests load the model and the CSV from the repo root)
    - `tests/CampusMarket.ML.Tests/PriceModelQualityTests.cs` (R² ≥ 0.8 on the same non-spam test split, and three `[Theory]` cases asserting `New > Worn` for the same title and description)
    - `CampusMarket.sln` (new `CampusMarket.ML.Tests` project, nested under the `tests` solution folder)
    - `postman/CampusMarket.postman_collection.json` (new `SuggestPrice` folder inside `Listings`, one request "Price suggestion - laptop (200)" that asserts `suggestedPrice` is a whole number ≥ 1; inherits the collection-level bearer token set by `Login`)
    - `README.md` (new row in the "Implemented so far" table for `POST /api/v1/listings/price-suggestion`, and a "Price suggestion model (B3)" subsection under "Machine learning" with the metrics and the quality-gate description)
    - `PROMPTS.md` (this entry)
- **Problems seen:**
    1. The first scratchpad consumer of `price-regression.zip` only referenced `Microsoft.ML` and threw `FileNotFoundException: 'Microsoft.ML.FastTree'` on load — the same failure the API would have hit at first request. Fixed by adding `Microsoft.ML.FastTree` to `CampusMarket.Api.csproj` too (it was only added to the training project at first, where the task's "add Microsoft.ML.FastTree with the same version as Microsoft.ML" bullet pointed).

### Run 3

- **Why:** B3 added the price model; B4 adds the second one, category classification, the same way: one new trainer, the `category-classification.zip` artefact, a `SuggestCategory` slice behind an `ICategorySuggester` interface, and a quality test that guards the model in CI. Written by Ancuța Ilenei with Claude Code.
- **Prompt:** (new session)

```
Week 5, task B4: category suggestion, built the same way as Sara's price suggestion (B3).
1. Training: CategoryTrainer : IModelTrainer, registered in Program.cs. Only non-spam rows. Features: Title and
   Description with FeaturizeText; label Category; a multiclass trainer (SDCA maximum entropy). Metrics on the test
   split: micro and macro accuracy and log loss. ModelFileName category-classification.zip, saved and committed.
2. API: POST /api/v1/listings/category-suggestion, RequireAuthorization, body { title, description? }, a query
   (no IUnitOfWork), the same validation and error codes as SuggestPrice, response { category, confidence }
   with confidence between 0 and 1. The handler depends on ICategorySuggester in Common/Categories/, the
   implementation uses PredictionEnginePool and lives in Infrastructure/ML.
3. ML.Tests: CategoryModelQualityTests on the same non-spam split, micro accuracy >= 0.9, plus a few listings
   written by hand that are not in the CSV.
4. No unit or integration tests for the slice yet (generated with the skills). README, Postman and PROMPTS.md.
Do not change existing tests. Do not commit or push.
```

- **Result:** `dotnet run --project ml/CampusMarket.ML.Training` prints for the category model `Micro accuracy 1.000`, `Macro accuracy 1.000`, `Log loss 0.014`; the price model metrics are unchanged (R² 0.830), and `price-regression.zip` was left as it was. Build 0 errors, 0 warnings; all existing tests pass, plus 4 new ML tests (8 in `CampusMarket.ML.Tests`). Checked by hand on the running API: a ThinkPad laptop → `Electronics` 0.99, "Notițe Analiză matematică" → `Notes` 1, empty title 400 `Listing.TitleEmpty`, description of 1001 characters 400 `Listing.DescriptionTooLong`, no token 401.
- **Files changed:**
    - `ml/CampusMarket.ML.Training/CategoryTrainer.cs` (new) and `Program.cs` (registers `new CategoryTrainer()`)
    - `ml/models/category-classification.zip` (new committed artefact)
    - `src/CampusMarket.Api/Common/Categories/ICategorySuggester.cs` (new, with the `CategorySuggestion` record)
    - `src/CampusMarket.Api/Infrastructure/ML/CategoryInput.cs`, `CategoryPrediction.cs`, `CategorySuggester.cs` (new) and `MlServiceCollectionExtensions.cs` (the second `PredictionEnginePool` and the `ICategorySuggester` singleton)
    - `src/CampusMarket.Api/Features/Listings/SuggestCategory/` (request, query, response, handler, endpoint) and `ListingsEndpoints.cs` (one line)
    - `tests/CampusMarket.ML.Tests/CategoryModelQualityTests.cs` (new)
    - `README.md`, `postman/CampusMarket.postman_collection.json` (new `SuggestCategory` folder with 4 requests), `PROMPTS.md`
- **Problems seen:**
    1. The accuracy is 1.000 because the dataset is synthetic and each category has its own words. The quality bar was set at 0.9 so that a retrain on a changed dataset does not fail for small differences.
    2. There is no clothing category, so "Hanorac facultate, mărimea M" gets `LabEquipment` (0.82). It is only a suggestion; noted in the README.
    3. In Git Bash, `curl -d` with diacritics sent the body in the wrong encoding and the API answered 400 `Request.Invalid`; sent from a UTF-8 file, the same body gives 200. Not a bug in the API.
    4. SonarCloud showed 2.0% duplication on new code: the title and description checks of `SuggestCategoryHandler` were the same lines as in `SuggestPriceHandler` and `Listing`. Fixed by moving them into `Listing.ValidateText(title, description)`, used by `Listing`, `SuggestPrice` and `SuggestCategory`; the error codes, messages and order did not change, and all tests pass without changes.

- **Commits:** code 2ec87e6; tests generated in f98a859 (see Run 18 of unit-testing and Run 18 of integration-testing)

### Run 4

- **Why:** B5, the third model and the moderation around it: `SpamTrainer`, the `spam-detection.zip` artefact, a new `Report` entity and `Features/Moderation/` (report a listing, the Admin list, hide a listing), `Listing.Hide()`, and the hidden listings in favorites that A3 left for this task. Written by Ancuța Ilenei with Claude Code.
- **Prompt:** (new session)

```
Week 5, task B5: spam + moderation. Same ML structure as B3 and B4.
1. Training: SpamTrainer : IModelTrainer, registered in Program.cs. Binary classification on IsSpam with all rows.
   Features: Title and Description with FeaturizeText, Price in bins (spam has absurd prices). Metrics: accuracy, AUC,
   F1, precision, recall. ModelFileName spam-detection.zip, saved and committed.
2. API: ISpamDetector in Common/Moderation (no ML types), SpamDetector in Infrastructure/ML with PredictionEnginePool.
3. Domain: Report in Domain/Reports (a member reports a listing with a reason; the spam model reports it on its own),
   Listing.Hide() (only Active or Reserved; a reserved listing loses its reservation).
4. Features/Moderation: POST /listings/{id}/reports (any member, once per listing), GET /moderation/listings
   (Admins, one row per reported listing, paging), POST /moderation/listings/{id}/hide (Admins).
   PublishListing asks the spam model and adds an automatic report when it says spam.
5. Favorites: a hidden listing cannot be saved and is not in GET /me/favorites (left from A3).
6. Migration AddReports. ML.Tests: SpamModelQualityTests (AUC and F1 over a bar, examples written by hand).
7. No unit or integration tests yet (generated with the skills). README, Postman and PROMPTS.md.
Do not change existing tests (except what the new constructor of PublishListingHandler needs, through the skill). Do not commit or push.
```

- **Decisions:**
    - Automatic marking happens at publish: that is when a listing becomes public. A suspicious listing stays published but gets a report with no reporter and the spam probability, so an Admin decides; the model never hides anything by itself.
    - The Admin list shows one row per listing (not one per report), the most reported first, and leaves hidden listings out, because they are already handled.
    - A member reports a listing at most once (409 `Report.AlreadyExists`, plus a unique index on listing + reporter); the seller cannot report their own listing; only `Active` and `Reserved` listings can be reported, like what other members can see.
    - Hiding a reserved listing cancels its reservation, so the buyer is not left waiting for a listing nobody can see.
    - `GET /api/v1/listings/{id}` still returns a hidden listing, the same way it already returns drafts; the search, the favorites and reservations leave it out.
- **Result:** `dotnet run --project ml/CampusMarket.ML.Training` prints for the spam model `Accuracy 1.000`, `AUC 1.000`, `F1 1.000`, `Precision 1.000`, `Recall 1.000`; the price and category models are unchanged and their `.zip` files were left as they were. Migration `AddReports` (only the new `Reports` table); `dotnet ef migrations has-pending-model-changes`: no changes. Checked by hand on the running API: a normal listing published without a report, a spam one published with an automatic report (0.97); report 201, again 409, own listing 400, empty reason 400, unknown listing 404, no token 401; the list as a student 403, `page=0` 400; hide 204, again 400, as a student 403, unknown 404; after hiding, the listing is gone from the list, the search and the favorites, and cannot be saved or reserved.
- **Files changed:**
    - `ml/CampusMarket.ML.Training/SpamTrainer.cs` (new) and `Program.cs` (registers `new SpamTrainer()`); `ml/models/spam-detection.zip` (new)
    - `src/CampusMarket.Api/Common/Moderation/ISpamDetector.cs`; `Infrastructure/ML/SpamInput.cs`, `SpamPrediction.cs`, `SpamDetector.cs` (new); `MlServiceCollectionExtensions.cs` (the third `PredictionEnginePool`)
    - `src/CampusMarket.Api/Domain/Reports/` (`Report`, `IReportRepository`), `Domain/Listings/Listing.cs` (`Hide()`), `Domain/Favorites/Favorite.cs` (refuses `Hidden`)
    - `src/CampusMarket.Api/Infrastructure/Persistence/` (`ReportConfiguration`, `EfReportRepository`, `DbSet<Report>`), migration `AddReports`
    - `src/CampusMarket.Api/Features/Moderation/` (`ReportListing`, `GetReportedListings`, `HideListing`, `ModerationEndpoints`); `Features/Listings/PublishListing/PublishListingHandler.cs` (spam check); `Features/Favorites/GetMyFavorites/GetMyFavoritesHandler.cs` (leaves hidden listings out)
    - `src/CampusMarket.Api/CampusMarketSetup.cs` (two `using` lines, the repository and `MapModerationEndpoints`)
    - `tests/CampusMarket.ML.Tests/SpamModelQualityTests.cs` (new)
    - `README.md` (endpoints, error codes, a "Moderation" section, `Hidden` in the diagrams, "Spam detection model (B5)"), `postman/CampusMarket.postman_collection.json` (new `Moderation` folder with 17 requests and the variable `modListingId`), `PROMPTS.md`
- **Problems seen:**
    1. The scores are 1.000 because the dataset is synthetic, as for the category model. The quality bar is AUC 0.95 and F1 0.9.
    2. Changing the constructor of `PublishListingHandler` broke the build of the existing `PublishListingHandlerTests`; they were updated by the unit-testing run (see Run 19), not by hand.
    3. `ReportedListingItem.From` lives in the response file, like `MyFavoriteItem.From`, so it is outside the coverage and Stryker filters (`*Handler.cs`); its logic is tested through `GetReportedListingsHandler`.
- **Commits:** code 21011ac; tests generated in ffc2895 (see Run 19 of unit-testing and Run 19 of integration-testing)

## confirm-sale

Week 5, task A1 (confirm a sale). Feature slice, built with the `cqrs` skill and the shape of the existing reservation slices. Run by Sara Matasel.

### Run 1

- **Prompt:**

```
Week 5, task A1: confirm a sale. Follow our cqrs skill (.claude/skills/cqrs) and the style of the existing reservation slices
(PublishListing, ReserveListing, CancelReservation). Use the empty folder Features/Listings/ConfirmSale/ (remove its .gitkeep).

1. Domain (read Listing.cs and Reservation.cs first):
   - Reservation.Complete(): internal, like Cancel(). Only an Active reservation can be completed, otherwise
     Error.Validation("Reservation.NotActive", ...) with the same message style as Cancel. Sets Status = Completed.
   - Listing.ConfirmSale(Guid reservationId, DateTimeOffset currentTime):
     reservation not on this listing -> Error.NotFound("Reservation.NotFound", "Reservation not found.") (same as CancelReservation);
     reservation not Active -> the error from Complete() (Reservation.NotActive);
     reservation expired (reservation.IsExpired(currentTime)) -> Error.Validation("Reservation.Expired", "The reservation has expired.");
     then reservation.Complete(), Status = ListingStatus.Sold, Version++.
     A listing has at most one active reservation (Reserve needs an Active listing and sets it to Reserved), so there are
     no other active reservations to cancel; write this in a short comment.
   - Listing.UpdateDetails: replace the comment about ConfirmSale with the real check: a Sold listing cannot be changed ->
     Error.Validation("Listing.Sold", "A sold listing cannot be changed.").
2. Slice Features/Listings/ConfirmSale/: ConfirmSaleCommand(ListingId, ReservationId, RequesterId, IsAdmin), handler with
   IListingRepository, IUnitOfWork and TimeProvider. Order: 404 Listing.NotFound, then 403 Listing.NotOwner (only the seller
   or an Admin can confirm, the buyer cannot), then the domain errors from ConfirmSale, then repository.Update and one CommitAsync.
   Endpoint: POST /api/v1/listings/{id:guid}/reservations/{reservationId:guid}/confirm, RequireAuthorization,
   204 on success, 404 / 403 (JSON, like the other endpoints) / 400. Map it in ListingsEndpoints.cs.
3. No migration should be needed (Completed and Sold already exist); check with dotnet ef migrations has-pending-model-changes.
4. Postman, in the Reservations folder: "Confirm sale (204)" after a reservation is made, then "Confirm sale again (400)"
   and "Update a sold listing (400 Listing.Sold)". README: the endpoint in the table, Listing.Sold and Reservation.Expired in
   the error codes, and the lifecycle Draft -> Active -> Reserved -> Sold. PROMPTS.md: add this prompt under a new section
   "## confirm-sale", Run 1.
Do not write unit or integration tests yet (we generate them with the skills next). Do not change existing tests.
Run dotnet build and dotnet test (existing tests must pass), list every file you changed. Do not commit or push.
```

- **Skill version:** `cqrs` v4 (last skill change in `.claude/skills/cqrs/SKILL.md`); the slice itself is hand-written, not generated by a skill.
- **Result:** `dotnet build` finishes with 0 errors and 0 warnings. `dotnet ef migrations has-pending-model-changes` answers "No changes have been made to the model since the last migration." `dotnet test` passes **343/343** (229 unit + 100 integration + 10 architecture + 4 ML), unchanged from before the slice.
- **Files changed:**
    - `src/CampusMarket.Api/Domain/Listings/Reservation.cs` (new `internal Result<Reservation> Complete()` next to `Cancel`, with the same `Reservation.NotActive` message style)
    - `src/CampusMarket.Api/Domain/Listings/Listing.cs` (new `ConfirmSale(Guid reservationId, DateTimeOffset currentTime)` and a `Listing.Sold` guard at the top of `UpdateDetails` replacing the "will be added later" comment)
    - `src/CampusMarket.Api/Features/Listings/ConfirmSale/ConfirmSaleCommand.cs` (new)
    - `src/CampusMarket.Api/Features/Listings/ConfirmSale/ConfirmSaleHandler.cs` (new; order is 404 Listing.NotFound → 403 Listing.NotOwner → domain errors → `repository.Update` → one `CommitAsync`)
    - `src/CampusMarket.Api/Features/Listings/ConfirmSale/ConfirmSaleEndpoint.cs` (new; `POST /api/v1/listings/{id:guid}/reservations/{reservationId:guid}/confirm`, `RequireAuthorization`, maps `NotFound`/`Forbidden`/`Validation` like the other reservation endpoints)
    - `src/CampusMarket.Api/Features/Listings/ConfirmSale/.gitkeep` (removed)
    - `src/CampusMarket.Api/Features/Listings/ListingsEndpoints.cs` (mapped `ConfirmSaleEndpoint`)
    - `postman/CampusMarket.postman_collection.json` (new requests at the end of the `Reservations` folder: three setup requests on a fresh listing — `Setup: create listing for confirm sale (201)`, `Setup: publish listing for confirm sale (204)`, `Setup: reserve listing for confirm sale (201)` — then the required `Confirm sale (204)`, `Confirm sale again (400)` and `Update a sold listing (400 Listing.Sold)`; two new collection variables `saleListingId` and `saleReservationId`)
    - `README.md` (new `ConfirmSale` row in the "Implemented so far" table; `Reservation.NotActive` wording extended to "cancel or confirm", and new `Reservation.Expired` and `Listing.Sold` rows in the error-codes table — the `Draft -> Active -> Reserved -> Sold` lifecycle is already in the state diagram)
    - `PROMPTS.md` (this entry)
- **Problems seen:**
    1. The task says to put the three Confirm sale requests "in the Reservations folder" **after a reservation is made**, but the only reservation in that folder (`reservationId`) is already cancelled by the end of the existing flow, and running Confirm sale on the main `listingId` would set its status to `Sold` and break the later `Update listing - valid (204)` request in the `UpdateListing` folder. Fixed by scoping the confirm-sale sequence to a brand-new listing (new `saleListingId`/`saleReservationId` variables) at the end of the `Reservations` folder, so the main `listingId` lifecycle used by `UpdateListing` and `DeleteListing` is untouched.

## reservation-expiry

Week 5, task A4 (expire reservations automatically). Feature slice plus a hosted background service, built with the `cqrs` skill and the shape of the existing reservation slices. Run by Sara Matasel.

### Run 1

- **Prompt:**

```
Week 5, task A4: expire reservations automatically. Follow our cqrs skill (.claude/skills/cqrs) and the style of the
reservation slices (ReserveListing, CancelReservation, ConfirmSale). Read Listing.cs and Reservation.cs first.

1. Domain:
   - Reservation.Expire(): internal, like Cancel() and Complete(). Only an Active reservation can expire,
     otherwise Error.Validation("Reservation.NotActive", ...) in the same message style. Sets Status = Expired.
   - Listing.ExpireReservations(DateTimeOffset currentTime): returns the number of reservations it expired (0 or 1,
     a listing has at most one active reservation). For the active reservation where IsExpired(currentTime) is true:
     Expire() it, set the listing back to Active and Version++. If nothing expired, change nothing and return 0.
2. Command slice Features/Listings/ExpireReservations/: ExpireReservationsCommand (no requester, it is a system job),
   handler with IListingRepository, IUnitOfWork and TimeProvider: GetAllAsync, call ExpireReservations on each listing,
   repository.Update only for the listings that changed, one CommitAsync only when at least one changed.
   It returns the number of expired reservations. No endpoint.
3. Background service in Infrastructure (for example Infrastructure/Background/ReservationExpiryService.cs):
   a BackgroundService that every Interval runs the command in a new DI scope (the handler is Scoped).
   Use a PeriodicTimer built from the TimeProvider. Options "ReservationExpiry" with Enabled (default true) and
   Interval (default 5 minutes), registered in CampusMarketSetup; register the hosted service only when Enabled.
   Log how many reservations were expired; catch and log exceptions so one failed run does not stop the service.
4. The integration test factory must set ReservationExpiry:Enabled to false, like it turns off the admin seed,
   so the background service never runs during the other tests.
5. GetMyReservations keeps showing IsExpired(now) as Expired (a reservation can be expired before the job runs).
6. Architecture tests must stay green: the background service lives in Infrastructure, the handler in Features.
7. README: a short "Reservation expiry" note (what the job does, the two settings, and that a listing becomes
   Active again so others can reserve it). PROMPTS.md: a new section "## reservation-expiry", Run 1, with this prompt.
Do not write unit or integration tests yet (we generate them with the skills next). Do not change existing tests
except the factory setting in point 4. Run dotnet build and dotnet test, list every file you changed. Do not commit or push.
```

- **Skill version:** `cqrs` v4 (last skill change in `.claude/skills/cqrs/SKILL.md`); the slice and the background service are hand-written, not generated by a skill.
- **Result:** `dotnet build` on the whole solution: 0 errors, 0 warnings. `dotnet test`: 215 unit + 10 architecture + 100 integration + 4 ML = 329 green, unchanged from before the slice.
- **Files changed:**
    - `src/CampusMarket.Api/Domain/Listings/Reservation.cs` (new `internal Result<Reservation> Expire()` next to `Cancel` and `Complete`, with the same `Reservation.NotActive` message style)
    - `src/CampusMarket.Api/Domain/Listings/Listing.cs` (new `ExpireReservations(DateTimeOffset currentTime)` returning `int`; expires the active reservation when `IsExpired(now)` is true, sets `Status = Active` and `Version++`)
    - `src/CampusMarket.Api/Features/Listings/ExpireReservations/ExpireReservationsCommand.cs` (new; empty command, no requester — it is a system job)
    - `src/CampusMarket.Api/Features/Listings/ExpireReservations/ExpireReservationsHandler.cs` (new; `IListingRepository`, `IUnitOfWork` and `TimeProvider`; returns the count of expired reservations, calls `repository.Update` only for the listings that changed and `CommitAsync` only when at least one changed)
    - `src/CampusMarket.Api/Infrastructure/Background/ReservationExpiryOptions.cs` (new; `Enabled` default `true`, `Interval` default 5 minutes, section `ReservationExpiry`)
    - `src/CampusMarket.Api/Infrastructure/Background/ReservationExpiryService.cs` (new `BackgroundService`; `PeriodicTimer` built from `TimeProvider`, resolves the handler in a fresh `IServiceScope` on every tick, logs the number expired and catches exceptions so one failed run does not stop the service)
    - `src/CampusMarket.Api/CampusMarketSetup.cs` (bind `ReservationExpiryOptions`, register `ReservationExpiryService` as a hosted service only when `Enabled`)
    - `tests/CampusMarket.IntegrationTests/Infrastructure/CampusMarketApiFactory.cs` (new `builder.UseSetting("ReservationExpiry:Enabled", "false")`, next to the admin seed and JWT key overrides)
    - `README.md` (new `### Reservation expiry` subsection under "Use cases" describing the job and the two `ReservationExpiry` settings)
    - `PROMPTS.md` (this entry)
- **Problems seen:** none.

## error-handling

Week 5, task A5 (global exception handler + ProblemDetails). Infrastructure code, no new slice. Written step by step by Ancuța Ilenei in one Claude Code session (no skill covers this part); the tests were generated afterwards with the integration-testing skill (Run 13 above).

### Run 1

- **Prompts:** the code was built in small steps, each one explained before writing it: (1) `Infrastructure/ErrorHandling/GlobalExceptionHandler.cs`, an `IExceptionHandler`; (2) `ErrorHandlingExtensions.AddCampusMarketErrorHandling`, with `AddProblemDetails` and `AddExceptionHandler`; (3) the middleware order in `Program.cs`; (4) a manual check of every error type against the running API.
- **Decision:** only the answers that do not come from a handler become ProblemDetails. The handler errors keep `{ code, message, type }`, because changing them would touch every endpoint (also the ones other people were working on this week) and 16 integration test files, and the task keeps A5 inside `Infrastructure/ErrorHandling/` and `Program.cs`. Every body still has a `code`: ProblemDetails get one in the same style (`Auth.Unauthorized`, `Concurrency.Conflict`, ...).
- **Result:**
    - `GlobalExceptionHandler`: `DbUpdateConcurrencyException` → 409 `Concurrency.Conflict`; `BadHttpRequestException` → its own status (400) `Request.Invalid`; anything else → 500 `Server.Error` with a fixed message, the real exception only in the log.
    - `AddCampusMarketErrorHandling`: every ProblemDetails gets `instance`, `traceId` and a `code` (`TryAdd`, so the code set by the exception handler is kept).
    - `Program.cs`: `UseExceptionHandler`, `UseStatusCodePages`, then `UseAuthentication`, `UseAuthorization` and `UseRateLimiter`. Authentication and authorization had to be called explicitly: added automatically, they run before our middleware and their 401/403 stay without a body.
    - Checked by hand on the running API: 401, 403 (Admin policy), 404, 400 (`"category": "Banana"`), 429 and a handler 400 (`Money.Invalid`, unchanged). Before the tests were updated, 12 integration tests failed, all of them checks of an empty 401/403 body.
    - README: new section "Error format", the 409 in the use case table and in "Key flows". Postman: the two no-token requests check the ProblemDetails, and a new folder `Errors` with 404, 405, 400 and 403.
- **Problems seen:**
    1. In Development, ASP.NET throws `BadHttpRequestException` for JSON it cannot read. The first version of the handler only knew `DbUpdateConcurrencyException`, so `"category": "Banana"` would have become 500 instead of 400; fixed before the first run by mapping it to its own status code.
    2. A 409 could not be produced by sending two reservations in parallel by hand (the requests arrive one after the other locally, the second gets 400 `Listing.NotActive`); the integration test makes the conflict happen on purpose instead.
- **Commits:** code db40206; tests generated in 33c179b (see Run 14 of integration-testing)

## docker

Backlog task (Docker + docker-compose + health checks). Dockerfile, `docker-compose.yml`, `.dockerignore` and the `/health` endpoint written by Sara Matasel; the review asked for the README, Postman, the tests and this entry, which were added by Ancuța Ilenei on the same branch.

### Run 1

- **What:** a two-stage `Dockerfile` (SDK image to publish, ASP.NET image to run) that fails the build when `ml-models/price-regression.zip` is missing; `docker-compose.yml` with port `5080:8080` and `ASPNETCORE_ENVIRONMENT=Development`; `AddHealthChecks().AddDbContextCheck<CampusMarketDbContext>()` and `MapHealthChecks("/health")`.
- **Review changes:** the first version ran as root and set `Development` in the image, and Sonar failed on the Dockerfile. Fixed by running as the image's non-root user (`USER $APP_UID`), with the SQLite file in `/app/data` that this user can write to, and by moving `Development` to `docker-compose.yml`.
- **Added after the review:** README ("With Docker" under "How to run", `/health` in the endpoint table), Postman (folder `Health` with `GET /health`), integration tests generated with the integration-testing skill (see the "Health check" run of integration-testing), and this entry. `main` was merged into the branch first, so the tests run against the error handling of A5.
- **Problems seen:**
    1. There is no volume in `docker-compose.yml`, so the database is lost when the container is removed; written in the README.
    2. Running the image without `docker-compose` uses `Production`, which has no JWT key; written in the README that the container is a local demo.
- **Commits:** code 956ebe8, 1ae3ae9; README and Postman 516d3b2; skill 56fdb10; tests c1eebb4

### Run 2

Follow-up of Run 1, asked by Sara Matasel after the week 5 tasks were merged; written by Ancuța Ilenei with Claude Code. No C# code changes, so no skill runs.

- **Prompt:** `Look at the latest main and finish the Docker part: what is still missing after the first Docker PR.`
- **What:**
    - `docker-compose.yml`: a named volume `campusmarket-data` on `/app/data`, so the SQLite database is kept after `docker compose down` (problem 1 of Run 1). The volume takes the owner of `/app/data` from the image, so the non-root user can still write to it.
    - `Dockerfile`: the build checks all three models (`price-regression.zip`, `category-classification.zip`, `spam-detection.zip`), not only the price one; B4 and B5 added two models and a missing one would only fail at the first request. A `HEALTHCHECK` on `/health` (curl installed in the runtime image, with `--no-install-recommends` and the apt lists removed), so Docker shows the container as `healthy` or `unhealthy`.
    - `.dockerignore`: `tests`, `postman`, `.postman`, `.github`, `.claude` and `*.log` are not sent to the build, the API project does not need them.
    - CI: a new `docker` job in `build.yml` that runs `docker compose up -d --build --wait` (it waits until the health check says `healthy`), calls `/health`, prints the logs if something failed and removes the container and the volume.
    - README: "With Docker" describes the volume, the health check, the three models and the CI job.
- **Result:** checked locally with Docker Desktop: `docker compose up -d --build --wait` → the container is `healthy`; it runs as the `app` user and `/app/ml-models` has the three models. A member registered before `docker compose down` can still log in after `up` (the database is on the volume). On an empty volume (`down -v`), the whole Postman collection run with newman against `http://localhost:5080`: 129 requests, 252 assertions, 0 failed.
- **Problems seen:**
    1. Docker Desktop needs WSL 2, which was not installed on the laptop (`wsl --install --no-distribution` and a restart first).
    2. Running the image without `docker-compose` still uses `Production`, which has no JWT key (problem 2 of Run 1, unchanged).
- **Commits:** code 61782f9

## favorites

Week 5, task A3 (favorites). New entity and three slices, written by Ancuța Ilenei with Claude Code in the shape of the existing slices (`cqrs` skill conventions, paging copied from `GetListings`); the tests were generated afterwards with the unit-testing (Run 16) and integration-testing skills.

### Run 1

- **Prompt:** `A3: Favorite entity in Domain/Favorites and the feature in Features/Favorites: add, remove and GET /me/favorites with paging. Write it in the style of the existing slices, do not write tests yet (they are generated with the skills), and run the existing tests.`
- **Decisions:**
    - Routes under `/api/v1/members/me/favorites`, next to `/members/me/reservations`; the member is always the user in the token, so there is no 403 case. Own group `FavoritesEndpoints`, mapped in `CampusMarketSetup`, so `MembersEndpoints.cs` is not touched.
    - `POST .../{listingId}` → 201 with `Location` to the list, like `ReserveListing`; `DELETE .../{listingId}` → 204. A listing is saved at most once (409 `Favorite.AlreadyExists`, plus a unique index on member + listing). The seller cannot save their own listing and a draft cannot be saved, like the reservation rules.
    - Foreign keys from `Favorites` to `Listings` and `Members` with cascade delete, so deleting a listing or a member removes its favorites.
    - Hidden listings are not handled yet: nothing can hide a listing before B5, so those branches could not be tested. B5 adds them together with `Listing.Hide()`.
- **Result:** `Domain/Favorites/` (`Favorite`, `IFavoriteRepository`), `Infrastructure/Persistence/` (`FavoriteConfiguration`, `EfFavoriteRepository`, `DbSet<Favorite>`), migration `AddFavorites`, `Features/Favorites/` (`AddFavorite`, `RemoveFavorite`, `GetMyFavorites`, `FavoritesEndpoints`), two lines in `CampusMarketSetup.cs`. `dotnet ef migrations has-pending-model-changes`: no changes. Build 0 errors, 0 warnings; the existing 398 tests pass. Checked by hand on the running API: draft 400, 201, again 409, own listing 400, unknown listing 404, list 200, page 0 400, no token 401, remove 204, again 404, and deleting the listing removes the favorite from the database. README: endpoints, error codes and a "Favorites" section. Postman: a new `Favorites` folder with 13 requests and the variable `favListingId`.
- **Problems seen:**
    1. The first version also refused hidden listings and hid them in the list. No code can make a listing `Hidden` yet, so those branches were removed before the tests and moved to B5.
- **Commits:** code 91b81a2; tests generated in 25c29b8 (see Run 16 of unit-testing and Run 16 of integration-testing)

## reviews

Week 5, task A2 (seller reviews). New entity and two slices, written by Ancuța Ilenei with Claude Code in the shape of the existing slices (`cqrs` skill conventions, paging copied from `GetMyFavorites`); the tests were generated afterwards with the unit-testing (Run 20) and integration-testing (Run 20) skills.

### Run 1

- **Prompt:** `A2: Review entity in Domain/Reviews and the feature in Features/Reviews. After a completed sale the buyer rates the seller from 1 to 5, once. GET /members/{id}/reviews shows the reviews and the average. Write it in the style of the existing slices, do not write tests yet (they are generated with the skills), and run the existing tests.`
- **Decisions:**
    - `POST /api/v1/listings/{id}/reviews`: the review belongs to one sale, and a listing is sold at most once, so "once" means one review per listing (409 `Review.AlreadyExists`, plus a unique index on `ListingId`). The reviewer is the user in the token.
    - Only the member of the completed reservation (the buyer) can review: anyone else, the seller included, gets 403 `Review.NotBuyer`, like the other "not yours" errors. A listing that is not `Sold` gets 400 `Review.SaleNotCompleted`.
    - An optional comment of at most 500 characters; empty or blank is stored as `null`, like the listing description.
    - `GET /api/v1/members/{id}/reviews` is public, like the other GET endpoints, and returns 404 for an unknown member. `averageRating` is over all the reviews (not only the page), with two decimals, and `null` when there are none.
    - Foreign keys from `Reviews` to `Listings` and to `Members` (seller and reviewer), all with cascade delete.
- **Result:** `Domain/Reviews/` (`Review`, `IReviewRepository`), `Infrastructure/Persistence/` (`ReviewConfiguration`, `EfReviewRepository`, `DbSet<Review>`), migration `AddReviews`, `Features/Reviews/` (`AddReview`, `GetMemberReviews`, `ReviewsEndpoints`), three lines in `CampusMarketSetup.cs`. `dotnet ef migrations has-pending-model-changes`: no changes. Build 0 errors, 0 warnings; the existing 596 tests pass. Checked by hand on the running API (on a separate database): before the sale 400, another member 403, the seller 403, rating 0 and 6 400, comment of 501 characters 400, the buyer 201 with `Location`, again 409, unknown listing 404, no token 401; the seller's reviews without a token 200 with the average 4.5 for ratings 5 and 4, `page=0` 400, `pageSize=101` 400, unknown member 404, and page 2 of size 1 still gives the average over both. README: endpoints, error codes and a "Seller reviews" section. Postman: a new `Reviews` folder with 13 requests.
- **Commits:** code 452f178; tests generated in 1d76998 (see Run 20 of unit-testing and Run 20 of integration-testing)

## full-coverage

Team final step: 100% coverage of the whole project in SonarCloud, not only of `Domain/` and the handlers. Done by Ancuța Ilenei with Claude Code; the tests were generated with the integration-testing skill (Run 21).

### Run 1

- **Prompt:** `Sonar shows these files below 100% (ReservationExpiryService, the five Ef*Repository, CreateListingEndpoint, CampusMarketSetup). Take the latest main and fix everything.`
- **How:** all four test projects run with coverage, the opencover reports merged with a small script that prints every line and branch not covered in any of them (the same list as Sonar). For each line: a test when the code can run, a change when it can never run.
- **Code changes:** two, same behaviour:
    - `CampusMarketSetup.cs`: `configuration.GetSection(...).Get<ReservationExpiryOptions>() ?? new ReservationExpiryOptions()` → `configuration.GetValue("ReservationExpiry:Enabled", new ReservationExpiryOptions().Enabled)`. The `??` side ran only when the section is missing, which never happens in the tests.
    - `ReservationExpiryService.cs`: `if (!await timer.WaitForNextTickAsync(stoppingToken)) break;` → `await timer.WaitForNextTickAsync(stoppingToken);`. `WaitForNextTickAsync` returns false only after the timer is disposed, and the timer is disposed only when the method ends, so the `break` could never run. The loop still ends through the cancellation.
- **Result:** after Run 21 of integration-testing, no uncovered line or branch in the merged reports; 674 tests pass.
- **Problems seen:**
    1. The duplicate checks in the `Ef*Repository.AddAsync` methods and the 409 of `CreateListingEndpoint` cannot be reached over HTTP, because the domain always creates a new `Guid`. They are tested on the repositories and with a fake repository instead of being removed, since the handlers and their unit tests use the `false` result.
- **Commits:** code 878ee4c; tests generated in 2872870 (see Run 21 of integration-testing)

### Run 2

- **Prompt:** `Sonar shows 3 issues and 98.9% coverage on main, and one issue on PR #71. Update everything and fix it so Sonar shows no issue and 100%.`
- **How:** Sonar's issues here are Roslyn suggestions (`roslyn` tag), which `dotnet build` does not print. Every project was built with `-p:ErrorLog=<file>.sarif%2Cversion=2.1` and the SARIF files read: the same 4 issues as Sonar (3 on main, 1 on the PR). `main` (with #70) was merged into the branch first.
- **Result:**
    - `ReservationExpiryService.cs`, CA1873 twice (a log argument is evaluated even when the level is off): the four log calls became `[LoggerMessage]` source-generated methods (`LogStarted`, `LogRunException`, `LogRunFailed`, `LogExpired`), which check the level first. Same messages, the tests that read the logs still pass.
    - `ml/.../PriceTrainer.cs`, CA1859: the trained model is declared as `ITransformer`, the type `IModelTrainer` returns, so `Evaluate(ITransformer)` is no longer flagged. The `// NOSONAR` comment above `Evaluate` was removed: it was on another line than the issue, and Roslyn issues are not suppressed by `NOSONAR`.
    - `ReservationExpiryServiceTests.cs`, CA1859: fixed by the skill (Run 22 of integration-testing).
    - Coverage: with the coverage of all test projects merged, the only lines left were the `Down()` methods of the EF Core migrations, which never run (the app only applies migrations). `**/Migrations/**` was added to `sonar.coverage.exclusions` in the CI, next to `Program.cs` and `ml/**`, and written in the README under "Coverage exclusions".
    - The SARIF logs still show CA1861 in two migrations; those files are generated by EF Core and Sonar does not report them.
- **Commits:** code 977eaaf; tests generated in 80b724d (see Run 22 of integration-testing)

## category-min-confidence

Follow-up of the category suggestion (Run 18 of unit-testing and Run 17 of integration-testing). Small change written by Diana with Claude Code; the tests were generated with the unit-testing (Run 25) and integration-testing (Run 28) skills.

### Run 1

- **Prompt:** `Make SuggestCategoryResponse.Category nullable, add SuggestCategoryHandler.MinConfidence = 0.7 and return a null category when the confidence is below it; add the sections "Feature: Minimum category confidence (Diana)" to the two skills, a sentence in the README, then run the skills for that section and dotnet test.`
- **Decisions:**
    - The threshold lives in the handler (`MinConfidence`, public const), not in the model or the endpoint: the model keeps returning its best guess and the rule "is it sure enough" stays in one place that the tests can read.
    - The confidence is always returned, also when the category is `null`, so a client can still see how unsure the model was. Exactly 0.7 counts as sure enough (`>=`).
    - The endpoint and the route do not change; only the JSON field `category` can now be `null`.
- **Result:** `SuggestCategoryResponse.Category` is `Category?`; the handler returns `null` below 0.7. Unit tests: one existing test raised from confidence 0.5 to 0.9 (nothing else changed in it), plus a `[Theory]` for 0.9 and exactly 0.7 and a test for 0.69 (`Category` null, `Confidence` 0.69). Integration tests: "Banana" gives 200 with `category` null and a confidence below 0.7; "Notite Analiza matematica, an I" gives 200 with `Notes`. README: the `SuggestCategory` row says `category` is null below 0.7. Build 0 errors, 0 warnings; all tests pass.
- **Commits:** code 36cefd6; skill sections dc876be; tests generated in 020f950 (see Run 25 of unit-testing and Run 28 of integration-testing)

## max-price

Week 6, task 2 (maximum price). The production code is the one from the task list, applied by Diana Preutu with Claude Code; the tests were generated afterwards with the unit-testing (Run 26) and integration-testing (Run 29) skills.

### Run 1

- **Prompt:** `Do the whole task, exactly as written, then run the skills: replace Money.cs with the given code (MaxAmount = 100000, Money.TooHigh), add the Money.TooHigh row to the README error table, append the section "Feature: Maximum price (Diana)" to the unit-testing and integration-testing SKILL.md, then run both skills for that section, add only those tests and run dotnet test.`
- **Decisions:**
  - The limit is a constant on the value object, `Money.MaxAmount` = 100000, so `Listing.Create` and every other place that builds a price get the rule without any extra check.
  - Exactly 100000 is allowed; above it the error is `Money.TooHigh` (Validation, 400), checked after `Money.Invalid`.
- **Result:** `Domain/Listings/Money.cs`, README error table, the two skill sections. Tests generated: `MoneyTests` (exactly the maximum, maximum + 0.01), `ListingTests` (`Listing.Create` above the maximum), integration `CreateListingTests` (price 200000 → 400 `Money.TooHigh`, nothing saved). Build 0 errors; 435 unit, 220 integration, 10 architecture and 13 ML tests pass.
- **Commits:** code eb8885b; skill sections 0d26845; tests generated in c989e44 (see Run 26 of unit-testing and Run 29 of integration-testing)

### Run 2

- **Prompt:** `SonarCloud reports 80% coverage on new code for PR #82. Find out which new line or condition is not covered. Do NOT change any file, do NOT commit.` Then: `Do not add that test. Instead, in src/CampusMarket.Api/Domain/Listings/Money.cs change public const decimal MaxAmount = 100_000m; to public static readonly decimal MaxAmount = 100_000m; and add a short comment above it: a const decimal gets a static constructor that never runs, so the line stays uncovered. Run dotnet build and dotnet test, then run the tests with coverage like the CI and confirm every new line of Money.cs is covered.`
- **Decisions:**
  - The coverage reports of the CI command (`dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings`) showed one uncovered new line: `public const decimal MaxAmount = 100_000m;`. The compiler gives a `const decimal` a static constructor, and every use of the constant is replaced by its value at compile time, so that constructor never runs, not even in the tests that use `Money.MaxAmount`.
  - `static readonly` instead of `const`: the field is now read at run time by `Money.Create`, so the static constructor runs and the line is covered, without a test written only for coverage and without excluding the line in Sonar. The value, the name and every use stay the same (no test or other code needed a compile-time constant), and a comment above the field says why it is not `const`.
- **Result:** `Money.MaxAmount` is `public static readonly decimal`. Build 0 warnings, 0 errors; 10 architecture, 438 unit, 13 ML and 222 integration tests pass. In both coverage reports of `CampusMarket.Api`, every sequence point and branch of `Money` is covered, including the static constructor (line 8, 1 visit) and both sides of `amount > MaxAmount`.
- **Commits:** code 7e6a4c6

## frontend-skeleton

Weeks 6–7 (plan of Sara and Anca), Anca's first task: the skeleton of the web frontend, so Sara can start her pages. Written by Ancuța Ilenei with Claude Code; the CORS tests were generated with the integration-testing skill (Run 30).

### Run 1

- **Prompt:** `Do the frontend skeleton from the plan, completely and correctly: the frontend/ project (React + Vite + TypeScript), routing, the menu, the API client with the token and automatic refresh, the login page, CORS in the API.`
- **Decisions:**
    - React + Vite + TypeScript (chosen with Sara), React Router; no UI library and no state library, only what the skeleton needs.
    - CORS policy `Frontend` with the origins from `Cors:AllowedOrigins` (`http://localhost:5173` in `appsettings.json`; the deployed address goes in `Cors__AllowedOrigins__0`). Any header and method, `Location` exposed, no credentials, because the token goes in the `Authorization` header and never in a cookie. `UseCors` is the first middleware, so 401/404/500 answers also carry the CORS header and the frontend can read the error.
    - The origins are read with `GetChildren()` instead of `Get<string[]>() ?? []`, so there is no branch that the tests can never reach (Sonar coverage).
    - API client (`frontend/src/api/client.ts`): adds the token, refreshes it 30 s before it expires and once more after a 401, then repeats the request. Requests that get a 401 together share one refresh, because the API rotates the refresh token and a second refresh with the old one would log the user out. A failed refresh clears the tokens; a network error does not.
    - Both error shapes of the API (`{ code, message, type }` and ProblemDetails) become one `ApiError { status, code, message }`, so pages check only `code`.
    - The tokens are kept in `localStorage`, so a page refresh keeps the user logged in, and other tabs follow a login or logout. The user (`id`, `email`, `role`) is read from the access token claims, only for the UI.
    - So two people do not edit the same lines: `src/api/` and `src/app/` are Anca's; every feature has its folder `src/features/<feature>/` and a `routes.tsx`; `router.tsx` and `menu.ts` only get added lines. The login page lives in `src/app/pages/` (part of the skeleton), so `features/auth/` stays free for Sara (register, forgot password).
    - `frontend/**` added to `sonar.coverage.exclusions` (TypeScript, outside the .NET coverage) and to `.dockerignore` (the API image does not need it).
- **Checks:** `npm run build` (type check + build) and `npm run lint` with no errors. With the API running: CORS checked with curl (allowed preflight, other origin, 200, 401, 404, 201 with `Location`). The API client checked with a scratch script against the running API, 12 cases, all passed: wrong password → `Auth.InvalidCredentials`, login, the role read from the token, a broken token → one refresh and the request repeated, three parallel 401s → one refresh, a token about to expire → refresh before the request, an invalid refresh token → logged out, logout → the old refresh token is refused, a 404 → the code from the body.
- **Result:** new `frontend/` (the skeleton, `README.md`, `.env.example`); `CampusMarketSetup.cs` (CORS policy), `Program.cs` (`UseCors`), `appsettings.json` (`Cors:AllowedOrigins`); README (how to run the frontend, CORS, tech stack, coverage exclusions); CI and `.dockerignore`. Build 0 warnings; 10 architecture, 454 unit, 13 ML and 235 integration tests pass. In the merged coverage every new line of `CampusMarketSetup.cs` is covered; the lines still uncovered are from the deploy and price feedback PRs (#95, #96).
- **Commits:** code 5b3d6a9; tests generated in 820290e (see Run 30 of integration-testing)

### Run 2

- **Prompt:** `Rearrange it so it looks much nicer: an interesting, well-made look that fits the idea of the app, something "wow".`
- **Decisions:**
    - One idea for the whole look: the campus notice board. Light theme = squared notebook paper, notes pinned with pushpins, yellow highlighter, ballpoint-blue accent; dark theme = a chalkboard with yellow chalk. The choice is saved in `localStorage` (system setting by default) and applied in `index.html` before the page paints, so it does not flash.
    - Fonts from npm (`@fontsource`), not from a CDN: Bricolage Grotesque for titles, Inter for text, Caveat for the handwritten notes, all with the Romanian letters.
    - A shared UI kit in `frontend/src/app/ui/` (Button, TextField, Card, PageHeader, Alert, Badge, EmptyState, Loading, Note, Pagination, icons, theme toggle, logo), so Sara's pages and Anca's pages look the same. Colours only through CSS variables in `index.css`.
    - Top bar with logo, menu (active page underlined with the highlighter), theme button, account chip (initials, email, Admin badge, logout); on phones the menu folds into a burger. The phone menu closes on navigation by remembering the page where it was opened (no `setState` in an effect).
    - Home page: hero, a cork board with four example notes (labelled "exemple", not real listings), the five categories as sticky notes, the three steps (publish, reserve, confirm the sale) and the three ML helpers (price, category, spam). The texts only describe what the API already does.
    - Login: chalkboard on the left, form on the right, password show/hide. 404: a sticky note "pagina asta s-a vândut deja…".
    - `prefers-reduced-motion` turns the animations off; visible focus ring; labels and `aria` on the icon buttons.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings. Screenshots with headless Chrome of the home page (light, dark, 500 px wide), the login page and the 404 page; two problems seen and fixed (one note covered the AI price of the next one; the ML section was too narrow in three columns).
- **Result:** new `frontend/src/app/ui/` (components + `ui.css`), `layout.css`, `pages/home.css`, `login.css`, `not-found.css`, new favicon; `index.css`, `Layout.tsx`, `menu.ts` (the login button is now on the right, not in the menu), the three pages, `index.html`, `main.tsx` (fonts) and `frontend/README.md` (look and UI kit) changed. No change in the API.
- **Commits:** code 5b3d6a9

### Run 3

- **Prompt:** `Make it look more professional, less like AI and simpler: well organised and structured, but simpler and clean.`
- **Decisions:**
    - The notice board look of Run 2 was dropped before any commit: no notebook grid, cork board, tilted notes, handwritten font or chalkboard. A neutral look instead: light grey background, white panels with a thin border, one blue accent, Inter only (the two decorative fonts were uninstalled). The dark theme is a plain dark grey.
    - The UI kit keeps the same components and props, only their style is simpler; `Note` became `Chip` (categories and filters), the badge tone `marker` became `warning`.
    - Home page: a short intro with the login button and the five categories, then "Cum funcționează" (three steps) and "Funcționalități" (price, category, anti-spam), as plain cards. Login: one centred card. 404: a short message and a link back.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings; screenshots with headless Chrome of the home page (light, dark, 500 px wide) and the login page.
- **Result:** `index.css`, `ui.css`, `layout.css`, the page styles and pages, `Logo.tsx`, `favicon.svg`, `Layout.tsx` (footer), `ui/Layout.tsx` (`Chip`), `main.tsx` (only Inter) and `frontend/README.md` changed. No change in the API.
- **Commits:** code 5b3d6a9

### Run 4

- **Prompt:** `A bit nicer and better ordered, the text more centred, simple; colours white, cream, beige; for now not too many things, only what the task asked, and working as the task says.`
- **Decisions:**
    - Warm palette in the tokens of `index.css`: cream background, warm white panels, beige borders, caramel-brown accent; the dark theme uses the same warm tones. Logo and favicon follow the accent.
    - The home page keeps only what the skeleton needs: a centred welcome, the login button, and after login who is logged in. The "how it works" and "features" sections were removed; the listings search replaces this page later.
    - Footer is one centred line.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings; screenshots (home light and dark, login, 500 px wide). The whole login flow driven in a headless Chrome through the DevTools protocol against the running API, 9 checks, all passed: wrong password → "Email sau parolă greșită.", login → home with the email and the Admin badge in the menu, tokens stored, still logged in after a page refresh, `/login` while logged in goes home, logout clears the user and the tokens, an unknown address shows the 404 page.
- **Result:** `index.css`, `HomePage.tsx`, `home.css`, `layout.css`, `Layout.tsx` (footer), `Logo.tsx`, `favicon.svg`, `index.html` and `frontend/README.md` changed. No change in the API.
- **Commits:** code 5b3d6a9

### Run 5

- **Prompt:** `The colours and the top bar are fine, but add something here so the home page looks a bit more professional.`
- **Decisions:** the home page keeps the centred welcome and adds, in the same colours: a small label above the title, "CampusMarket" in the accent colour, a soft warm glow behind the title, the five categories as chips under the button, and one panel with three highlights (suggested price, automatic category, spam sent to moderation) that describe what the API already does.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings; screenshots of the home page (light, dark, 500 px wide).
- **Follow-up:** `Remove the text under the title; the three highlights should not describe how the app works inside, only what the user gets.` The sentence under the title was removed and the highlights became user-facing: *Preț corect* (a price suggestion), *Rezervare simplă* (a colleague reserves, you confirm), *Doar între studenți* (buy and sell on campus); `HandshakeIcon` added to `Icons.tsx`.
- **Follow-up 2:** `Remove the email and the Admin badge from the top bar.` The top bar keeps only the initials (the email shows on hover) and the logout button.
- **Result:** `HomePage.tsx`, `home.css`, `Icons.tsx`, `Layout.tsx` and `layout.css` changed. No change in the API.
- **Commits:** code 5b3d6a9

### Run 6

- **Prompt:** `Change it to the style Sara asked for in her conversations and that you see in the HTML (her page sketches), for my part of the task, and it must work correctly.`
- **Decisions:**
    - Sara's sketches (a Claude canvas exported to HTML) were rendered with headless Chrome and read page by page. Her final direction: white background, coral-red accent, Figtree, rounded corners, a coral wordmark, a top bar with the links on the right, a landing page with a big title on the left and a 2×2 photo collage on the right, a login page with the form on the left and a big picture on the right. The export shows only four pages correctly (home, explore, listing, publish); register, login and profile repeat the listing page.
    - Only the parts of the sketches that belong to the skeleton and work today: the colours, the font, the top bar, the footer, the landing hero and the login layout. Left out until their pages exist: the search bar, "Vinde ceva", "Creează cont", "Ai uitat parola?", the listings grid, campus/faculty fields (not in the API).
    - The collage tiles show four of the API categories, because listings have no photos yet; the login picture is a soft coral panel for the same reason.
    - Tokens in `index.css` (light and dark), Figtree instead of Inter, buttons and inputs a bit taller and bolder as in the sketches, the secondary button outlined in dark.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings. Against the running API, in a headless Chrome driven through the DevTools protocol: wrong password → "Email sau parolă greșită.", login → home with the avatar, still logged in after reload, `/login` while logged in goes home, logout clears the tokens, unknown address → 404; all passed (one run hit the login rate limit of 5 per minute and was repeated a minute later). Screenshots of home (light, dark, logged in, 500 px) and login.
- **Result:** `index.css`, `ui.css`, `layout.css`, `Logo.tsx`, `Layout.tsx` (footer), `HomePage.tsx`, `home.css`, `LoginPage.tsx`, `login.css`, `main.tsx`, `index.html`, `favicon.svg`, `package.json` (Figtree instead of Inter) and `frontend/README.md` changed. No change in the API.
- **Commits:** code 5b4d094
