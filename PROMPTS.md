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

### Run 21, skill v23 (Feature: Condition filter)

- **Why a new version:** task 1 (issue #72). `GET /api/v1/listings` got an optional `condition` filter (`New`, `Good`, `Worn`); the new branch in `GetListingsHandler` had no tests.

#### Change to SKILL.md (v22 → v23) and why

| # | v22 (before) | v23 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the condition filter. | New section *"Feature: Condition filter (Carina)"*: the file and three tests (condition alone, condition with category, no condition). | Covers the new filter and proves that the default behaviour did not change. |
| 2 | — | *"`Condition` is the **last** parameter of `GetListingsQuery` (after `PageSize`), so existing positional calls keep their meaning; set it with a named argument"*. | Existing tests build the query by position; if the parameter were in the middle, a literal `0` meant for `Page` would silently become `Condition.New`. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Condition filter (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results, the coverage for Domain/ and the handlers, and every file you changed.` (new session)
- **Skill version:** v23, commit 1be82be; section written and run by Carina Coman
- **Result:** 3 new tests in `Features/Listings/GetListings/GetListingsHandlerTests.cs` (`Given_ListingsWithDifferentConditions_When_FilteringByCondition_Then_ReturnsOnlyThatCondition`, a test for `Category` and `Condition` together, `Given_NoConditionFilter_When_Handling_Then_ReturnsEveryActiveListing`); the 37 existing tests in the file are unchanged. Each test seeds its own `FakeListingRepository`, sets `Condition` with a named argument and checks that the repository still has its 3 items. 435/435 unit tests pass; coverage stays **100%**: 1026/1026 lines and 358/358 branches (`Domain/` 11 classes, handlers 30). The report in `coverage-report/` was generated again. Nothing changed in `src/`.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v23 is the final version.
- **Commits:** skill 1be82be; generated code ab95b6d

### Run 22, skill v24 (Feature: My profile)

- **Why a new version:** task 10 (issue #76). New slices `GetMyProfile` (`GET /api/v1/members/me`) and `UpdateMyProfile` (`PUT /api/v1/members/me`, only the name changes) without tests.

#### Change to SKILL.md (v23 → v24) and why

| # | v23 (before) | v24 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the profile of the logged-in member. | New section *"Feature: My profile (Carina)"*: what each handler does, how to build the members (`Member.Register` with `FakePasswordHasher`, Student or Admin), and 6 tests. | Without it Claude would have to guess the order of the checks and what the response contains. |
| 2 | — | *"`member.Update(command.Name, member.Email.Value)`, so only the name changes and the email stays the same"* + a test that checks the email after the update. | Proves that the profile update can never change the email. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: My profile (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results, the coverage for Domain/ and the handlers, and every file you changed.` (new session)
- **Skill version:** v24, commit f197c2c (numbered after the condition filter PR, #72, which makes v23); section written and run by Carina Coman
- **Result:** 6 new tests in two new files: `Features/Members/GetMyProfile/GetMyProfileHandlerTests.cs` (a Student with its id, name, lower-case email, role and `CreatedAt`, the repository unchanged; an Admin → role `"Admin"`; unknown id → `Member.NotFound`) and `Features/Members/UpdateMyProfile/UpdateMyProfileHandlerTests.cs` (a valid name with spaces → trimmed, email unchanged, `UpdateCount` 1, `SaveCount` 1; a 1-character name → `Member.InvalidName`, name unchanged, `UpdateCount` 0, `SaveCount` 0; unknown id → `Member.NotFound`, `SaveCount` 0). Every error checks type, code and the exact message. No existing test, fake or file in `src/` changed. 438/438 unit tests pass; coverage stays **100%**: 1045/1045 lines and 362/362 branches. The report in `coverage-report/` was generated again.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v24 is the final version.
- **Commits:** skill f197c2c; generated code b9461db

### Run 23, skill v25 (Feature: Public profile)

- **Why a new version:** task 12 (issue #78). New query slice `GetPublicProfile` (`GET /api/v1/members/{id}/profile`): name, member since, average rating, review count and Active listings, without email or role. The handler had no tests.

#### Change to SKILL.md (v24 → v25) and why

| # | v24 (before) | v25 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the public profile. | New section *"Feature: Public profile (Carina)"*: what the handler returns, how to build Draft, Active and Sold listings and a review (`Publish`, `Reserve`, `ConfirmSale`, `Review.Create`), and 6 tests. | Building a review needs a sold listing with a completed reservation; without the steps Claude would have to guess them. |
| 2 | — | *"reviews with ratings 5, 4 and 4 → `AverageRating` 4.33 (rounded to 2 decimals)"*. | Proves the rounding, not only an average that is already exact. |
| 3 | — | *"If `FakeReviewRepository` already exists in `Fakes/`, use it as it is"*. | So the run does not write a second fake for the same interface. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests (and the fake, if it is missing) from the "Feature: Public profile (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results, the coverage for Domain/ and the handlers, and every file you changed.` (new session)
- **Skill version:** v25, commit f4cb94c (numbered after the condition filter and my profile PRs, #72 and #76, which make v23 and v24); section written and run by Carina Coman
- **Result:** one new file, `Features/Members/GetPublicProfile/GetPublicProfileHandlerTests.cs`, with the 6 tests of the section: 2 Active, 1 Draft and 1 Sold listing → only the 2 Active ones, newest first, with title and price (also the member's id, name and `MemberSince`); another seller's Active listing is not included; no reviews → `AverageRating` null, `ReviewCount` 0; ratings 5 and 4 → 4.5 and 2; ratings 5, 4 and 4 → 4.33; unknown id → `Member.NotFound`. The existing `FakeReviewRepository` was used, so no fake changed. No existing test or file in `src/` changed. 438/438 unit tests pass; coverage stays **100%**: 1044/1044 lines, 362/362 branches, 167/167 methods. The report in `coverage-report/` was generated again.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v25 is the final version.
- **Commits:** skill f4cb94c; generated code 155cf71

### Skill update v25 → v26 (coverage-report is local only)

- **Numbering:** numbered after #73, #77 and #79, which make unit-testing v23, v24 and v25, so those three PRs keep their numbers.

| What | Before (v25) | After (v26) |
|---|---|---|
| Coverage report in Git (section "Coverage") | *"Commit `coverage-report/` (the HTML report is a deliverable of the assignment). Never commit `TestResults/`."* | *"The report in `coverage-report/` is only for reading locally. It is ignored by `.gitignore`: never commit it. Before committing, `git status` must not list `coverage-report/`. Never commit `TestResults/` either."* |
| Check after generating | *"`git status` must list `coverage-report/`, and `git check-ignore -v coverage-report/index.html` must print nothing."* | removed |

- **Why:** the generated HTML report changed in almost every PR (new timestamps on every class page), so PRs carried dozens of files that nobody reviews, and they conflicted at every merge. `coverage-report/` is now in `.gitignore` and removed from the repository in the same PR (`git rm -r --cached`). The old rule asked to commit the report, so it contradicted `.gitignore`, and the next run would have tried to commit it again.
- **Who:** Carina Coman, at Sara Matasel's request (edit only, no generation)
- **Commit:** `9b8d5be` (the `.gitignore` change and the removal of the folder are in the commit "chore: stop tracking coverage-report" of the same PR)

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

### Run 27, skill v29 (Feature: Change password)

- **Why a new version:** task 11 (issue #101). New `Member.ChangePassword` and the slice `Features/Auth/ChangePassword/` (`POST /api/v1/auth/change-password`); the new domain method and the handler had no tests.

#### Change to SKILL.md (v28 → v29) and why

| # | v28 (before) | v29 (after) | Why |
|---|---|---|---|
| 1 | Nothing about changing the password. | New section *"Feature: Change password (Carina)"*: the order of the checks in `ChangePasswordHandler` (404, `Auth.InvalidCredentials`, `Auth.WeakPassword`, `Auth.PasswordsDoNotMatch`), `Member.ChangePassword`, and the test list, with the 7/8-character boundary. | Without it Claude would have to guess the order of the checks and the exact messages. |
| 2 | — | *"the `changed.IsFailure` branch ... add one small private class in `ChangePasswordHandlerTests.cs` that implements `IPasswordHasher`, whose `Hash` returns `""` ... This extra hasher is allowed only for this test."* (added after the first run) | `FakePasswordHasher` never returns an empty hash, so the branch could not be reached and the handler was not at 100%. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them and only add the tests from the "Feature: Change password (Carina)" section. Follow the skill exactly, including the "Never do" list. Then run the tests with coverage, generate the HTML report as the skill says, and tell me the test results, the coverage for Domain/ and the handlers, and every file you changed. Do not commit.` (new session)
- **Skill version:** v29, commits e1cb9f3 and 9df3a7c (the second adds the private hasher rule after the first run); numbered after #77 and #79 (v24, v25), the skill update v26 and the entries on main up to v28; section written and run by Carina Coman
- **Result:** `Domain/Members/MemberTests.cs`: 2 tests added (a hash is stored and the same instance is returned; `""` and `"   "` as a `[Theory]` → `Member.PasswordRequired`, hash unchanged). New `Features/Auth/ChangePassword/ChangePasswordHandlerTests.cs`: success (new fake hash stored, the member's id returned, `UpdateCount` 1, `SaveCount` 1); wrong current password and a member without a password → `Auth.InvalidCredentials`; 7 characters and `""` (`[Theory]`) → `Auth.WeakPassword`; exactly 8 characters → success; confirmation different → `Auth.PasswordsDoNotMatch`; unknown member → `Member.NotFound`; the `changed.IsFailure` branch with a private `EmptyHashPasswordHasher` → `Member.PasswordRequired`. Every error checks type, code and the exact message, and every failure checks `SaveCount` 0. 469/469 unit tests pass; `Member` and `ChangePasswordHandler` at 100%; total for `Domain/` and the handlers 99.7% lines (1159/1162) and 99.7% branches (393/394).
- **Problems seen:**
  1. The first run (same prompt) stopped and reported that the `changed.IsFailure` branch cannot be reached with `FakePasswordHasher`. Its output was discarded, rule 2 was added to the section (commit 9df3a7c) and the run was repeated in a new session.
  2. `ConfirmSaleHandler.cs`, lines 72-75 (the `record.IsFailure` branch, from the price feedback loop, not from this feature) cannot be reached either: a listing price is always above 0. This is the only gap; reported to Sara, nothing changed in `src/`.
  3. The run regenerated `coverage-report/`; it was restored to the version on main and not committed, because generated files do not go into PRs (see the skill update v25 → v26).
- **Change to SKILL.md:** none after the second run; v29 is the final version.
- **Commits:** skill e1cb9f3, 9df3a7c; generated code a19f159

### Run 28, skill v30 (Feature: Validation thresholds)

- **Why a new version:** the minimum confidence of the category suggestion is no longer the constant `SuggestCategoryHandler.MinConfidence` (0.7) but the setting `Ml:Category:MinConfidence` (0.6, chosen on validation data), read through `IOptions<CategorySuggestionOptions>`. The handler tests no longer compiled.

#### Change to SKILL.md (v29 → v30) and why

| # | v29 (before) | v30 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the setting. | New section *"Feature: Validation thresholds (Anca)"*: build the handler with `Options.Create(new CategorySuggestionOptions { MinConfidence = 0.7 })` through one helper so the existing 0.7 tests keep their meaning; new tests for 0.6 (inclusive), 0.59 and a 0.95 setting with confidence 0.9. | Proves the handler uses the configured value and keeps the existing limit tests valid. |

- **Prompt:** `Use the unit-testing skill (read the whole file first). Unit tests already exist: keep them all. Generate the changes for the section "Feature: Validation thresholds (Anca)": update SuggestCategoryHandlerTests as the section says and add only the tests it lists. Build and test the unit test project, check the coverage of Domain/ and the handlers and the SARIF log. You may only change files under tests/CampusMarket.UnitTests/.` (new session)
- **Skill version:** v30, commit 2d54c3f (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** `SuggestCategoryHandlerTests.cs` only: a helper `CreateHandler(suggester, minConfidence = 0.7)` used by the 13 existing tests (names and assertions unchanged) and 3 new tests (0.6 setting with confidence 0.6 → category, 0.59 → null with the confidence kept, 0.95 setting with 0.9 → null), each checking one call with the trimmed title. 484 unit tests pass; `SuggestCategoryHandler` 8/8 lines and 4/4 branches; SARIF log empty.
- **Problems seen:**
    1. The section asked for "one private helper" but the new tests need other limits; the helper got a `minConfidence` parameter with default 0.7.
    2. Not from this section: `ConfirmSaleHandler` (price feedback loop, #96) lines 72–75, the `SaleRecord.Create` failure branch, are not covered by any unit test; with the current fakes it looks unreachable. Left for the owner of that feature.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 2d54c3f; generated code 31f3668

### Run 29, skill v31 (Feature: Similar listings)

- **Why a new version:** new query `GetSimilarListings` (`GET /api/v1/listings/{id}/similar`) with its handler; the handler logic (which listings are candidates, the order, the five, the rounding) had no unit test.

#### Change to SKILL.md (v30 → v31) and why

| # | v30 (before) | v31 (after) | Why |
|---|---|---|---|
| 1 | Nothing about similar listings. | New section *"Feature: Similar listings (Anca)"*: the handler rules, a new `FakeSimilarListingsFinder` (similarity per candidate title, records the calls) and 7 tests: unknown id, the target as the only Active listing, only Active candidates and never the target, the five highest, ties by `CreatedAt` then `Id`, the rounding and the item fields. | The ranking and the filtering are the handler's own logic, testable without the model. |

- **Prompt:** `Use the unit-testing skill (read the whole file first). Unit tests already exist: keep them all. Generate the tests for the section "Feature: Similar listings (Anca)": the new fake and the new test file, only the tests it lists. Build and test the unit test project, check the coverage of Domain/ and the handlers and the SARIF log. You may only change files under tests/CampusMarket.UnitTests/.` (new session)
- **Skill version:** v31, commit 7cb4df8 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new `Fakes/FakeSimilarListingsFinder.cs` and `Features/Listings/GetSimilarListings/GetSimilarListingsHandlerTests.cs` with the 7 tests; every test also checks that the repository is unchanged. 491 unit tests pass; `GetSimilarListingsHandler` 29/29 lines and 6/6 branches; SARIF log empty.
- **Problems seen:** not from this section: `ConfirmSaleHandler` lines 72–75 (#96) are still not covered, as noted in Run 28.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 7cb4df8; generated code cfb9e5a

### Run 30, skill v32 (Feature: Conversations)

- **Why a new version:** new domain `Conversations` (conversation and messages) and four new handlers (start a conversation, send a message, read a conversation, my conversations) with no unit test.

#### Change to SKILL.md (v31 → v32) and why

| # | v31 (before) | v32 (after) | Why |
|---|---|---|---|
| 1 | Nothing about conversations. | New section *"Feature: Conversations (Anca)"*: the rules of `Conversation.Start` and `Send` in their order, the four handlers, a new `FakeConversationRepository` and the tests for each (who can write and read, the message numbers, the trimmed text, the 1000-character limit, the message added to an existing conversation, the order of my conversations). | The participant check and the message rules live in the domain and the handlers; they are tested without a database. |

- **Prompt:** `Use the unit-testing skill (read the whole file first). Unit tests already exist: keep them all. Generate the tests for the section "Feature: Conversations (Anca)": the new fake and the new test files, only the tests it lists. Build and test the unit test project, check the coverage of Domain/ and the handlers and the SARIF log. You may only change files under tests/CampusMarket.UnitTests/.` (new session)
- **Skill version:** v32, commit 64131b7 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new `Fakes/FakeConversationRepository.cs`, `Domain/Conversations/ConversationTests.cs` and one test file per handler: 50 new tests, 541 unit tests pass. `Conversation.cs` and the four handlers at 100% lines and branches; SARIF log empty.
- **Problems seen:** the private constructor of `Message` (used only by EF Core when it reads a message) cannot be reached from a unit test; the integration tests cover it. `ConfirmSaleHandler` lines 72–75 (#96) still not covered, as in Run 28.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 64131b7; generated code b9400fc

### Run 31, skill v33 (Feature: Listing photos)

- **Why a new version:** new domain `Photos` (`Photo`, `PhotoFormat`), four new handlers (upload, list, get the file, delete) and `DeleteListingHandler` now takes an `IPhotoStorage`, so its tests no longer compiled.

#### Change to SKILL.md (v32 → v33) and why

| # | v32 (before) | v33 (after) | Why |
|---|---|---|---|
| 1 | Nothing about photos. | New section *"Feature: Listing photos (Anca)"*: the format detection by the first bytes, the rules of `Photo.Create` in their order (seller, 5 photos, empty, 2 MB, format), the four handlers, two new fakes (`FakePhotoRepository`, `FakePhotoStorage`) and the change to `DeleteListingHandlerTests` (the storage as third argument, the folder removed only after a successful delete). | The limits and the format check are the core of the task and are tested without a disk. |

- **Prompt:** `Use the unit-testing skill (read the whole file first). Unit tests already exist: keep them all. Generate the tests for the section "Feature: Listing photos (Anca)": the new fakes, the new test files and the change to DeleteListingHandlerTests, only what it lists. Build and test the unit test project, check the coverage of Domain/ and the handlers and the SARIF log. You may only change files under tests/CampusMarket.UnitTests/.` (new session)
- **Skill version:** v33, commit 59b3660 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** two new fakes, `PhotoFormatTests` (9), `PhotoTests` (13) and one file per handler (18); the five `DeleteListingHandlerTests` kept, now also checking the photo folder. 581 unit tests pass; `Photo`, `PhotoFormat`, the four handlers and `DeleteListingHandler` at 100% lines and branches; SARIF log empty.
- **Problems seen:** the run rewrote `DeleteListingHandlerTests.cs` with CRLF line endings; put back to LF by hand (no content change). One "order" case of the section was the same as the empty-content case, so it is one test.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 59b3660; generated code 237ab25

### Run 32, skill v34 (Feature: Sorting)

- **Why a new version:** `GetListingsQuery` has a new optional `Sort` (`newest`, `price_asc`, `price_desc`) and `GetListingsHandler` rejects any other value with `Listings.InvalidSort` and orders by price when asked. These branches had no tests.

#### Change to SKILL.md (v33 → v34) and why

| # | v33 (before) | v34 (after) | Why |
|---|---|---|---|
| 1 | Nothing about sorting. | New section *"Feature: Sorting (Diana)"*: the file (`GetListingsHandlerTests`, add to it) and the list of tests: three Active listings priced 30, 10, 20 → `price_asc` gives 10, 20, 30 and `price_desc` gives 30, 20, 10. | Prices out of order in the arrange prove that the handler sorts, not that the data was already sorted. |
| 2 | — | *"`newest` and no sort → newest `CreatedAt` first (same as before)"*; *"two listings with the same price → ordered by `Id` ascending"*. | Proves that the default did not change and that equal prices still give a stable order, so the pages do not shift. |
| 3 | — | *"`Sort = "abc"` → `Validation` `Listings.InvalidSort`, exact message"*. | The error code and the message are part of the contract of the endpoint. |

- **Prompt:** `Run the unit-testing skill for the section "Feature: Sorting (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v34, commit b5da1ba (section written and run by Diana Preutu with Claude Code)
- **Result:** 5 new test methods (7 cases) in `GetListingsHandlerTests`: price ascending and descending, `[Theory]` `newest` and `null` → newest first, `[Theory]` `price_asc` and `price_desc` with equal prices → `Id` ascending, and `Sort = "abc"` → `Validation`, `Listings.InvalidSort`, the exact message `Sort must be newest, price_asc or price_desc.`, repository unchanged. Every test checks the repository. A small private helper (`RepositoryWithActiveListings`) publishes and seeds the listings. No existing test and nothing in `src/` changed by the run. 442/442 unit tests pass. The coverage report was not generated again in this run (it is not part of the PR).
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v28 is the final version.
- **Commits:** skill b5da1ba; generated code 2d27370

### Run 33, skill v35 (Feature: Notifications)

- **Why a new version:** Sara's notifications (#111): new domain `Notification`, three handlers (my notifications, mark one as read, mark all as read) and a notification written by `ReserveListingHandler`, `ConfirmSaleHandler` and `ExpireReservationsHandler`, with no unit test.

#### Change to SKILL.md (v34 → v35) and why

| # | v34 (before) | v35 (after) | Why |
|---|---|---|---|
| 1 | Nothing about notifications. | Section *"Feature: Notifications (Sara)"* (written by Sara, commit 155811a): the domain rules, the three handlers, a `FakeNotificationRepository`, and the notification checks to add to the reserve, confirm-sale and expiry handler tests. | The notification is written in the same unit of work as the action; only the handler tests show it is (or is not) added. |

- **Prompt:** `Use the unit-testing skill in .claude/skills/unit-testing/SKILL.md. Unit tests already exist: keep them, do not change them, only add the tests from the "Feature: Notifications (Sara)" section. Run dotnet test. Do not commit.` (new session, run by Ancuța Ilenei at Sara's request)
- **Skill version:** v35, commit 155811a
- **Result:** `Fakes/FakeNotificationRepository.cs`, `Domain/Notifications/NotificationTests.cs`, one test file per notification handler, and notification checks added to `ReserveListingHandlerTests`, `ConfirmSaleHandlerTests` and `ExpireReservationsHandlerTests`. After the merge with main: 654 unit tests pass.
- **Problems seen:** none.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 155811a; generated code 4d5a11a

### Run 34, skill v36 (Feature: Full coverage)

- **Why a new version:** issue #121 (100% coverage in SonarQube, every mutant killed). The `changeResult.IsFailure` branch of `ResetPasswordHandler` (lines 48–49) was not reached by any unit test: `FakePasswordHasher` never returns an empty hash, so `Member.ChangePassword` never failed.

#### Change to SKILL.md (v35 → v36) and why

| # | v35 (before) | v36 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the `Member.ChangePassword` failure in `ResetPasswordHandler`. | New section *"Feature: Full coverage (Carina)"*: a private `EmptyHashPasswordHasher` in `ResetPasswordHandlerTests.cs` (`Hash` returns `""`, `Verify` returns `true`, the same rule as in `ChangePasswordHandlerTests`), the handler built directly (the `BuildHandler` helper takes a `FakePasswordHasher` and stays unchanged), and one test: `Member.PasswordRequired` with the exact message, hash unchanged, reset token not used, refresh token not revoked, no `Update` on any repository, `SaveCount` 0, no email. | The handler returns before `MarkUsed`, the refresh revocation, the `Update` calls, `CommitAsync` and the email; the test checks exactly that, so the branch is covered and its early return is asserted. |

- **Prompt:** `/unit-testing Task: issue #121 "Coverage 100% in SonarQube and all mutants killed". Scope for this run: ResetPasswordHandler only. 1. First update .claude/skills/unit-testing/SKILL.md: add a section "Feature: Full coverage (Carina)" and bump the version from v32 to v33. The section must say: ResetPasswordHandler must also be tested when Member.ChangePassword fails (a password hasher that returns an empty hash, like the existing EmptyHashPasswordHasher rule) -> Validation failure Member.PasswordRequired; assert what the handler actually does in this case (check the code: token not marked as used, no commit, etc.). Commit only SKILL.md with the message "skills: unit-testing full coverage section (v33) (#121)". 2. Following the skill, write the unit tests for this case. Run the unit tests and coverage exactly as the skill says. ResetPasswordHandler.cs must have 100% line and branch coverage. 3. Add Run 31 to PROMPTS.md under unit-testing, in exactly the same format as Run 30 (skill version v32 -> v33, change table, prompt, result, problems, commit hashes, no TODO). 4. Restore coverage-report and delete TestResults so they are not committed. Then commit only the test files and PROMPTS.md with the message "test: unit tests for reset password empty hash, Run 31 (#121)". 5. Show me git log --oneline -4, git status, and the coverage of ResetPasswordHandler.cs.`
- **Skill version:** v36, commit 3859f09 (section written by Carina with Claude Code; renumbered from v33 when main was merged, because #120 (listing photos) makes v33; renumbered again from v34 when main was merged a second time, because main's Runs 32 and 33 (Sorting, Diana; Notifications, #111) make v34 and v35)
- **Result:** one new test in `Features/Auth/ResetPassword/ResetPasswordHandlerTests.cs`, `Given_HasherReturnsEmptyHash_When_Handling_Then_ReturnsPasswordRequiredAndChangesNothing`, with a private `EmptyHashPasswordHasher`; the existing tests and the `BuildHandler` helper are unchanged. 566 unit tests pass; `ResetPasswordHandler` 42/42 lines and 18/18 branches (100%).
- **Problems seen:**
    1. `SKILL.md` has no version line: the version lives only in this file, so the bump to v36 is the new section plus this entry.
    2. Not from this section: the sections *"Feature: Similar listings (Anca)"* and *"Feature: Conversations (Anca)"* (v31 and v32, commits 7cb4df8 and 64131b7) are no longer in `SKILL.md`; they were dropped in the merge a5e2569. Left for the owner of those sections.
    3. The `ConfirmSaleHandler` lines 72–75 (#96) noted in Runs 28–30 were removed from the production code in commit d000b76 (#121), so the gap is gone.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 3859f09; generated code and this entry in the same commit (the one that changes `ResetPasswordHandlerTests.cs`)

### Run 35, skill v37 (Feature: Full coverage, surviving mutants)

- **Why a new version:** issue #121 (100% coverage in SonarQube, every mutant killed). Stryker in `tests/CampusMarket.UnitTests` ended at 97.83% (812 killed, 18 survived): email bodies checked only with `Contain`, missing `Update` calls, `< limit` boundaries, `OrderBy`/`ThenBy` tie-breakers, `[LoggerMessage]` calls hidden by `NullLogger`, and two equivalent mutants (`Message.cs`, `Review.cs`). The section *"Feature: Surviving mutants (Diana)"* also said production code never changes, which contradicted marking the new equivalent mutants.

#### Change to SKILL.md (v36 → v37) and why

| # | v36 (before) | v37 (after) | Why |
|---|---|---|---|
| 1 | *"Feature: Full coverage (Carina)"* covers only the empty-hash branch of `ResetPasswordHandler`. | New subsection *"Surviving mutants of issue #121"* with six rules: equivalent mutants get `// Stryker disable once <Mutator> : <reason>` in `src/` (new ones: `Message.cs` `Text = string.Empty`, `Review.cs` `First` vs `FirstOrDefault`); a test at exactly the limit for every `< limit` check (`ResetPasswordHandler` 8 characters, `PageSize` 1 in `GetReportedListingsHandler` and `GetMemberReviewsHandler`); exact ordering tests with ties (same `SoldAt` in `PriceAccuracyHandler`, same `CreatedAt` in `GetPublicProfileHandler`, and the `PerCategory` order); `UpdateCount` on every fake the handler updates; emails asserted on the full exact `Body`; `[LoggerMessage]` logs asserted with a hand-written `FakeLogger<T>` (`IsEnabled` true, `Log` stores level and message). | Each rule kills one family of the 18 surviving mutants; written as general rules so the next handler is tested the same way. |
| 2 | *"Feature: Surviving mutants (Diana)"*: "Production code does not change", point 6 lists only `Listing.cs` and `Member.cs`, rule "Never change production code". | The same section allows the `// Stryker disable once` comment for a mutant no test can kill and points to the new equivalent mutants in *"Feature: Full coverage (Carina)"*. | The two sections no longer contradict each other. |

- **Prompt:** `/unit-testing Task: issue #121 "Coverage 100% in SonarQube and all mutants killed". Scope for this run: the 18 surviving mutants from the Stryker report you just analysed (#1-#18). 1. Update .claude/skills/unit-testing/SKILL.md: extend the section "Feature: Full coverage (Carina)" with the rules needed for these mutants, and update "Feature: Surviving mutants" so it no longer contradicts them: - equivalent mutants (only when no test can kill them) get a "// Stryker disable once <Mutator> : <reason>" comment in src, like Listing.cs and Member.cs; list Message.cs (Text = string.Empty) and Review.cs (First vs FirstOrDefault, Sold listing always has a Completed reservation) as the new equivalent mutants; - boundary tests for "< limit" checks (exact limit must succeed); - exact ordering tests with ties (same SoldAt / same CreatedAt) for OrderBy/ThenBy; - success tests assert UpdateCount on every fake repository that the handler updates; - emails are asserted on the full exact Body, not only Contain; - logs from [LoggerMessage] are asserted with a hand-written FakeLogger<T> (IsEnabled returns true, Log stores level and message). This is skill version v34 (recorded only in PROMPTS.md). Commit only SKILL.md with the message "skills: unit-testing surviving mutants rules (v34) (#121)". 2. Add only the two "// Stryker disable once" comments in src/CampusMarket.Api/Domain/Conversations/Message.cs and src/CampusMarket.Api/Domain/Reviews/Review.cs. No other change in src. Commit only these two files with the message "chore: mark equivalent mutants in Message and Review (#121)". 3. Following the skill, write the tests that kill mutants #3-#18. Run the unit tests, then run dotnet stryker in tests/CampusMarket.UnitTests. The final mutation score must be 100%. If a mutant still survives, stop and tell me why instead of forcing it. 4. Run dotnet build and dotnet test for the whole solution with coverage like the CI does: everything must pass and the 7 files from Run 42 must still be at 100% line and branch coverage. 5. Add Run 32 to PROMPTS.md under unit-testing, in exactly the same format as Run 31 (v33 -> v34, change table, prompt, result, problems, commit hashes, no TODO). Mention the Stryker score before (97.83%) and after. 6. Restore coverage-report, delete TestResults and make sure StrykerOutput/mutation-report are not committed. Commit only the test files and PROMPTS.md with the message "test: kill surviving mutants, Run 32 (#121)". 7. Show me git log --oneline -9, git status, and the final Stryker score line.`
- **Skill version:** v37, commit 4a22915 (section written by Carina with Claude Code; renumbered from v34 when main was merged, because #120 (listing photos) makes v33; renumbered again from v35 when main was merged a second time, because main's Runs 32 and 33 (Sorting, Diana; Notifications, #111) make v34 and v35); the two `// Stryker disable once` comments in commit 481d4ae
- **Result:** new `Fakes/FakeLogger.cs`; 9 new tests: `PriceAccuracyHandlerTests` (2: `PerCategory` order, same `SoldAt` ordered by `Id` descending), `ResetPasswordHandlerTests` (1: new password of exactly `RegisterHandler.MinPasswordLength`), `GetPublicProfileHandlerTests` (1: same `CreatedAt` ordered by `Id`), `GetReportedListingsHandlerTests` and `GetMemberReviewsHandlerTests` (1 each: `PageSize` 1), `ConfirmSaleHandlerTests` (3: the two warnings with their exact messages, and no log on a positive suggestion). Assertions added to two existing tests, names unchanged: the full `Body` in the success tests of `ForgotPasswordHandler` and `ResetPasswordHandler`, and `UpdateCount` 1 on `members`, `tokens` and `refreshTokens` in the `ResetPasswordHandler` one. 575 unit tests pass. Stryker: **97.83% before → 100.00% after** (1172 mutants: 828 killed, 0 survived, 47 compile errors, 297 ignored, including the two new equivalent mutants). Full run with coverage as in the CI: build 0 warnings; 10 architecture, 575 unit, 69 ML, 328 integration tests pass. The 7 files from Run 45 of integration-testing stay at 100% lines and branches (`PriceAccuracyEndpoint.cs` 9/9, `EfSaleRecordRepository.cs` 3/3, `UpdateMyProfileEndpoint.cs` 13/13 and 4/4, `CampusMarketSetup.cs` 200/200 and 54/54, `SmtpEmailSender.cs` 18/18 and 2/2, `ConfirmSaleHandler.cs` 43/43 and 10/10, `ResetPasswordHandler.cs` 42/42 and 18/18).
- **Problems seen:**
    1. The report was not in `tests/CampusMarket.UnitTests/mutation-report/` as the analysis prompt said, but in `StrykerOutput/<date>/reports/mutation-report.json` (ignored by `.gitignore`). The tracked `mutation-report/mutation-report.html` at the repository root was left unchanged.
    2. Domain/ and the handlers from the unit tests alone: 1507/1508 lines and 480/480 branches. The missing line is the private constructor of `Message`, used only by EF Core, as already noted in Run 30; the integration tests cover it.
    3. `SKILL.md` has no version line: the version lives only in this file, as in Run 34.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 4a22915; `src/` comments 481d4ae; generated code and this entry in the same commit (the one that adds `Fakes/FakeLogger.cs`)

### Run 36, skill v38 (Feature: Full coverage, photo mutants)

- **Why a new version:** issue #121 (100% coverage in SonarQube, every mutant killed). After the merge of main (commit 1438856), which brought the listing photos code of #120 (commit f4520ff, Ancuța Ilenei), Stryker in `tests/CampusMarket.UnitTests` ended at 99.67% (894 killed, 3 survived): two equivalent `String` mutants in `Photo.cs` (`FileName = string.Empty`, `ContentType = string.Empty`) and the WebP boundary in `PhotoFormat.cs` (`content.Length >= 12` → `> 12`), because every WebP test image was longer than 12 bytes.

#### Change to SKILL.md (v37 → v38) and why

| # | v37 (before) | v38 (after) | Why |
|---|---|---|---|
| 1 | *"Surviving mutants of issue #121"* lists `Message.cs` and `Review.cs` as the new equivalent mutants. | Also `Domain/Photos/Photo.cs`, `FileName` and `ContentType` defaults: `Photo.Create` always sets both and EF Core overwrites them, one comment above each property. Point 6 of *"Feature: Surviving mutants (Diana)"* names them too. | No test can see the default value, so the comment is the only way to reach 100%; point 6 lists every later equivalent mutant, so it stays complete. |
| 2 | The boundary rule has examples only for `ResetPasswordHandler` and `PageSize` 1. | New example: `PhotoFormat.Detect`, a WebP of exactly 12 bytes (`"RIFF"`, 4 size bytes, `"WEBP"`) → `PhotoFormat.WebP`, test `Given_WebPOfExactly12Bytes_When_Detecting_Then_ReturnsWebP` in `Domain/Photos/PhotoFormatTests.cs`. | Kills `>=` → `>`; the existing 16-byte WebP image passes with both. |

- **Prompt:** `Part A - finish the merge: Delete TestResults, restore coverage-report if it changed, git add only the three conflicted files (.claude/skills/integration-testing/SKILL.md, .claude/skills/unit-testing/SKILL.md, PROMPTS.md) and commit the merge with the default merge message. Make sure StrykerOutput is not committed. Part B - /unit-testing Task: issue #121 "Coverage 100% in SonarQube and all mutants killed". Scope for this run: the 3 surviving mutants after the merge (from mutanti2.txt). 1. Update .claude/skills/unit-testing/SKILL.md, in my section about surviving mutants: add Photo.cs (FileName and ContentType defaults) to the list of equivalent mutants, and add the PhotoFormat WebP 12-byte boundary as an example of the boundary rule. This is skill version v36 (recorded only in PROMPTS.md). Commit only SKILL.md with the message "skills: unit-testing photo mutants (v36) (#121)". 2. Add only the two "// Stryker disable once String : ..." comments in src/CampusMarket.Api/Domain/Photos/Photo.cs (lines 15 and 16), with the reasons from mutanti2.txt. No other change in src. Commit only Photo.cs with the message "chore: mark equivalent mutants in Photo (#121)". 3. Following the skill, add the test Given_WebPOfExactly12Bytes_When_Detecting_Then_ReturnsWebP in tests/CampusMarket.UnitTests/Domain/Photos/PhotoFormatTests.cs. Run the unit tests, then dotnet stryker in tests/CampusMarket.UnitTests: the final score must be 100%. If anything still survives, stop and tell me. 4. Run dotnet build and dotnet test for the whole solution with coverage like the CI does: everything must pass and the 7 files from issue #121 must still be at 100%. 5. Add Run 34 to PROMPTS.md under unit-testing, in the same format as Run 33 (v35 -> v36, change table, prompt, result, problems, commit hashes, no TODO). Mention the score before (99.67%) and after. 6. Delete TestResults, restore coverage-report, do not commit StrykerOutput. Commit only the test file and PROMPTS.md with the message "test: kill photo mutants, Run 34 (#121)". 7. Show me git log --oneline -6, git status and the final Stryker score line.`
- **Skill version:** v38, commit 498b98a (section written by Carina with Claude Code; renumbered again from v36 when main was merged a second time, because main's Runs 32 and 33 (Sorting, Diana; Notifications, #111) make v34 and v35); the two `// Stryker disable once` comments in commit 4dee8ec
- **Result:** one new test in `Domain/Photos/PhotoFormatTests.cs`, `Given_WebPOfExactly12Bytes_When_Detecting_Then_ReturnsWebP`, with a new `static readonly` 12-byte WebP array; the existing tests are unchanged. 616 unit tests pass. Stryker: **99.67% before → 100.00% after** (1255 mutants in the report: 895 killed, 0 survived, 52 compile errors, 308 ignored, including the two new equivalent mutants of `Photo.cs`). Full run with coverage as in the CI: build 0 warnings; 10 architecture, 616 unit, 69 ML, 357 integration tests pass. The 7 files from Run 45 of integration-testing stay at 100% lines and branches (`PriceAccuracyEndpoint.cs` 9/9, `EfSaleRecordRepository.cs` 3/3, `UpdateMyProfileEndpoint.cs` 13/13 and 4/4, `CampusMarketSetup.cs` 204/204 and 54/54, `SmtpEmailSender.cs` 18/18 and 2/2, `ConfirmSaleHandler.cs` 43/43 and 10/10, `ResetPasswordHandler.cs` 42/42 and 18/18); `Photo.cs` 35/35 and 10/10, `PhotoFormat.cs` 17/17 and 10/10.
- **Problems seen:**
    1. `CampusMarketSetup.cs` has 204 lines instead of 200 since the merge: main added the photo registrations; they are covered.
    2. Stryker Safe Mode removes every mutation in `UploadPhotoEndpoint.ReadAtMostAsync` (a mutant gives CS0165, "Use of unassigned local variable 'read'") and marks them as compile errors; not a surviving mutant, so left as it is. `UploadPhotoEndpoint.cs` is covered by the integration tests (29/29 lines, 14/14 branches).
    3. `SKILL.md` has no version line: the version lives only in this file, as in Run 34.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 498b98a; `src/` comments 4dee8ec; generated code and this entry in the same commit (the one that changes `PhotoFormatTests.cs`)

### Run 37, skill v39 (Feature: Full coverage, equivalent mutants after the merge)

- **Why a new version:** issue #121 (100% coverage in SonarQube, every mutant killed). After the merge of main (commit 931aefb), which brought Sara's notifications (#111, commit a7b6f90) and Diana's sorting (commit 42998f7), Stryker in `tests/CampusMarket.UnitTests` ended at 99.79% (973 killed, 2 survived): `ExpireReservationsHandler.cs:26` (Logical, `&&` → `||` in `r.Status == ReservationStatus.Active && r.IsExpired(now)`) and `GetListingsHandler.cs:43` (String, `query.Sort ?? "newest"` → `?? ""`). Both were checked first for an input that behaves differently; there is none, so both are equivalent.

#### Change to SKILL.md (v38 → v39) and why

| # | v38 (before) | v39 (after) | Why |
|---|---|---|---|
| 1 | *"Surviving mutants of issue #121"* lists `Message.cs`, `Review.cs` and `Photo.cs` as the new equivalent mutants. | Also `ExpireReservationsHandler.cs` (`&&` → `||`: `Reservation.IsExpired` already requires `Status == Active`, a listing has at most one `Active` reservation, and the notification is written only when that one expired) and `GetListingsHandler.cs` (the `"newest"` fallback: the switch has no `"newest"` arm, so `null`, `"newest"` and `""` all reach the default arm). Point 6 of *"Feature: Surviving mutants (Diana)"* names them too. | No test can see either change, so the comment is the only way to reach 100%; point 6 lists every later equivalent mutant, so it stays complete. |
| 2 | The rule for equivalent mutants says to mark them only when no test can kill them. | New line: before marking, look for an input that behaves differently with the mutation (other statuses, older reservations, ties, the empty value); if one exists, write that test instead. | Makes the check done in this run part of the rule, so a killable mutant is never hidden by a comment. |

- **Prompt:** `Part A - finish the merge now: Delete TestResults, restore coverage-report if it changed, git add only the four resolved files (.claude/skills/integration-testing/SKILL.md, .claude/skills/unit-testing/SKILL.md, PROMPTS.md, tests/CampusMarket.UnitTests/Features/Listings/ConfirmSale/ConfirmSaleHandlerTests.cs) and commit the merge with the default merge message. Do not commit StrykerOutput. Part B - /unit-testing Task: issue #121 "Coverage 100% in SonarQube and all mutants killed". Scope for this run: the 2 surviving mutants after the merge (ExpireReservationsHandler.cs:26 && -> ||, GetListingsHandler.cs:43 "newest" -> ""). 1. For each one, first double-check that it is really equivalent: try to find any input or test that behaves differently with the mutation. If one exists, write that test instead and do not mark it. Only if no test can kill it, mark it as equivalent. 2. Update .claude/skills/unit-testing/SKILL.md: add the equivalent ones to the list of equivalent mutants in my section and in point 6 of "Feature: Surviving mutants (Diana)", with the reason. Use the next free skill version after the last one in PROMPTS.md (v39 if the last one is v38). Commit only SKILL.md with the message "skills: unit-testing equivalent mutants after merge (#121)". 3. Add only "// Stryker disable once <Mutator> : <reason>" comments on the line above each equivalent mutant in src. No other change in src, the behaviour of Sara's and Diana's code must stay exactly the same. Commit only those src files with the message "chore: mark equivalent mutants in ExpireReservations and GetListings (#121)". If step 1 produced a test, commit it separately with the message "test: kill mutant after merge (#121)". 4. Run dotnet stryker in tests/CampusMarket.UnitTests: the final score must be 100%. Run dotnet build and dotnet test for the whole solution with coverage like the CI does: everything must pass and the 7 files from #121 must be at 100%. 5. Add the next unit-testing Run to PROMPTS.md (Run 37 if the last one is Run 36), in the same format as the previous Run (version change, change table, prompt, result, problems, commit hashes, no TODO), with the Stryker score before (99.79%) and after. Delete TestResults, restore coverage-report, do not commit StrykerOutput. Commit only PROMPTS.md with the message "docs: PROMPTS unit-testing Run for equivalent mutants after merge (#121)". 6. Show me git log --oneline -6, git status, the final Stryker score line, and the final list of my Run numbers and versions in PROMPTS.md (unit-testing and integration-testing).`
- **Skill version:** v39, commit 687cbb6 (section written by Carina with Claude Code); the two `// Stryker disable once` comments in commit a39e53e
- **Result:** no new test (step 1 found no input that kills either mutant). Two comments in `src/`, behaviour unchanged: `// Stryker disable once Logical : ...` above line 26 of `ExpireReservationsHandler.cs`, and `// Stryker disable once String : ...` in `GetListingsHandler.cs`, attached to the `query.Sort ?? "newest"` expression. 672 unit tests pass. Stryker: **99.79% before → 100.00% after** (1350 mutants in the report: 973 killed, 0 survived, 57 compile errors, 320 ignored, including the two new equivalent mutants). Full run with coverage as in the CI: build 0 warnings; 10 architecture, 672 unit, 69 ML, 374 integration tests pass. The 7 files from Run 45 of integration-testing stay at 100% lines and branches (`PriceAccuracyEndpoint.cs` 9/9, `EfSaleRecordRepository.cs` 3/3, `UpdateMyProfileEndpoint.cs` 13/13 and 4/4, `CampusMarketSetup.cs` 206/206 and 54/54, `SmtpEmailSender.cs` 18/18 and 2/2, `ConfirmSaleHandler.cs` 56/56 and 16/16, `ResetPasswordHandler.cs` 42/42 and 18/18); `ExpireReservationsHandler.cs` 33/33 and 12/12, `GetListingsHandler.cs` 45/45 and 28/28. Every new or changed `src/` file from main is at 100%.
- **Problems seen:**
    1. With the comment on the line above the whole `var ordered = (query.Sort ?? "newest") switch { ... }` statement, `disable once` covered the entire switch: Stryker also ignored the `"price_asc"` and `"price_desc"` String mutants (3 ignored instead of 1, 971 tested), which the sorting tests kill. The comment was moved inside the parentheses, onto the `query.Sort ?? "newest"` expression (a line break, no other change); then exactly 1 mutant is ignored and 973 are tested and killed.
    2. `ConfirmSaleHandler.cs` has 56 lines and 16 branches instead of 43 and 10, and `CampusMarketSetup.cs` 206 instead of 204: main added the notification code and registrations; they are covered.
    3. `SKILL.md` has no version line: the version lives only in this file, as in Run 34.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 687cbb6; `src/` comments a39e53e; this entry in its own commit (the one that changes only `PROMPTS.md`)

### Run 38, skill v40 (Feature: Admin categories)

- **Why a new version:** branch `feature/admin-categories`. New admin-managed categories: `Domain/Categories/` (`ListingCategory`, `ICategoryRepository`), the slices `GetCategories` and `UpdateCategory`, and a new last constructor parameter `ICategoryRepository` in `CreateListingHandler`, `UpdateListingHandler` and `PublishListingHandler` (a deactivated category takes no new, moved or published listings, `Listing.CategoryInactive`). The existing tests of the three listing handlers no longer compiled and the new code had no unit tests.

#### Change to SKILL.md (v39 → v40) and why

| # | v39 (before) | v40 (after) | Why |
|---|---|---|---|
| 1 | Nothing about categories managed by an Admin. | New section *"Feature: Admin categories (Carina)"*: what `ListingCategory`, `GetCategoriesHandler`, `UpdateCategoryHandler` and the three listing handlers do, in which order, and the tests to add. | Without it Claude would have to guess the order of the checks, the "last active" rule and when the listing handlers look at the category. |
| 2 | — | New fake `FakeCategoryRepository`: all 5 categories active by default (with the seed display names), `Deactivate(key)` to make one inactive, `Items.Remove(key)` for the "not in the repository" branches, `UpdateCount`. | Every listing test starts from the normal state (all active) and a test switches off exactly the category it needs, without going through the domain `Update`. |
| 3 | — | *"Only the constructor calls change: every existing publish, create and edit test keeps all its assertions."* | The existing tests must keep testing what they tested before; only the new dependency is passed. |

- **Prompt:** `/unit-testing Task: admin-managed categories, branch feature/admin-categories. Rules: do NOT commit, do NOT push, no Co-Authored-By. Do not change any file in src/ or frontend/. 1. Update .claude/skills/unit-testing/SKILL.md: add a section "Feature: Admin categories (Carina)" with the rules for this feature (FakeCategoryRepository with all 5 categories active by default and a way to deactivate one; existing publish/create/edit tests keep all their assertions, only their constructor calls change). 2. Fix the existing unit tests that no longer compile: only add the FakeCategoryRepository to the constructor calls of CreateListingHandler, UpdateListingHandler and PublishListingHandler. Do not change or remove any existing assertion or test. 3. Write the unit tests, following the skill: - Category domain: change display name and description; empty display name -> Category.DisplayNameEmpty; too long display name -> Category.DisplayNameTooLong; too long description -> Category.DescriptionTooLong; deactivate and reactivate; the last active rule -> Category.LastActive. - Update category handler: unknown category -> Category.NotFound; missing isActive -> Category.IsActiveRequired; empty name -> Category.DisplayNameEmpty; deactivating the last active one -> Category.LastActive and nothing saved; success -> values changed, Update and commit called once. - Get categories handler: returns the 5 categories with their values. - CreateListingHandler: inactive category -> Listing.CategoryInactive and nothing saved. - UpdateListingHandler: moving a listing into an inactive category -> Listing.CategoryInactive and nothing saved; editing a listing that is already in an inactive category without changing the category -> assert what the handler actually does. - PublishListingHandler: draft in an inactive category -> Listing.CategoryInactive and nothing saved. 4. Run the unit tests and the coverage exactly as the skill says: all unit tests pass and every new or changed file in Domain/Categories, Features/Categories and the 3 listing handlers is at 100% line and branch coverage. Then run dotnet build for the whole solution: 0 warnings (analyzer clean). 5. Add the next unit-testing Run to PROMPTS.md: the next free Run number after the last unit-testing Run in PROMPTS.md, and the next skill version after the last one recorded, in the same format as the previous Runs. Write "commit hashes: to be filled in" for now, I will commit myself later. Tell me the Run number and version you used. 6. Restore coverage-report and delete TestResults. 7. Show me: the tests added, the coverage of the new files, the build result and git status. Answer in Romanian.` (new session)
- **Skill version:** v40 (section written by Carina with Claude Code; the last unit-testing version was v39, Run 37)
- **Result:** new `Fakes/FakeCategoryRepository.cs`. In `CreateListingHandlerTests`, `UpdateListingHandlerTests` and `PublishListingHandlerTests` only the constructor calls changed (`new FakeCategoryRepository()` as the last argument); no existing test or assertion changed. 39 new test methods (45 cases):
    - `Domain/Categories/ListingCategoryTests.cs` (new, 23 methods, 28 cases): `Create` valid (trimmed, active by default), inactive, blank description → `null`, undefined key → `Category.NotFound`, empty display name (`null`, `""`, `"   "`) → `Category.DisplayNameEmpty`, exactly 100 characters (also with spaces around) → success, 101 → `Category.DisplayNameTooLong`, 500 → success, 501 → `Category.DescriptionTooLong`; `Update` changes name and description (trimmed, same instance), `null` description, each text error leaves the category unchanged, deactivate, reactivate, last active → Conflict `Category.LastActive` (unchanged), last active kept active, inactive kept inactive; `CheckAcceptsListings` active → `null`, inactive → `Listing.CategoryInactive`; `NotFound`.
    - `Features/Categories/UpdateCategory/UpdateCategoryHandlerTests.cs` (new, 6 methods, 7 cases): success with a lower-case key (values changed, `UpdateCount` 1, `SaveCount` 1); unknown key `"Books"` and `"1"` → `Category.NotFound`; a key removed from the fake → `Category.NotFound`; `IsActive` null → `Category.IsActiveRequired`; empty name → `Category.DisplayNameEmpty`; the last active one → `Category.LastActive`, nothing saved.
    - `Features/Categories/GetCategories/GetCategoriesHandlerTests.cs` (new, 2): the 5 categories in key order with their display names, descriptions and active flags, `Count` 5; an inactive one comes back with `IsActive` false; the fake unchanged.
    - `CreateListingHandlerTests` (+2): inactive category → `Listing.CategoryInactive`, nothing stored, `SaveCount` 0; undefined category → `Listing.CategoryInvalid` (the branch where the category is not in the repository).
    - `UpdateListingHandlerTests` (+4): moving into an inactive category → `Listing.CategoryInactive`, listing unchanged, `UpdateCount` 0, `SaveCount` 0; moving into an active category → success; a listing already in an inactive category edited without changing the category → **success**, the change is saved (`UpdateCount` 1, `SaveCount` 1), as the handler only checks when the category changes; moving to an undefined category → `Listing.CategoryInvalid`.
    - `PublishListingHandlerTests` (+2): a draft in an inactive category → `Listing.CategoryInactive`, still `Draft`, `UpdateCount` 0, `SaveCount` 0, spam detector not called, no report; a draft whose category is not in the fake → published as before.
  720/720 unit tests pass. Coverage (unit tests only, report filter of the skill): `ListingCategory.cs` 45/45 lines and 24/24 branches, `UpdateCategoryHandler.cs` 21/21 and 10/10, `GetCategoriesHandler.cs` 8/8 (no branches), `CreateListingHandler.cs` 23/23 and 8/8, `UpdateListingHandler.cs` 27/27 and 14/14, `PublishListingHandler.cs` 32/32 and 16/16; the whole report 1855/1856 lines and 618/618 branches. `dotnet build` of the solution: 0 warnings, 0 errors.
- **Problems seen:**
    1. The only uncovered line in the report is still the private constructor of `Message` (EF Core only), as in Run 35; covered by the integration tests.
    2. The `category?.` null branches of the three listing handlers can only be reached when the category is not in the repository: for Create and Update with an undefined enum value (then `Listing.CategoryInvalid`), for Publish only by removing the key from the fake, because a stored draft always has a defined category.
    3. `SKILL.md` has no version line: the version lives only in this file, as in Run 34.
    4. Stryker found 1 surviving mutant (redundant null check in `UpdateCategoryHandler`), fixed by removing the ternary, score back to 100%.
- **Change to SKILL.md after the run:** none.
- **Commits:** 0865eb0 (feat), 236c00d (skills), 89c2b88 (tests)

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

### Run 23, skill v22 (Feature: Condition filter)

- **Why a new version:** task 1 (issue #72). The new `condition` query parameter has to be tested over HTTP, including the 400 that ASP.NET Core returns for an unknown value.

#### Change to SKILL.md (v21 → v22) and why

| # | v21 (before) | v22 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the condition filter. | New section *"Feature: Condition filter (Carina)"*: the arrange (published `Good` and `Worn` listings with fixed `CreatedAt`) and two tests. | The endpoint has a new parameter, so Claude needs the contract to test it. |
| 2 | — | *"`?condition=Banana` → 400. ASP.NET Core rejects it before the handler runs, so check only the status code."* | There is no handler `Error` to read for this case. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Condition filter (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v22, commit c3861b4; section written and run by Carina Coman
- **Result:** 2 new tests in `Listings/GetListingsTests.cs`: `?condition=Good` → 200 with only the Good listing (`totalCount` 1, `count` 1, its id and condition checked); `?condition=Banana` → 400, status only. Both use a private helper `SeedGoodAndWornListingsAsync`, which publishes the listings before `SeedAsync`, and check that the database still has the 2 listings. Existing tests unchanged, nothing changed in `src/`. `dotnet build` 0 warnings, 0 errors (no CA or IDE diagnostics in the SARIF log); integration tests 221/221 three runs in a row; full run: 10 architecture, 435 unit, 13 ML, 221 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v22 is the final version.
- **Commits:** skill c3861b4; generated code 8c18e23

### Run 24, skill v23 (Feature: API documentation)

- **Why a new version:** task 8 (issue #74). The API got an OpenAPI document (`/openapi/v1.json`) and a documentation page (`/scalar`), both only in Development. The new endpoint had no test.

#### Change to SKILL.md (v22 → v23) and why

| # | v22 (before) | v23 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the API documentation. | New section *"Feature: API documentation (Carina)"*: the production code (`AddOpenApi`, and `MapOpenApi` and `MapScalarApiReference` only in Development; the test host runs in Development), the new file and one test. | The document is a new endpoint, and Claude needs to know that it exists only in Development. |
| 2 | Every test checks the database (section 4). | *"The database has nothing to do with this endpoint: at the end, check that it is still empty (0 listings), as the 404 and 405 tests of 'Feature: Error handling (Anca)' do."* | Keeps the status, body and database checks without inventing data. |
| 3 | — | *"Read the body with `ReadAsStringAsync`; do not deserialize the OpenAPI document."* | The test only has to prove that the paths are listed; a typed OpenAPI model would need a new package. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the test from the "Feature: API documentation (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v23, commit 0e90f1e (numbered after the condition filter PR, #72, which makes v22); section written and run by Carina Coman
- **Result:** new file `Infrastructure/OpenApiTests.cs`, class `OpenApiTests : IntegrationTestBase`, with one test, `Given_DevelopmentEnvironment_When_GettingOpenApiDocument_Then_Returns200WithApiPaths`: `GET /openapi/v1.json` → 200, content type `application/json`, the body (read as text) contains `/api/v1/listings` and `/api/v1/auth/login`, and the database still has 0 listings. No existing test changed, nothing changed in `src/`. `dotnet build` 0 warnings, 0 errors (built with the SARIF error log, no CA or IDE diagnostics); integration tests 220/220 three runs in a row; full run: 10 architecture, 432 unit, 13 ML, 220 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v23 is the final version.
- **Commits:** skill 0e90f1e; generated code 5cd4f6c

### Run 25, skill v24 (Feature: My profile)

- **Why a new version:** task 10 (issue #76). `GET /api/v1/members/me` and `PUT /api/v1/members/me` are new endpoints that need a token.

#### Change to SKILL.md (v23 → v24) and why

| # | v23 (before) | v24 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the profile endpoints. | New section *"Feature: My profile (Carina)"*: a table with the success and failures of both endpoints, which errors are ProblemDetails (401) and which keep `{ code, message, type }` (400, 404), and 6 tests. | The endpoints are new, so Claude needs the contract to test them. |
| 2 | — | *"the raw body ... does not contain `password`"* and *"in the database the member has the new name and the old email"*. | Proves that the response never shows the password hash and that the update cannot change the email. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: My profile (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v24, commit e28342c (numbered after the condition filter and API documentation PRs, #72 and #74, which make v22 and v23); section written and run by Carina Coman
- **Result:** new file `Members/MyProfileTests.cs` with 6 tests: logged in → GET 200 with id, name, lower-case email, role `Student` and `createdAt`, no `password` in the raw body; a token of a member that is not in the database → 404 `Member.NotFound`; no token → GET 401 and PUT 401 (ProblemDetails `Auth.Unauthorized`), member unchanged; a valid name → 204, new name and old email in the database; a 1-character name → 400 `Member.InvalidName`, member unchanged. No existing test or file in `src/` changed. `dotnet build` 0 warnings, no CA or IDE diagnostics; 10 architecture, 438 unit and 13 ML tests pass; the 6 new tests pass in every run.
- **Problems seen:** the existing `ReservationExpiryTests` (job on) failed in 2 of 4 integration runs with `SqliteException: SQLite Error 5: 'database is locked'`, so the "three runs in a row" check did not pass. The test was not touched by this run: the expiry job and the test use the same in-memory SQLite connection at the same time. Reported to the team, not fixed here.
- **Change to SKILL.md:** none after the run; v24 is the final version.
- **Commits:** skill e28342c; generated code 35d92a4

### Run 26, skill v25 (Feature: Public profile)

- **Why a new version:** task 12 (issue #78). `GET /api/v1/members/{id}/profile` is a new public endpoint; it must never show the email, and it shows only Active listings and the review summary.

#### Change to SKILL.md (v24 → v25) and why

| # | v24 (before) | v25 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the public profile. | New section *"Feature: Public profile (Carina)"*: the response, the 404 from the handler and from the route, how to seed a sold listing with a review, and 4 tests. | The endpoint is new, so Claude needs the contract to test it. |
| 2 | — | *"the raw body (`ReadAsStringAsync`) does not contain the seller's email"*. | Proves over HTTP that the email, which is personal data, never leaves the API here. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Public profile (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v25, commit 4394edd (numbered after the condition filter, API documentation and my profile PRs, #72, #74 and #76, which make v22, v23 and v24); section written and run by Carina Coman
- **Result:** new file `Members/PublicProfileTests.cs` with 4 tests, none of them logged in: an Active and a Draft listing → 200 with only the Active one (id, title, price), the right name, 0 reviews, no average, and the raw body does not contain the email; a sold listing reviewed with 4 → `averageRating` 4, `reviewCount` 1, no active listings; unknown id → 404 `Member.NotFound`, type `NotFound`; `/members/abc/profile` → 404 (status only). Every test checks that the seeded data is unchanged. No existing test or file in `src/` changed. `dotnet build` 0 warnings, no diagnostics in the SARIF log; integration tests 223/223 three runs in a row; full run: 10 architecture, 438 unit, 13 ML, 223 integration.
- **Problems seen:** the first version of the first test ordered the listings by date inside the database query, which SQLite cannot do with `DateTimeOffset`, so it failed. The run fixed its own test in the same session (no sort in the query, the two listings compared without order); the API was not involved.
- **Change to SKILL.md:** none after the run; v25 is the final version.
- **Commits:** skill 4394edd; generated code 9febc5f

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

### Run 31, skill v30 (Feature: Register rate limiting)

- **Why a new version:** task 5 (issue #98). `POST /api/v1/auth/register` got the rate limiter policy "register": at most 5 registrations per minute from the same IP, then 429, with the limit read from `RateLimiting:RegisterPermitLimit`. The integration test factory raises it to 1000 so the other tests are not blocked, so the real limit needed its own tests.

#### Change to SKILL.md (v29 → v30) and why

| # | v29 (before) | v30 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the register rate limit. | New section *"Feature: Register rate limiting (Carina)"*: the policy, the factory setting (1000), a second copy of the API with `UseSetting("RateLimiting:RegisterPermitLimit", "5")`, and 3 tests. | Without the second copy the tests would run with the raised limit and never see a 429. |
| 2 | — | *"5 registrations whose passwords do not match → 400 each (they also count toward the limit); a 6th, valid → 429"*. | Proves that the limiter counts every request, not only the successful ones. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Register rate limiting (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results.` (new session)
- **Skill version:** v30, commit 3d0ee62 (numbered after #73, #75, #77 and #79, which make v22 to v25, and after the entries on main up to v29, the CORS run of #99); section written and run by Carina Coman
- **Result:** new file `Auth/RegisterRateLimitTests.cs` with 3 tests: six valid registrations with the limit at 5 → the first five 201, the sixth 429, exactly 5 members and none with the sixth email; five registrations whose passwords do not match → 400 each, then a sixth valid one → 429, no members; the normal factory (limit 1000) → six registrations 201, 6 members. The first two use a second copy of the API (`WithWebHostBuilder`, disposed with `await using`); the five earlier calls are in a private helper `RegisterFiveAsync`, written as five explicit calls like `ExhaustLoginRateLimitAsync`, so no test has a loop. Nothing changed in `src/`, the factory, `IntegrationTestBase` or the skill. Integration tests 230/230 three runs in a row.
- **Problems seen:** none. Notes from the run: the 429 is checked only by status code, as the section says (the error handling section may turn it into ProblemDetails, but its body is not checked); the status codes of the five earlier calls are collected in the Arrange and checked at the start of the Assert, so the Act stays the single sixth call.
- **Change to SKILL.md:** none after the run; v30 is the final version.
- **Commits:** skill 3d0ee62; generated code 9e9c0fb

### Run 32, skill v31 (Feature: Change password)

- **Why a new version:** task 11 (issue #101). `POST /api/v1/auth/change-password` is a new endpoint that needs a token; the change must be saved and must change how login behaves.

#### Change to SKILL.md (v30 → v31) and why

| # | v30 (before) | v31 (after) | Why |
|---|---|---|---|
| 1 | Nothing about changing the password. | New section *"Feature: Change password (Carina)"*: a table with the success and the failures, which errors are ProblemDetails (401) and which keep `{ code, message, type }` (400, 404), the arrange with a real hash and `AuthenticateAs`, and the test list. | The endpoint is new, so Claude needs the contract to test it. |
| 2 | — | *"in the database `AspNetPasswordHasher.Verify` accepts the new password and rejects the old one"* and the login tests after a change (new password → 200, old → 401). | Proves that the new hash is really stored and used by login. |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them and only add the tests from the "Feature: Change password (Carina)" section. Follow the skill exactly. When you finish, run dotnet build and dotnet test and tell me the results. Do not commit.` (new session)
- **Skill version:** v31, commit 0c22b8e (numbered after the CORS run of #99, v29, and the register rate limiting PR, issue #98, v30); section written and run by Carina Coman
- **Result:** new file `Auth/ChangePasswordTests.cs` with 8 tests: valid request → 204 and the stored hash verifies the new password, not the old one; after a change, login with the new password → 200 and with the old one → 401; wrong current password → 400 `Auth.InvalidCredentials`, hash unchanged; 7 characters → 400 `Auth.WeakPassword`; confirmation different → 400 `Auth.PasswordsDoNotMatch`; a token of a member that is not in the database → 404 `Member.NotFound`; no token → 401. No existing test or file in `src/` changed. `dotnet build` 0 warnings, no CA or IDE diagnostics in the SARIF log; 469 unit, 10 architecture, 13 ML and 237 integration tests pass.
- **Problems seen:** in the repeated integration runs one run had 1 failure out of 237. Run again by hand: the failing test is the existing `ReservationExpiryTests.Given_JobOnWithExpiredAndValidReservations_When_JobRuns_Then_ExpiresOnlyTheExpiredOne`, 1 failure in 4 runs, still flaky after #97. The 8 new tests passed in every run. Reported to Sara, not fixed here.
- **Change to SKILL.md:** none after the run; v31 is the final version.
- **Commits:** skill 0c22b8e; generated code 5498907

### Run 33, skill v32 (Feature: Live and ready health checks)

- **Why a new version:** `GET /health/live` (no checks) and `GET /health/ready` (database + the 3 ML models) were added next to `/health`; none had an integration test, and the Unhealthy branch of `MlModelsHealthCheck` cannot be reached over HTTP.

#### Change to SKILL.md (v31 → v32) and why

| # | v31 (before) | v32 (after) | Why |
|---|---|---|---|
| 1 | Nothing about `/health/live` and `/health/ready`. | New section *"Feature: Live and ready health checks (Diana)"*: the file (`Infrastructure/HealthTests.cs`) and 4 tests: `/health/live` → 200 `Healthy`; `/health/ready` → 200 `Healthy`; `/health` → 200; `CheckHealthAsync` with a `ModelsPath` that does not exist → Unhealthy, the message lists the 3 missing files. | The ready check depends on the models copied to the test output, which only a real request through the host proves; the Unhealthy branch is reached by calling the check directly. |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Live and ready health checks (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v32, commit e90123a (section written and run by Diana Preutu with Claude Code)
- **Result:** 4 new tests in the new file `Infrastructure/HealthTests.cs`; the three HTTP tests check the status, the plain text body `Healthy` and that the database is still empty. The Unhealthy test calls `MlModelsHealthCheck.CheckHealthAsync` directly (no HTTP call, the only way to reach that branch) and checks the status and that the message names `price-regression.zip`, `category-classification.zip` and `spam-detection.zip`. No existing test changed. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 438 unit, 13 ML, 226 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v32 is the final version.
- **Commits:** skill e90123a; generated code 341a311

### Run 34, skill v33 (Feature: Correlation id)

- **Why a new version:** every response now has an `X-Correlation-Id` header, set by a new middleware that runs before every other one. It is a behaviour of the whole pipeline (also on 404 and error answers), so only a real request can show it.

#### Change to SKILL.md (v32 → v33) and why

| # | v32 (before) | v33 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the correlation id. | New section *"Feature: Correlation id (Diana)"*: the file (`Infrastructure/CorrelationIdTests.cs`, new) and 3 tests: no header → a header that is a valid Guid; `X-Correlation-Id: test-123` → the same value; a 404 answer also has the header. | The three cases are the three paths of the middleware (new id, id from the client, id on an error answer). |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Correlation id (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v33, commit 1de3dc2 (section written and run by Diana Preutu with Claude Code)
- **Result:** new file `Infrastructure/CorrelationIdTests.cs` with 3 tests: `GET /api/v1/listings` without the header → 200 and one `X-Correlation-Id` value that is not empty and is a Guid; the same request with `test-123` → the header is exactly `test-123`; `GET /api/v1/listings/{random Guid}` → 404 with the header. No existing test changed. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 438 unit, 13 ML, 225 integration. With coverage like the CI, every line and branch of `CorrelationIdMiddleware` is covered by the integration tests (the unit test report has no hit, as expected).
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v33 is the final version.
- **Commits:** skill 1de3dc2; generated code 873dccb

### Run 35, skill v34 (Feature: Listings cache)

- **Why a new version:** `GET /api/v1/listings` is now cached for 30 seconds (output cache, tag `listings`) and every endpoint that changes a listing clears the tag. A wrong or missing clear shows old data, which only a request after a cached request can show; no unit test can.

#### Change to SKILL.md (v33 → v34) and why

| # | v33 (before) | v34 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the cache. | New section *"Feature: Listings cache (Diana)"*: the file (`Listings/ListingsCacheTests.cs`, new) and 3 tests: search, then create and publish, then the same search; search, then update the title, then the same search; search, then reserve, then the same search. | Each test makes the first search fill the cache, so the second search only shows the change if the tag was cleared. |
| 2 | — | Which endpoints clear the cache: create, update, delete, publish, reserve, cancel reservation, confirm sale, hide. | Tells Claude the contract; the section tests three of them, one for each kind of change (new, changed, no longer `Active`). |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Listings cache (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v34, commit 030e958 (section written and run by Diana Preutu with Claude Code)
- **Result:** New file `Listings/ListingsCacheTests.cs` with 3 tests, each doing a search before the change and the same search after it: the new listing is in the second result; the new title is in the second result; the reserved listing is gone from the second result. No existing test changed. There is no unit-testing run for this feature: the endpoints have no unit tests. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 432 unit, 13 ML, 222 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v34 is the final version.
- **Commits:** skill 030e958; generated code 9f480ad

### Run 36, skill v35 (Feature: Listings cache, review fix)

- **Why a new version:** Sara's review on PR #84 found that the Run 35 tests did not test the cache. The ASP.NET Core output cache does not store a response to a request with an `Authorization` header, and those tests searched after `AuthenticateAs`, so every search went to the database and the tests would pass without `ListingsCache.ClearAsync`.

#### Change to SKILL.md (v34 → v35) and why

| # | v34 (before) | v35 (after) | Why |
|---|---|---|---|
| 1 | Section *"Feature: Listings cache (Diana)"*: file `Listings/ListingsCacheTests.cs` (new); 3 tests: *"search, then create and publish a listing, then search again with the same query"*, *"search, then update a listing's title, then search again"*, *"search, then reserve the listing, then search again"*. Nothing about tokens. | Same section with the note *"the ASP.NET Core output cache does not store responses to requests that have an `Authorization` header. Every search in these tests is sent **without a token**; only the change (create, publish, update, reserve) is sent with a token."*, and the file is *"(replace its tests)"*. | The old tests logged in before the first search, so the cache was never filled. |
| 2 | No setup. | New *Setup*: every test starts by evicting the tag `ListingsCache.Tag` from `IOutputCacheStore` taken from `Factory.Services`. | The tests do not affect each other. |
| 3 | 3 tests. | 4 tests: the 3 above, each with searches without a token (the create/publish one is *"search without a token → create a listing with a token → the same search without a token (the draft is not there, and this fills the cache) → publish with a token → the same search without a token → the new listing is there"*), and a new first one: search without a token → change a published listing in the database through the DbContext (no endpoint) → the same search still returns the old title. | A test that proves the cache is used; without it, the other three could pass with no cache at all. |

- **Prompt:** `Run the integration-testing skill for "Feature: Listings cache (Diana)". Replace the old tests in Listings/ListingsCacheTests.cs with the ones the section describes. Run dotnet test. Then remove the ListingsCache.ClearAsync line from one endpoint (for example UpdateListingEndpoint), run only ListingsCacheTests and confirm the update test fails, then put the line back and confirm all pass again.`
- **Skill version:** v35, commit a93eb36 (section written by Diana Preutu with Claude Code)
- **Result:** `Listings/ListingsCacheTests.cs` now has 4 tests. Searches use an anonymous client (`Factory.CreateClient()`), changes use the logged-in `Client`; the title change in the first test is a SQL `UPDATE` through the DbContext. The create/publish test has a search between create and publish, because create already clears the cache and a single search before it could not show a missing clear in publish. Check: `ListingsCache.ClearAsync` was removed from `UpdateListingEndpoint`, `PublishListingEndpoint` and `ReserveListingEndpoint`, one at a time; each time exactly the matching test failed (update, publish, reserve) and the other 3 passed; with the lines back all 4 pass. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 438 unit, 13 ML, 226 integration.
- **Problems seen:** the first build failed on missing `using` lines in the test file (`DependencyInjection`, `Persistence`); fixed in the test.
- **Change to SKILL.md:** none after the run; v35 is the final version.
- **Commits:** skill a93eb36; generated code 8631881

### Run 37, skill v36 (Reservation expiry: stop the job before the Act)

- **Why a new version:** `ReservationExpiryTests.Given_JobExpiredTheReservation_When_AnotherMemberReserves_Then_Returns201` still failed in CI now and then (500 instead of 201, last time on PR #103). After #97 the copy of the API with the expiry job was stopped before the final database reads, but the reservation POST was still sent through that copy while the job ticked every 200 ms on the shared in-memory SQLite connection; a tick at the same moment gives "database is locked" and the API answers 500.

#### Change to SKILL.md (v35 → v36) and why

| # | v35 (before) | v36 (after) | Why |
|---|---|---|---|
| 1 | "Feature: Reservation expiry (Sara)" only said to poll with a timeout and to check the database at the end. | New rule: the copy with the job on must not run while the test sends its Act request or reads the database for its assertions; poll inside `await using (var withJob = WithExpiryJob())`, leave the block (the job stops), then send the Act with the normal `Client` after `AuthenticateAs(...)`. | The POST does not need the job; running it next to the job is the only way the two can meet on the same connection. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). The section "Feature: Reservation expiry (Sara)" has a new last rule. The test Given_JobExpiredTheReservation_When_AnotherMemberReserves_Then_Returns201 breaks it: apply the rule to the tests of that section in ReservationExpiryTests.cs, change only what the rule requires, keep names, arrange and assertions. Build, run the ReservationExpiryTests 20 times in a row, the whole solution once, and the SARIF check.` (new session)
- **Skill version:** v36, commit 988ce9c (rule written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** only that test changed: it waits for `Expired` inside the block with the job, checks the status after the block, then logs in the second buyer on the normal `Client` and sends the POST with the job stopped. The unused `using System.Net.Http.Headers;` was removed. The other two tests already followed the rule. `ReservationExpiryTests` passed 20 runs out of 20; full run: 10 architecture, 481 unit, 13 ML, 270 integration; build 0 warnings; the SARIF log of the test project is empty.
- **Problems seen:** the rule says to leave the block before "the Act and the assertions"; the check that the job expired the reservation reads only a local variable, so it stays right after the block, before the Act, to fail early with a clear message.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 988ce9c; generated code d431dc4

### Run 38, skill v37 (Feature: Validation thresholds)

- **Why a new version:** the two decision thresholds of the ML models became settings chosen on validation data (`Ml:Category:MinConfidence` 0.6, `Ml:Spam:Threshold` 0.4), validated at startup. The category tests compared with the removed constant `SuggestCategoryHandler.MinConfidence`, and nothing tested that the settings are read and checked. (Numbered after Run 37, the reservation expiry fix in its own PR.)

#### Change to SKILL.md (v36 → v37) and why

| # | v36 (before) | v37 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the settings. | New section *"Feature: Validation thresholds (Anca)"*: the two `SuggestCategoryTests` compare with the configured value from `IOptions<CategorySuggestionOptions>`; new `Infrastructure/MlThresholdsTests.cs`: the defaults from `appsettings.json`, a 0.99 minimum confidence hides a confident suggestion, a 0 spam threshold reports a normal listing, and an out-of-range value of either setting stops the API. | Only a real host shows that the configuration reaches the handler and the spam detector, and that a wrong value is refused at startup. |
| 2 | (after the run) nothing said how to log in on a copy of the API, nor that `git status` lists the hand-made changes. | Two rules: `Authorization: Bearer` with `TestTokens.Create` on the copy's client; the hand-made changes are listed by `git status`. | The run found both gaps. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). Integration tests already exist: keep them all. Generate the changes for the section "Feature: Validation thresholds (Anca)": update the two tests in Listings/SuggestCategoryTests.cs and add Infrastructure/MlThresholdsTests.cs with only the tests it lists. Build, run the integration tests three times and the whole solution once, section 10 and the SARIF check. You may only change files under tests/CampusMarket.IntegrationTests/.` (new session)
- **Skill version:** v37, commit 2d54c3f (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** `SuggestCategoryTests.cs` (a `MinConfidence` property read from the options, used by the two tests) and new `MlThresholdsTests.cs` with 5 tests: defaults 0.6 / 0.4, `MinConfidence` 0.99 → `category` null for the Notes title, spam threshold 0 → a normal listing gets an automatic report, 1.5 and -0.1 → `OptionsValidationException` naming the setting. Integration tests 275/275 three times; full run 10 architecture, 484 unit, 41 ML, 275 integration; SARIF log empty.
- **Problems seen:** `AuthenticateAs` only logs in the base client, so the copies use the `Bearer` header (rule added); the 0.6 floor in the 0.99 test is written as a number, the defaults test checks that it is the configured value.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill 2d54c3f; generated code 31f3668

### Run 39, skill v38 (Reservation expiry: retry a polling read that collides with the job)

- **Why a new version:** after Run 37 the same test still failed once in CI (PR #106), this time inside `WaitForReservationStatusAsync`: while the job was running, one of the polling reads threw (stack: `QueryDbAsync` → `DbContext.Set` → service resolution). Run 37 stopped the job before the Act and the assertions, but the polling reads have to run while the job is on, on the same in-memory SQLite connection.

#### Change to SKILL.md (v37 → v38) and why

| # | v37 (before) | v38 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the polling reads failing. | New rule in "Feature: Reservation expiry (Sara)": a polling read that throws a `SqliteException` or an `InvalidOperationException` (which covers `ObjectDisposedException`) counts as "not yet"; wait 100 ms and read again until the timeout; after the timeout, one more read without catching, so an error that never goes away still fails the test with its own message. Catch only those two types. | The read and the job's write can meet on the shared connection; retrying is what the polling is for. |
| 2 | (after the first try) "a read that throws". | The two exception types, written as `catch (Exception ex) when (...)`. | The first run caught every exception; Sonar flags a general catch (S2221). |

- **Prompt:** `Use the integration-testing skill (read the whole file first). The section "Feature: Reservation expiry (Sara)" has a new last rule about the polling reads. WaitForReservationStatusAsync breaks it: in CI a polling read threw while the job was running. Apply the rule to that helper; change only what it requires. Build, run the ReservationExpiryTests 30 times in a row, the whole solution once and the SARIF check.` Then, after the rule was narrowed: `Re-read the rule and catch only SqliteException or InvalidOperationException; run the filter 15 times, the whole solution and the SARIF check again.` (new session)
- **Skill version:** v38, commit ba5a270 (rule written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** only the helper changed: the query moved to a private `ReadReservationStatusAsync`, the loop catches the two exception types and retries every 100 ms, and after the 5 s timeout one last read runs without a catch; the tests, the assertions and the timeouts are unchanged. `ReservationExpiryTests` passed 30 runs with the first version and 15 runs with the narrowed catch; full run 10 architecture, 484 unit, 41 ML, 275 integration; SARIF log empty.
- **Problems seen:**
    1. The CI failure could not be reproduced on this machine (every local run passed before and after), so the fix follows the stack trace and the rule, not a local repro.
    2. The last read after the timeout still runs while the job is on, so in the rare case where no read succeeds in 5 seconds it can fail with the collision instead of the "within 5 seconds" message; that only happens when the job is already too slow.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill ba5a270; generated code 9aef200

### Run 40, skill v39 (Feature: Similar listings)

- **Why a new version:** new public endpoint `GET /api/v1/listings/{id}/similar` that runs the real similar-listings model, and a fourth model file that the ready check now requires.

#### Change to SKILL.md (v38 → v39) and why

| # | v38 (before) | v39 (after) | Why |
|---|---|---|---|
| 1 | Nothing about similar listings. | New section *"Feature: Similar listings (Anca)"*: the endpoint with the real model, the seeded listings (two math textbooks, two laptops, notes, a bicycle, a draft textbook) and 5 tests: a textbook gets the other textbook first, a laptop the other laptop, a draft target gets a textbook, only the target published gives no items, unknown id → 404; plus `similar-listings.zip` in the "models folder does not exist" health test. | Only the real model over HTTP shows that the ranking makes sense and that the model file is loaded. |
| 2 | (after the first run) titles only. | Every listing gets a condition, a price and a description, the bicycle none. | The first run seeded no description at all, so the finder's branch for a listing with a description was never run. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). Integration tests already exist: keep them all. Generate the tests for the section "Feature: Similar listings (Anca)": the new file Listings/GetSimilarListingsTests.cs with only the tests it lists and the change to Infrastructure/HealthTests.cs. Build, run the integration tests three times and the whole solution once, section 10 and the SARIF check. You may only change files under tests/CampusMarket.IntegrationTests/.` Then, after row 2: `Re-read the Arrange and apply it to the seeding helper; change nothing else.` (new session)
- **Skill version:** v39, commit 7cb4df8 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** new `GetSimilarListingsTests.cs` with the 5 tests (every call without a token, every test checks the database) and one more assertion in `HealthTests.cs`. The real model ranks every case as expected, with and without descriptions. Integration tests 280/280 three times; full run 10 architecture, 491 unit, 61 ML, 280 integration; SARIF log empty.
- **Problems seen:** the first Arrange gave no descriptions (fixed, row 2); "a math textbook first" for the draft accepts either published textbook.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill 7cb4df8; generated code cfb9e5a

### Run 41, skill v40 (Feature: Conversations)

- **Why a new version:** four new endpoints (`POST /listings/{id}/conversations`, `POST /conversations/{id}/messages`, `GET /conversations/{id}`, `GET /members/me/conversations`) and two new tables (`Conversations`, `Messages`, migration `AddConversations`).

#### Change to SKILL.md (v39 → v40) and why

| # | v39 (before) | v40 (after) | Why |
|---|---|---|---|
| 1 | Nothing about conversations. | New section *"Feature: Conversations (Anca)"*: the endpoint table with every status, how to seed members, listings and conversations, and the tests per endpoint: a second message goes to the same conversation, a sold listing keeps an existing conversation, only the two members can read or write (403 for another member and for an Admin), my conversations as buyer and as seller, and the cascade when the listing is deleted. | Only real HTTP calls on the real database show the 403s, the `Location` header, the unique index and the cascade. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). Integration tests already exist: keep them all. Generate the tests for the section "Feature: Conversations (Anca)": the four new files under Conversations/ with only the tests it lists. Build, run the integration tests three times and the whole solution once, section 10 and the SARIF check. You may only change files under tests/CampusMarket.IntegrationTests/.` (new session)
- **Skill version:** v40, commit 64131b7 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** `Conversations/StartConversationTests.cs` (13), `SendMessageTests.cs` (7), `GetConversationTests.cs` (5), `GetMyConversationsTests.cs` (4). Integration tests 309/309 three times; full run 10 architecture, 541 unit, 69 ML, 309 integration; SARIF log empty.
- **Problems seen:** none; the API matched the section. The table names a hidden listing for `Conversation.ListingNotAvailable` but the test list does not (the unit tests cover it).
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 64131b7; generated code b9400fc

### Run 42, skill v41 (Feature: Listing photos)

- **Why a new version:** four new endpoints (upload with `multipart/form-data`, list, the image, delete), files written on disk and the photo folder removed when a listing is deleted.

#### Change to SKILL.md (v40 → v41) and why

| # | v40 (before) | v41 (after) | Why |
|---|---|---|---|
| 1 | Nothing about photos. | New section *"Feature: Listing photos (Anca)"*: the endpoint table, a temporary photos folder per test factory (`Factory.PhotosPath`, deleted on dispose, never the source tree), how to upload with `MultipartFormDataContent`, and the tests: the three formats, a renamed GIF, exactly 2 MB and one byte more, no file, the sixth photo, 403/404/401/415, the image bytes and headers, a row whose file is missing, deleting one photo, and the folder removed with the listing. | Only real HTTP calls show the form binding, the 415, the `Location` and the files on disk. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). Integration tests already exist: keep them all. Generate the tests for the section "Feature: Listing photos (Anca)": the change to CampusMarketApiFactory and the four new files under Photos/ with only the tests it lists. Build, run the integration tests three times and the whole solution once, section 10, the SARIF check, and check that no photos folder was created in the repository. You may only change files under tests/CampusMarket.IntegrationTests/.` (new session)
- **Skill version:** v41, commit 59b3660 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** `CampusMarketApiFactory` with its own temporary `PhotosPath`; `Photos/UploadPhotoTests.cs` (13 cases), `GetListingPhotosTests.cs` (4), `GetPhotoFileTests.cs` (3), `DeletePhotoTests.cs` (9). Integration tests 338/338 three times; full run 10 architecture, 581 unit, 69 ML, 338 integration; SARIF log empty; no photos folder in the repository or left in the temp folder.
- **Problems seen:** the 415 answer has `code` `Server.Error`, because the status-code mapping has no case for 415 (the same for every endpoint, not only photos); the test checks the status and the ProblemDetails shape, not the code. "Delete, then GET → 404" became its own test (one HTTP call per Act).
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 59b3660; generated code 237ab25

### Run 43, skill v42 (Feature: Sorting)

- **Why a new version:** `GET /api/v1/listings?sort=...` is a new query parameter: `price_asc` orders by price over HTTP, an unknown value answers 400 `Listings.InvalidSort`.

#### Change to SKILL.md (v41 → v42) and why

| # | v41 (before) | v42 (after) | Why |
|---|---|---|---|
| 1 | Nothing about sorting. | New section *"Feature: Sorting (Diana)"*: the file (`Listings/GetListingsTests.cs`, add to it) and 2 tests: `?sort=price_asc` → 200 with the prices in ascending order; `?sort=abc` → 400 `Listings.InvalidSort`. | The parameter has to be bound from the query string and the error mapped to 400 with the `Error` body; only a real request shows that. |

- **Prompt:** `Run the integration-testing skill for the section "Feature: Sorting (Diana)". Add only those tests. Run dotnet test.`
- **Skill version:** v42, commit b5da1ba (section written and run by Diana Preutu with Claude Code)
- **Result:** 2 new tests in `Listings/GetListingsTests.cs`: three published listings priced 90, 10 and 40 (created in another order) → `?sort=price_asc` gives 10, 40, 90; `?sort=abc` → 400, `Listings.InvalidSort`, type `Validation`, the exact message. Both check the database count. No existing test changed. `dotnet build` 0 warnings, 0 errors; full run: 10 architecture, 442 unit, 13 ML, 223 integration.
- **Problems seen:** none.
- **Change to SKILL.md:** none after the run; v29 is the final version.
- **Commits:** skill b5da1ba; generated code 2d27370

### Run 44, skill v43 (Feature: Notifications)

- **Why a new version:** three new endpoints under `/api/v1/members/me/notifications` (list, mark one as read, mark all as read) and notifications written when a listing is reserved, sold or a reservation expires.

#### Change to SKILL.md (v42 → v43) and why

| # | v42 (before) | v43 (after) | Why |
|---|---|---|---|
| 1 | Nothing about notifications. | Section *"Feature: Notifications (Sara)"* (written by Sara, commit 155811a): reserve → the seller is notified, confirm sale → the buyer is notified, mark one / all as read, 404 for another member's notification, 401 without a token. | The notifications are written through the real reserve and confirm-sale endpoints and read back over HTTP. |
| 2 | (after the first run) | Two more tests: two notifications come newest first (every field) and `unreadOnly` after reading one; `page=0` and `pageSize=101` → 400. | The coverage run after the merge with main showed the sorting in `EfNotificationRepository` and the 400 answer of the list endpoint were never run (Sonar needs 100% of the new code). |

- **Prompt:** `Use the integration-testing skill in .claude/skills/integration-testing/SKILL.md. Integration tests already exist: keep them, only add the tests from the "Feature: Notifications (Sara)" section. Run dotnet test three times. Do not commit.` Then, after row 2: `Add only the tests for the two new bullets of the section to NotificationsTests.cs.` (new sessions, run by Ancuța Ilenei at Sara's request)
- **Skill version:** v43, commit 155811a; row 2 in the merge commit c288b04
- **Result:** `Notifications/NotificationsTests.cs`, 11 tests in the first run, 15 after row 2. Integration tests 361/361 three times; full run 10 architecture, 654 unit, 69 ML, 361 integration; SARIF log empty.
- **Problems seen:** the first run found a real bug: `GET /members/me/notifications` answered 500, because SQLite cannot `ORDER BY` a `DateTimeOffset` column. Fixed in `EfNotificationRepository.GetByMemberAsync` (read, then sort in memory, like the other repositories), commit 4a1b0bb.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill 155811a; generated code 4d5a11a, c288b04

### Run 45, skill v44 (Feature: Full coverage)

- **Why a new version:** issue #121 (100% coverage in SonarQube, every mutant killed). After all four test projects ran, the coverage analysis still showed gaps in five files: `PriceAccuracyEndpoint.cs` (the whole `Handle`, no test called the endpoint as Admin), `EfSaleRecordRepository.GetAllAsync`, the 404 branch of `UpdateMyProfileEndpoint.cs`, `CampusMarketSetup.cs` (the IP branch of the `register` policy, a `Jwt:Key` of only spaces, the demo seed on a database that already has a member, and `TrySeedDemoMember` when `Member.Register` fails) and `SmtpEmailSender.cs` (the credentials branch and the end of `SendAsync`; Sara chose option (a), a local fake SMTP server).

#### Change to SKILL.md (v43 → v44) and why

| # | v43 (before) | v44 (after) | Why |
|---|---|---|---|
| 1 | Nothing about these gaps; section 7 forbids starting a server on a port, and "Feature: Password reset (Sara)" forbids opening an SMTP connection. | New section *"Feature: Full coverage (Carina)"*: the production code of each gap and the tests: `Admin/PriceAccuracyTests.cs` (Admin with two seeded `SaleRecord`s → 200 with `count`, `mae`, `mape`, `perCategory` and `lastRecords`; student → 403; no token → 401), the unknown member on `PUT /members/me` → 404, two register rate limit tests with `TestRemoteIpStartupFilter`, three `ProductionConfigurationTests` (`Jwt:Key` of spaces, demo seed with a member already there, demo seed with `EmptyHashPasswordHasher`), and `SmtpEmailSenderTests` with a new `FakeSmtpServer` (`TcpListener` on `127.0.0.1`, port 0, plain-text SMTP with `AUTH LOGIN`). The two rules get an explicit, narrow exception for the fake SMTP server only; the tests that set `Email__Username` and `Email__Password` run in a collection with `DisableParallelization = true` and restore the variables in `Dispose`. | Every gap is behaviour that only shows through the real host, the real database or a real SMTP dialog; the exception is limited to a loopback fake so no test can reach a real mail server. |

- **Prompt:** `/integration-testing Task: issue #121 "Coverage 100% in SonarQube and all mutants killed". Scope for this run: the integration cases from the coverage analysis, plus SmtpEmailSender (Sara chose option (a): a local fake SMTP server). 1. Update .claude/skills/integration-testing/SKILL.md: add a section "Feature: Full coverage (Carina)" describing these cases (v41 is recorded only in PROMPTS.md): a. GET /api/v1/admin/ml/price-accuracy as Admin with at least one SaleRecord seeded in the database -> 200 and the body matches the seeded data (covers PriceAccuracyEndpoint and EfSaleRecordRepository.GetAllAsync). Also non-admin -> 403 and no token -> 401. b. PUT /api/v1/members/me authenticated as a member id that does not exist in the database -> 404 Member.NotFound. c. Register rate limit with the X-Test-Remote-Ip header (TestRemoteIpStartupFilter), so the IP branch of the "register" policy is used. d. Production environment with Jwt:Key = "   " (only spaces) -> InvalidOperationException at startup. e. Seed:DemoData=true when the database already contains a member -> no demo listings are added. f. Seed:DemoData=true with a valid Seed:DemoPassword and EmptyHashPasswordHasher -> assert what CampusMarketSetup actually does at line 299 (member creation fails, returns null). g. SmtpEmailSender with a local fake SMTP server (TcpListener on a free port, answers the basic SMTP commands): a successful send reaches the end of the method, and a send with Email__Username set covers the credentials branch. Tests that set environment variables must not run in parallel with other tests and must restore the variables afterwards. Commit only SKILL.md with the message "skills: integration-testing full coverage section (v41) (#121)". 2. Following the skill, write the integration tests. Run all tests with coverage like the CI does. PriceAccuracyEndpoint.cs, EfSaleRecordRepository.cs, UpdateMyProfileEndpoint.cs, CampusMarketSetup.cs, SmtpEmailSender.cs, ConfirmSaleHandler.cs and ResetPasswordHandler.cs must all reach 100% line and branch coverage. If something still cannot be covered, stop and tell me why instead of forcing it. 3. Add Run 42 to PROMPTS.md under integration-testing, in exactly the same format as Run 41 (v40 -> v41, change table, prompt, result, problems, commit hashes, no TODO). 4. Restore coverage-report and delete TestResults. Commit only the test files and PROMPTS.md with the message "test: integration tests for full coverage, Run 42 (#121)". 5. Show me git log --oneline -6, git status, and the coverage percentage (line and branch) for each of the 7 files above.`
- **Skill version:** v44, commit 095d3e7 (section written by Carina with Claude Code; renumbered from v41 when main was merged, because #120 (listing photos) makes v41; renumbered again from v42 when main was merged a second time, because main's Runs 43 and 44 (Sorting, Diana; Notifications, #111) make v42 and v43)
- **Result:** new `Admin/PriceAccuracyTests.cs` (3), `Infrastructure/FakeSmtpServer.cs` and `Infrastructure/SmtpEmailSenderTests.cs` (2); added to `Members/MyProfileTests.cs` (1), `Auth/RegisterRateLimitTests.cs` (2) and `Infrastructure/ProductionConfigurationTests.cs` (3): 11 new tests. Integration tests 328/328 three times; full run with coverage as in the CI: 10 architecture, 566 unit, 69 ML, 328 integration; build 0 warnings; SARIF log empty. Coverage (all test projects merged): `PriceAccuracyEndpoint.cs` 9/9 lines, `EfSaleRecordRepository.cs` 3/3 lines, `UpdateMyProfileEndpoint.cs` 13/13 lines and 4/4 branches, `CampusMarketSetup.cs` 200/200 lines and 54/54 branches, `SmtpEmailSender.cs` 18/18 lines and 2/2 branches, `ConfirmSaleHandler.cs` 43/43 lines and 10/10 branches, `ResetPasswordHandler.cs` 42/42 lines and 18/18 branches: 100% for all seven.
- **Problems seen:**
    1. `ConfirmSaleHandler.cs` and `ResetPasswordHandler.cs` were already at 100% before this run (commit d000b76 removed the unreachable `SaleRecord` check; Run 34 of unit-testing, commit 9f34b37, covered the empty-hash branch), so no integration test was needed for them.
    2. Not from this section: the sections *"Feature: Similar listings (Anca)"* and *"Feature: Conversations (Anca)"* (v39 and v40, commits 7cb4df8 and 64131b7) are no longer in `SKILL.md`; they were dropped in the merge a5e2569, as in the unit-testing skill (Run 34). Left for the owner of those sections.
- **Change to SKILL.md after the run:** none.
- **Commits:** skill 095d3e7; generated code and this entry in the same commit (the one that adds `Admin/PriceAccuracyTests.cs`)

### Run 46, skill v45 (Feature: Admin categories)

- **Why a new version:** branch `feature/admin-categories`. New admin-managed categories: `GET /api/v1/categories` (public) and `PUT /api/v1/categories/{category}` (Admin only), the 5 base categories seeded by the migration `AddCategories`, and a category check in `CreateListingHandler`, `UpdateListingHandler` and `PublishListingHandler` (a deactivated category takes no new, moved or published listings, `Listing.CategoryInactive`). The unit tests came first (Run 38 of unit-testing); the new endpoints and the new listing failures had no integration test.

#### Change to SKILL.md (v44 → v45) and why

| # | v44 (before) | v45 (after) | Why |
|---|---|---|---|
| 1 | Nothing about categories managed by an Admin. | New section *"Feature: Admin categories (Carina)"*: the two endpoints with their success and failure answers (401/403 ProblemDetails, 404 `Category.NotFound`, 400 `Category.IsActiveRequired` / `DisplayNameEmpty` / `DisplayNameTooLong` / `DescriptionTooLong`, 409 `Category.LastActive`), the three `Listing.CategoryInactive` cases and the edit that keeps the category, and the files and tests to add. | Without it Claude would have to guess the status codes, which answers are ProblemDetails and when the listing handlers look at the category. |
| 2 | — | *"The 5 categories are seeded by the migration"* (keys, display names, all active, description `null`); never seed or delete category rows. | Every test database already has them, so a test only changes what it needs. |
| 3 | — | *"A test that deactivates a category must not affect other tests"*: it deactivates only in its own database, through a private helper that calls the domain `Update` on the tracked categories, never through the endpoint, shared state or a kept `WithWebHostBuilder` copy. | Deactivating is shared, durable state; the per-test factory and in-memory database (section 3) keep it inside one test. |
| 4 | — | *"The existing publish, create and edit integration tests stay unchanged"*; the new cases go in `Listings/InactiveCategoryTests.cs`. | The existing tests must keep testing what they tested before, with all 5 categories active. |

- **Prompt:** `/integration-testing Task: admin-managed categories, branch feature/admin-categories. Rules: do NOT commit, do NOT push, no Co-Authored-By. Do not change any file in src/ or frontend/. Do not touch the Postman collection in this step. 1. Update .claude/skills/integration-testing/SKILL.md: add a section "Feature: Admin categories (Carina)" with the rules for this feature (the 5 categories are seeded by the migration; tests that deactivate a category must not affect other tests; existing publish/create/edit integration tests stay unchanged). 2. Write the integration tests, following the skill: - GET /api/v1/categories without token -> 200 with the 5 seeded categories (key, display name, description, isActive), all active. - PUT /api/v1/categories/{category} as Admin -> 204, then GET shows the new display name, description and isActive. - PUT without token -> 401; as a normal member -> 403. - PUT unknown category -> 404 Category.NotFound; missing isActive -> 400 Category.IsActiveRequired; empty displayName -> 400 Category.DisplayNameEmpty; too long displayName -> 400 Category.DisplayNameTooLong; too long description -> 400 Category.DescriptionTooLong. - Deactivating the last active category -> 409 Category.LastActive and it stays active. - POST /api/v1/listings in an inactive category -> 400 Listing.CategoryInactive. - Editing a listing to move it into an inactive category -> 400 Listing.CategoryInactive. - Editing a listing that is already in an inactive category without changing the category -> succeeds. - Publishing a draft whose category became inactive -> 400 Listing.CategoryInactive. Check the exact error codes and ProblemDetails like the other integration tests do. 3. Run all tests with coverage like the CI does: everything passes, and every new or changed file in src (Domain/Categories, Features/Categories, EfCategoryRepository, ListingCategoryConfiguration, the 3 listing handlers, CampusMarketSetup, CampusMarketDbContext) is at 100% line and branch coverage. dotnet build for the whole solution: 0 warnings. 4. Add the next integration-testing Run to PROMPTS.md (next free Run number and next skill version after the last ones recorded), in the same format as the previous Runs, with "commit hashes: to be filled in". Tell me the Run number and version you used. 5. Restore coverage-report and delete TestResults. 6. Show me: the tests added, the coverage table, the build/test results and git status. Answer in Romanian.` (new session)
- **Skill version:** v45 (section written by Carina with Claude Code; the last integration-testing version was v44, Run 45)
- **Result:** three new files, 14 test methods (16 cases); no existing test changed:
    - `Categories/GetCategoriesTests.cs` (1): no token → 200, `count` 5, the 5 seeded categories in enum order with key, display name, `description` null and `isActive` true; the database has exactly the 5 seeded ones.
    - `Categories/UpdateCategoryTests.cs` (9 methods, 11 cases): Admin → 204, empty body, then `GET` shows the trimmed display name, the trimmed description and `isActive` false, and the database has the new values with the other 4 unchanged; no token → 401 ProblemDetails `Auth.Unauthorized`; a member → 403 ProblemDetails `Auth.Forbidden`; `Books` → 404 `Category.NotFound`; `isActive` missing → 400 `Category.IsActiveRequired`; display name `""`, `"   "`, `null` → 400 `Category.DisplayNameEmpty`; 101 characters → 400 `Category.DisplayNameTooLong`; description of 501 → 400 `Category.DescriptionTooLong`; the other 4 deactivated in the Arrange, deactivating `Textbook` → 409 `Category.LastActive` (`Conflict`), still active, 1 active category. Every failure: the 5 categories unchanged.
    - `Listings/InactiveCategoryTests.cs` (4): create in an inactive category → 400 `Listing.CategoryInactive`, no listing; move into an inactive category → 400, listing unchanged; edit a listing already in an inactive category, same category → 204, new values, `Version` 2; publish a draft whose category became inactive → 400, still `Draft`, `Version` 1, no report.
  Full run with coverage as in the CI: 10 architecture, 720 unit, 69 ML, 391 integration, all pass; integration tests 391/391 three times. `dotnet build --no-incremental` of the solution: 0 warnings, 0 errors; SARIF log of the integration test project empty. Coverage (all test projects merged): `ListingCategory.cs` 45/45 lines and 24/24 branches, `UpdateCategoryHandler.cs` 21/21 and 10/10, `UpdateCategoryEndpoint.cs` 17/17 and 6/6, `GetCategoriesHandler.cs` 8/8, `GetCategoriesEndpoint.cs` 8/8, `CategoriesEndpoints.cs` 6/6, the request, command, query and response records 100%, `EfCategoryRepository.cs` 11/11, `ListingCategoryConfiguration.cs` 18/18, `CreateListingHandler.cs` 23/23 and 8/8, `UpdateListingHandler.cs` 27/27 and 14/14, `PublishListingHandler.cs` 32/32 and 16/16, `CampusMarketSetup.cs` 214/214 and 56/56, `CampusMarketDbContext.cs` 18/18: 100% for all.
- **Problems seen:**
    1. The "Admin → 204, then GET shows the new values" case needs a second HTTP call; the `GET` is made in the Assert, after the one Act call, together with the database check.
    2. The "key is valid but not in the database" branch of `UpdateCategoryHandler` and the `category?.` null branches of the three listing handlers cannot be reached over HTTP (the migration always seeds the 5 rows); they are covered by the unit tests (Run 38 of unit-testing), so the merged coverage is 100%.
- **Change to SKILL.md after the run:** none.
- **Commits:** 0865eb0 (feat), 236c00d (skills), 89c2b88 (tests)

### Run 47, skill v46 (Feature: Spam rules)

- **Why a new version:** `SpamDetector` now also flags a listing when a clear rule matches (`Infrastructure/ML/SpamRules.cs`: link, phone, `@handle`, WhatsApp / money bait / pay-in-advance phrases, `!!!`); the rule branch and every rule had no test.

#### Change to SKILL.md (v45 → v46) and why

| # | v45 (before) | v46 (after) | Why |
|---|---|---|---|
| 1 | Nothing about the spam rules. | New section *"Feature: Spam rules (Anca)"*: publish through the real endpoint with the real model; each rule on its own (probability exactly 0.9), a text the model flags (its own probability), and two normal texts (one with an email) with no report. | Only the real model shows which texts reach the rule branch; each rule needs its own case for the branch coverage. |
| 2 | (after the first run) the phrase and handle cases were "Calculator stiintific / Scrie-mi pe WhatsApp…" and "Notite curs POO / Mai multe pe @notite_poo"; no price or category given. | "Manual de fizica / Plata in avans prin transfer" and "Halat laborator / Mesaj la @halat_m"; every listing `Category.Other`, price 40. | The first run found that the model already flags the two old texts (0.97 and 0.57), so they never reached the rule; the new texts were checked against the model first. |

- **Prompt:** `Use the integration-testing skill (read the whole file first). Integration tests already exist: keep them all. Generate the tests for the section "Feature: Spam rules (Anca)": the new file Moderation/SpamRulesTests.cs with only the tests it lists. Build, run the integration tests three times and the whole solution once, and the SARIF check. You may only change files under tests/CampusMarket.IntegrationTests/.` Then, after row 2: `Re-read the section and update the data of SpamRulesTests.cs; same tests.` (new session)
- **Skill version:** v46, commit 1cba445 (section written by Ancuța Ilenei with Claude Code; run in a new session)
- **Result:** `Moderation/SpamRulesTests.cs` (9 cases). Integration tests 404/404 three times; full run 10 architecture, 720 unit, 69 ML, 404 integration; SARIF log empty; `SpamRules.cs` and `SpamDetector.cs` fully covered.
- **Problems seen:** the two texts of row 2, reported by the run instead of being changed.
- **Change to SKILL.md after the run:** row 2 of the table above.
- **Commits:** skill 1cba445; generated code 4912528

### Run 48, skill v47 (Feature: Admin categories, existing listings in a deactivated category)

- **Why a new version:** branch `feature/inactive-category-listings`, week 8, task 12. Decision: deactivating a category only stops new listings (create, move by edit, publish of a draft, already refused with `Listing.CategoryInactive`); listings that are already `Active` in it stay visible in the search, in the category filter and by id. The code already behaved like this (`GetListingsHandler` and `GetListingByIdHandler` never read `ListingCategory`, the category filter of `BrowsePage.tsx` lists all 5 keys), but no test fixed the behaviour.

#### Change to SKILL.md (v46 → v47) and why

| # | v46 (before) | v47 (after) | Why |
|---|---|---|---|
| 1 | *"Feature: Admin categories (Carina)"* covered only the refused cases (create, move, publish) and the edit that keeps the category. | New rule in the same section: an `Active` listing whose category is deactivated afterwards stays visible; three tests in `InactiveCategoryTests` (search without parameters, `?category=` of that category, `GET /{id}`), arranged as published listing → `SeedAsync` → deactivate with the helper, no token, and the database check that the listing is still `Active`, `Version` 2, and the category still inactive. | Without it a later change could hide these listings (for example a join on the active categories in the search) and no test would fail. |

- **Prompt:** `/integration-testing Task: week 8, task 12 - what happens to existing listings in a deactivated category. Branch: feature/inactive-category-listings. Rules: do NOT commit, do NOT push, no Co-Authored-By. Do not touch the "my listings" endpoint, page or tests (a teammate is fixing them). Do not change src/ or frontend/ unless step 1 finds that the behaviour is different from the decision below; in that case STOP and tell me before changing anything. Decision: deactivating a category only stops NEW listings (create, move by edit, publish draft are already refused). Existing active listings in a deactivated category stay visible: they appear in GET /api/v1/listings search, can be filtered by that category, and GET /api/v1/listings/{id} still works. 1. First check in the code (GetListingsHandler, the listing details handler, the frontend search filter) that the current behaviour matches this decision. Tell me what you found. 2. Update .claude/skills/integration-testing/SKILL.md: add to the section "Feature: Admin categories (Carina)" the rule for this case. 3. Following the skill, write the integration tests: an active listing whose category is deactivated afterwards (deactivate it directly in the test database, like the existing admin categories tests do) -> it still appears in the search, it is returned when filtering by that category, and GET by id returns 200. 4. Run all tests with coverage like the CI does: everything passes, dotnet build with 0 warnings. 5. README.md: in the categories part, add one or two lines that document this behaviour. Only add lines. 6. Add the next integration-testing Run to PROMPTS.md (next free Run number and next skill version after the last ones recorded), same format as the previous Runs, with "- **Commits:** commit hashes: to be filled in". 7. Restore coverage-report and delete TestResults. 8. Show me: what you found in step 1, the tests added, the Run number, the build/test results and git status. Answer in Romanian, short.` (new session)
- **Skill version:** v47 (rule written by Carina with Claude Code; the last integration-testing version was v46, Run 47)
- **Result:** step 1 found the code already matches the decision, so nothing changed in `src/` or `frontend/`. Three tests added to `Listings/InactiveCategoryTests.cs` (with the private helpers `SeedActiveListingAsync` and `AssertListingAndCategoryUnchangedAsync`); no existing test changed:
    - published `Textbook` listing, `Textbook` deactivated → `GET /api/v1/listings` → 200, the listing is the only item, `totalCount` 1;
    - the same → `GET /api/v1/listings?category=Textbook` → 200, the listing is returned with category `Textbook`, `totalCount` 1;
    - the same → `GET /api/v1/listings/{id}` → 200, id, title, category, `Active`, `Version` 2.
  Every test checks that the listing is still `Active` with `Version` 2 and `Textbook` is still inactive. Full run with coverage as in the CI: 10 architecture, 720 unit, 69 ML, 407 integration, all pass. `dotnet build --no-incremental`: 0 warnings, 0 errors; SARIF log empty. README: one line added under the Admin categories paragraph.
- **Problems seen:** none.
- **Change to SKILL.md after the run:** none.
- **Commits:** 36b0422 (skills), e224165 (tests)

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

### Run 7

- **Prompt:** `SonarCloud shows 1 new issue on PR #99: frontend/src/api/config.ts line 3, "Simplify this regular expression to reduce its runtime, as it has super-linear performance due to backtracking."`
- **Decision:** the regex `/\/+$/` (remove the slashes at the end of `VITE_API_URL`) was replaced by a small loop, `withoutTrailingSlashes`, that walks back from the end; same result, no regex, so nothing can backtrack.
- **Checks:** `npm run build` and `npm run lint` with no errors or warnings.
- **Commits:** code and this entry in the same commit (the one that changes `frontend/src/api/config.ts`)

## health-checks

Liveness and readiness endpoints for the container. The production code is the one from the task, applied by Diana Preutu with Claude Code; the tests were generated afterwards with the integration-testing skill (Run 31).

### Run 1

- **Prompt:** `Do the whole task, exactly as written: add MlModelsHealthCheck, tag the database and ML checks "ready", map /health/live (no checks) and /health/ready, add both to the README endpoints table and to the Postman Health folder, append the section "Feature: Live and ready health checks (Diana)" to the integration-testing SKILL.md, then run the skill for that section, add only those tests and run dotnet test. Do not commit, push or run coverage.`
- **Decisions:**
  - `/health/live` runs no checks (`Predicate = _ => false`), so it answers 200 as long as the process runs; `/health/ready` runs only the checks tagged `ready` (database and `ml-models`). `/health` is unchanged and still runs every check.
  - `MlModelsHealthCheck` reads `MlOptions.ModelsPath` and checks that the three model files exist, the same names `AddCampusMarketMl` loads. The names and the options matched the code, so nothing was renamed.
- **Result:** `Infrastructure/ML/MlModelsHealthCheck.cs` (new), `CampusMarketSetup.cs`, `Program.cs`, README endpoints table, two Postman requests ("Health live (200)", "Health ready (200)"), the skill section, and 4 integration tests (see Run 33 of integration-testing). Build 0 warnings, 0 errors; 10 architecture, 438 unit, 13 ML and 226 integration tests pass.
- **Commits:** code ab246dd; skill section e90123a; tests generated in 341a311 (see Run 33 of integration-testing)

## correlation-id

Week 6, observability (correlation id). Small middleware written by Diana Preutu with Claude Code from the task list; the tests were generated afterwards with the integration-testing skill (Run 34). There are no unit tests: the class is only a middleware, covered by the integration tests.

### Run 1

- **Prompt:** `New task on branch feature/correlation-id. Do it exactly as written: add Infrastructure/Logging/CorrelationIdMiddleware.cs, register it first in Program.cs, turn on IncludeScopes for the console logs in appsettings.json, add a note to the README, append the section "Feature: Correlation id (Diana)" to the integration-testing SKILL.md, run the skill, run the tests with coverage and log the run in PROMPTS.md. Do NOT commit, do NOT push.`
- **Decisions:**
  - The id is taken from `X-Correlation-Id` when the client sends one (so a client or a gateway can follow a request through the logs), otherwise it is a new Guid. It is put in the response header and in a logging scope (`CorrelationId`), so every log line of the request has it.
  - The middleware is the first one in `Program.cs`, before the exception handler and `UseStatusCodePages`, so 401, 403, 404 and 500 answers also have the header.
  - `Logging:Console:IncludeScopes` is `true` in `appsettings.json`: without it the console provider does not print scopes and the id would be in no log line.
  - The value from the client is used as it is (no length or format check), as the task asked.
- **Result:** `Infrastructure/Logging/CorrelationIdMiddleware.cs`, two lines in `Program.cs`, `Logging:Console:IncludeScopes` in `appsettings.json`, a "Correlation id" note in the README, the skill section and 3 integration tests. Build 0 errors, 0 warnings; 438 unit and 225 integration tests pass. Coverage like the CI: every new line and branch in `src/` is covered (`Program.cs` is excluded from the Sonar coverage in the CI).
- **Commits:** code 46c62dd; skill section 1de3dc2; tests generated in 873dccb (see Run 34 of integration-testing)

## listings-cache

Performance change on `GET /api/v1/listings`, written by Diana with Claude Code; the tests were generated afterwards with the integration-testing skill (Run 35). There are no unit tests: the endpoints are covered by the integration tests only.

### Run 1

- **Prompt:** `Cache GET /api/v1/listings for 30 seconds per query string with output caching, tagged "listings"; every endpoint that successfully changes a listing (create, update, delete, publish, reserve, cancel reservation, confirm sale, hide) clears the tag. Add a "Listings cache" note to the README and the section "Feature: Listings cache (Diana)" to the integration-testing skill, then run the skill and dotnet test.`
- **Decisions:**
    - Output caching (`AddOutputCache`, `UseOutputCache` after the rate limiter) with the tag `ListingsCache.Tag`, varied by every query parameter, so each search and page has its own entry. The endpoint is public, so no per-user variation is needed.
    - The cache is cleared in the endpoints, after a successful command and before the success response, not in the handlers: handlers stay free of HTTP concerns and their unit tests do not change. Failure paths do not clear it, because nothing changed.
    - `ReservationExpiryService` changes listings without an endpoint, so its changes can show up to 30 seconds late. Accepted and written in the README.
    - `HideListingEndpoint` needed a `using CampusMarket.Api.Features.Listings` because it is in another namespace.
- **Result:** `ListingsCache.cs` (new), `AddOutputCache` and `UseOutputCache`, `.CacheOutput(...)` on `GetListingsEndpoint`, `IOutputCacheStore` and `ListingsCache.ClearAsync` in eight endpoints. New `Listings/ListingsCacheTests.cs` (3): create and publish, update the title, and reserve, each after a cached search with the same query. Build 0 errors, 0 warnings. 222 integration tests pass; in the first full run `ReservationExpiryTests` failed once with `SQLite Error 5: database is locked`, and the integration tests then passed three times in a row.
- **Problems seen:**
    1. The first build failed: `HideListingEndpoint` could not see `ListingsCache` (missing `using`).
    2. `ReservationExpiryTests.Given_JobOnWithExpiredAndValidReservations_...` failed once with a locked SQLite database; it passed in the three following runs and was not changed.
- **Commits:** code f332e9d; skill section 030e958; tests generated in 9f480ad (see Run 35 of integration-testing)

### Run 2

- **Prompt:** `Sara's review on PR #84: the listings cache tests do not test the cache. ASP.NET Core output cache does not store responses to requests with an Authorization header, and the tests search after AuthenticateAs, so they would pass without ClearAsync.` Then: replace the section "Feature: Listings cache (Diana)" in the skill, run the skill, and check that removing a `ClearAsync` line makes a test fail.
- **Decisions:**
    - No change to production code. The cache works; the tests were wrong. The output cache skips requests with an `Authorization` header, so the searches now use a client without a token and only the changes are sent with one.
    - A new test changes a listing directly in the database and checks that the same search still returns the old title: this proves the cache is used, which the other three tests can only show together with the clearing.
    - Each test first evicts the tag from `IOutputCacheStore`, as the review asked, so the tests never depend on each other.
    - The check was done with `UpdateListingEndpoint`: one endpoint is enough to show that the test fails when the clearing is missing.
- **Result:** `ListingsCacheTests.cs` has 4 tests. Without `ListingsCache.ClearAsync` in `UpdateListingEndpoint`, the update test fails (3 pass); with the line back, all 4 pass. Full run: 10 architecture, 438 unit, 13 ML, 226 integration, 0 warnings. `src/` unchanged (the line was put back).
- **Commits:** skill section a93eb36; tests regenerated in 8631881 (see Run 36 of integration-testing)

## classification-report

Weeks 6–7 (plan of Sara and Anca), Anca's task "Cadrul de evaluare pentru clasificare": confusion matrix and macro-F1 for the category, precision / recall / F1 / PR-AUC for spam, and calibration for both, on the current data. Written by Ancuța Ilenei with Claude Code. The ML tests are written by hand, like the other tests in `CampusMarket.ML.Tests` (the test skills cover the API, not the training project).

### Run 1

- **Prompt:** `Do my next task from the plan: the evaluation framework for classification (Training/Evaluation/ClassificationReport.cs): confusion matrix and macro-F1 for the category, precision, recall, F1 and PR-AUC for spam, calibration (if the model says 90%, is it spam in ~90% of the cases?); it must work on the current data.`
- **Decisions:**
    - `ClassificationReport` works only on rows of (true label, predicted label, probability), with no ML.NET type, so it can be tested on small hand-made examples and reused on any data (the external set, the thresholds task, the model cards).
    - Macro-F1 is the plain mean of the per-class F1, so a small class counts as much as a big one; a class that is neither in the data nor predicted is left out. Precision or recall with an empty denominator is 0.
    - PR-AUC is computed as average precision: rows sorted by probability, precision added each time recall grows; rows with the same probability are taken together so their order does not change the result. Chosen over ROC-AUC for spam because spam is only 18 of the 711 test rows.
    - Calibration: 10 equal bins over [0, 1], mean predicted probability against how often it was true, and ECE (weighted mean gap). For the category the probability is the one of the chosen class and "true" means the prediction was right; for spam it is P(spam) and "true" means the row is spam. Empty bins are kept in the data and skipped in the table.
    - `CategoryTrainer.Report` and `SpamTrainer.Report` turn the scored test rows into a report; the trainers print it after the existing metrics and add `macroF1` and `ece` (category) and `prAuc` and `ece` (spam) to the `testMetrics` of the `.json` files. `check_metrics.py` only checks its known keys, so the new ones are informational.
    - The models were retrained to get the numbers; every existing metric came out identical. The committed `.zip` files and the other metadata were kept and only the four new metrics were added to the two `.json` files: on Windows `listings.csv` is checked out with CRLF, so its SHA-256 differs from the one CI computes on Linux.
- **Result:** new `Evaluation/ClassificationReport.cs`; `CategoryTrainer.cs` and `SpamTrainer.cs` (report, new metrics); `ml/models/category-classification.json` and `spam-detection.json` (four metrics); new `tests/CampusMarket.ML.Tests/ClassificationReportTests.cs` with 17 tests: the confusion matrix, per-class metrics, macro-F1, an unused label, an unknown label, multiclass calibration, binary counts, a higher threshold, average precision (worked example, perfect ranking, no positives, ties), calibration (worked example with ECE 0.2, no rows, zero bins), the Markdown tables, and two bars on the committed models: category macro-F1 at least 0.8 and ECE at most 0.1, spam PR-AUC at least 0.95. README: section "Classification report (category and spam)" with the last confusion matrix, per-class table and calibration, and the category and spam sections brought up to date (the old numbers and "seed 42, 20%" were from before dataset v2). Last run: category macro-F1 0.864, ECE 0.036; spam PR-AUC 1.000, ECE 0.007. Build 0 warnings; 10 architecture, 481 unit, 30 ML and 270 integration tests pass; the SARIF logs of the two ML projects have no finding in the new code.
- **Problems seen:** none.
- **Commits:** code and tests e33d9a9

### Run 2

- **Prompt:** `SonarCloud shows 1 new issue on PR #103: ClassificationReport.cs line 23, "Make this constructor private or simplify its parameters to not use multidimensional/jagged arrays."`
- **Decision:** the public record `MulticlassReport` took the confusion matrix as `int[,]`. It is now `IReadOnlyList<IReadOnlyList<int>>` (one read-only list per true class); the 2D array stays only as a local variable while counting. Same values, same Markdown table.
- **Result:** `ClassificationReport.cs` and the confusion matrix test (compares the rows in order). Build 0 warnings; the 30 ML tests pass.
- **Commits:** code and this entry in the same commit (the one that changes `ClassificationReport.cs`)

### Run 3

- **Prompt:** `SonarCloud now shows 3 issues on PR #103, all on ClassificationReportTests.cs line 30: "Prefer 'static readonly' fields over constant array arguments if the called method is called repeatedly and is not mutating the passed array" (CA1861).`
- **Decision:** the expected rows of the confusion matrix test moved to a `static readonly int[][] ExpectedConfusion` field. Run 2 was not checked with the SARIF build after the change; this time both ML projects were built with `-p:ErrorLog=...` and have no finding outside generated code.
- **Result:** `ClassificationReportTests.cs` only; the 30 ML tests pass.
- **Commits:** test and this entry in the same commit (the one that changes `ClassificationReportTests.cs`)

## validation-thresholds

Weeks 6–7 (plan of Sara and Anca), Anca's task "Praguri alese pe validare": the category minimum confidence and the spam threshold chosen on the validation data and used by the API as settings, not numbers written in the code. Written by Ancuța Ilenei with Claude Code; the API tests were generated with the unit-testing (Run 28) and integration-testing (Run 38) skills, the ML tests written by hand like the other `CampusMarket.ML.Tests`.

### Run 1

- **Prompt:** `Next task from the plan: thresholds chosen on validation. Category minimum confidence: below the threshold, category null. Spam threshold: a balance between spam let through and good listings blocked. The thresholds as settings, not numbers in the code (CategoryTrainer, SpamTrainer, SuggestCategoryHandler, SpamDetector).`
- **Decisions:**
    - `Evaluation/ThresholdSelection.cs` works on the prediction rows of `ClassificationReport` (the task builds on PR #103). Category: candidates 0.30–0.95 in steps of 0.05, the smallest threshold whose shown suggestions are right at least 90% of the time (smallest = most suggestions shown); none → the highest. Spam: candidates 0.05–0.95, the best F1; ties → the middle one, away from both edges. Only the validation part is used; test is measured once at the chosen value.
    - Result: **category 0.60** (0.55 gives 89.2%, 0.60 gives 90.3% with 85.1% coverage; the old 0.7 hid 8 more points of suggestions for about 2 points of accuracy), **spam 0.40** (F1 1.000 from 0.15 to 0.65 on validation; the middle of that range). On test: 85.6% of listings get a category suggestion, right 91.1% of the time; spam precision, recall and F1 1.000 at 0.40.
    - The trainers print the validation table, the test result and the line to copy into `appsettings.json`, and save the value under a new `"settings"` object of the model's `.json` (`ModelMetadata` writes it only when a model has settings, so the price file is unchanged); `accuracyAtMinConfidence`, `coverageAtMinConfidence` and `f1AtThreshold` were added to `testMetrics`.
    - The validation scoring runs on its own `MLContext`: on the shared one, the extra components drew from its random generator and the category and spam models trained differently (log loss 0.471 instead of 0.473). With the separate context every metric is identical to main, so the committed `.zip` files were kept and only the new keys were added to the two `.json` files (the CSV hash differs on Windows because of CRLF).
    - API: `CategorySuggestionOptions` (`Common/Categories/`, `Ml:Category:MinConfidence`) and `SpamDetectionOptions` (`Common/Moderation/`, `Ml:Spam:Threshold`), values in `appsettings.json`, both validated at startup (0–1, `ValidateOnStart`). `SuggestCategoryHandler` reads the first through `IOptions` (Features may not depend on Infrastructure, so the options live in `Common/`); `SpamDetector` decides spam as `Probability >= Threshold` instead of the model's built-in 0.5. The constant `SuggestCategoryHandler.MinConfidence` was removed.
    - A hand-written ML test reads `appsettings.json` and the two model `.json` files and fails when the configured thresholds are not the ones chosen at training, so a retrain cannot silently leave old settings.
    - Coverage: the two range checks share `IsProbability`, so the "too high" test of one setting and the "negative" test of the other cover both sides; `?? new MlOptions()` became `GetValue` with the default, because with an `Ml` section in `appsettings.json` its right side could never run.
- **Checks:** "Banana" gets 0.41 from the real model, still below 0.60. Build 0 warnings; 10 architecture, 484 unit, 41 ML and 275 integration tests pass; SARIF logs of the API, the two ML projects and the unit and integration test projects empty; every new or changed line of the API covered in the merged coverage. The whole Postman collection with newman against a fresh database: 147 requests, 289 assertions, 0 failed.
- **Result:** new `ml/.../Evaluation/ThresholdSelection.cs`; `CategoryTrainer.cs`, `SpamTrainer.cs`, `IModelTrainer.cs` (`Settings`), `ModelMetadata.cs` (`settings` object), `Program.cs`; `ml/models/category-classification.json` and `spam-detection.json`; new `Common/Categories/CategorySuggestionOptions.cs` and `Common/Moderation/SpamDetectionOptions.cs`; `SuggestCategoryHandler.cs`, `SuggestCategoryResponse.cs` (comment), `SpamDetector.cs`, `MlServiceCollectionExtensions.cs`, `appsettings.json`; new `tests/CampusMarket.ML.Tests/ThresholdSelectionTests.cs` (11 tests); README (endpoint table, moderation, section "Decision thresholds (chosen on validation)").
- **Commits:** code ad48202; skill sections 2d54c3f; tests generated in 31f3668 (see Run 28 of unit-testing and Run 38 of integration-testing)

## feature-weights

Weeks 6–7 (plan of Sara and Anca), Anca's task "Explicabilitate": the words that matter most for spam and for each category, as a table in the reports. Written by Ancuța Ilenei with Claude Code; the ML tests are written by hand like the other `CampusMarket.ML.Tests`.

### Run 1

- **Prompt:** `Next task: explainability (Training/Evaluation/FeatureWeights.cs): the words that matter most for spam and for each category, a table in the reports; optionally in the API answer for spam.`
- **Decisions:**
    - Both classifiers are linear, so the explanation is their own weights, not an extra model: `MaximumEntropyModelParameters.GetWeights` for the category (one row per class, the class of each row is the matching slot name of the `Score` column) and `LinearBinaryModelParameters.Weights` for spam (positive towards spam, negative towards a normal listing). The feature names are the slot names of the `Features` column (`TitleFeatures.Word.notite`, `DescriptionFeatures.Word.la|0738`, `...Char.a|b|c`, `PriceFeatures`).
    - Only the word features (single words and pairs) are listed, 10 per side; the groups of three letters are left out because nobody can read them; the price bins of the spam model are shown as one weight. Equal weights keep the slot order, so the tables do not change between runs.
    - A freshly trained pipeline is a `TransformerChain<KeyToValueMappingTransformer>` while a loaded one is a `TransformerChain<ITransformer>`; the first version cast to the second and the training stopped with an `InvalidCastException` after the price model. The model is now read as a sequence of transformers, which works for both.
    - The trainers print the tables after the report and the thresholds. Reading the weights and the schema does not touch the random generator of the shared `MLContext`: after the change every metric of the three models is identical to main (checked on freshly written `.json` files), so the committed models and metadata were kept unchanged.
    - The optional part (the words in the API answer for spam) was not done: the API only has the prediction engine, not the weights, and the model card task can decide whether it is worth it.
    - The README section says what the tables show, checked on the data: most words are the expected ones, the category model decides almost only from the title, "-" is the strongest Textbook word because 32% of the generated textbook titles are "Title - Author" (under 1% in the other categories), and spam is recognised from moving the talk elsewhere ("pe telegram", "link in", "suna", "scrie-mi"), which explains why the subtle spam of the external set is missed.
- **Checks:** build 0 warnings; 10 architecture, 484 unit, 51 ML and 275 integration tests pass; the SARIF logs of the two ML projects have no finding.
- **Result:** new `ml/.../Evaluation/FeatureWeights.cs`; `CategoryTrainer.cs` and `SpamTrainer.cs` (print the tables); new `tests/CampusMarket.ML.Tests/FeatureWeightsTests.cs` with 10 tests (reading a word, a pair, a letter group and the price feature; the highest and the lowest words; the count; equal weights; the length check; the Markdown tables; the committed category model has 10 positive words in decreasing order for each of the five categories, with "notite" for Notes and "manual" for Textbook; the committed spam model has 10 positive and 10 negative words, with "telegram" and "link" towards spam); README section "Explainability (which words matter)".
- **Commits:** code and tests 031b583

## model-cards

Weeks 6–7 (plan of Sara and Anca), Anca's task "Analiza erorilor + model cards": 10–20 wrong examples explained and one page per model (what it does, data, metrics, limits, when not to use it), in `docs/ml/category.md` and `docs/ml/spam.md`. Written by Ancuța Ilenei with Claude Code; the ML tests are written by hand like the other `CampusMarket.ML.Tests`.

### Run 1

- **Prompt:** `Next task from week 2: error analysis + model cards. 10–20 wrong examples, explained. One page per model: what it does, data, metrics, limits, when it must not be used (docs/ml/category.md, docs/ml/spam.md).`
- **Decisions:**
    - The examples come from code, not from a one-off copy: `Evaluation/ErrorAnalysis.cs` lists the category mistakes with the highest confidence (the ones the API really shows) and, for spam at the chosen threshold, the normal listings flagged and the spam let through. The training program prints them for the test part and the external set after every retrain.
    - Every explanation in the cards was checked on the data before writing it: the deliberate ~3% wrong labels of dataset v2 (`ml/data/README.md`) and how often the same title has each label (the Arduino kit is `LabEquipment` 11 times out of 12), the vocabulary of real notes ("grile", "materie", "sinteze" occur 0 times in `listings.csv`), "laptop" in 27 `Other` accessory titles, the dash artefact from the explainability task, and the `FeaturizeText` defaults (diacritics removed, punctuation kept glued to the word). Two first explanations were wrong and were corrected: "Lenovo is unknown" (it occurs 28 times, all `Electronics`) and "punctuation is dropped".
    - Category: 8 of the 13 most confident test mistakes are deliberate wrong labels (the model is right), 3 sit between Textbook and Notes, 2 are real mistakes (LEDs, nitrile gloves). On the external set the gaps are the exam-preparation vocabulary of real notes and the dash artefact. At 0.60 the external set gets a suggestion for 64.7% of the listings, right 80.3% of the time (test: 85.6% and 91.1%).
    - Spam: no mistake on the test part (saturated). External at 0.40: TP 10, FP 1, TN 101, FN 10 (precision 0.909, recall 0.500, PR-AUC 0.852); the flagged listing is a pair of hiking boots whose description contains "pe", the strongest spam word; the ten missed are scams that work through the payment (advance transfer, PayPal Friends & Family, crypto) and never ask to move the talk to another app. At 0.50 the boots would not be flagged; the card says the threshold should be chosen again on data that is not saturated.
    - The card numbers come from the committed models (the ones the API serves). A spam model retrained on this machine has the same test metrics but gives the boots 0.45 instead of 0.42; a stale CRLF `listings.csv` in the working copy (checked out before `.gitattributes`) was suspected and fixed, but the difference stayed, so the card says the last digits can move after a retrain. The committed models and metadata are unchanged.
- **Checks:** build 0 warnings; 10 architecture, 484 unit, 59 ML and 275 integration tests pass; the SARIF logs of the two ML projects have no finding; a full training run prints the same category lists as the cards and the same spam rows.
- **Result:** new `docs/ml/category.md` and `docs/ml/spam.md`; new `ml/.../Evaluation/ErrorAnalysis.cs`; `Program.cs` (error analysis after the external evaluation, with the thresholds chosen at training); new `tests/CampusMarket.ML.Tests/ErrorAnalysisTests.cs` with 8 tests (only wrong predictions, most confident first; ties and count; flagged and missed spam split at the threshold, inclusive; a higher threshold; the Markdown tables and the empty case; pipes and long descriptions; ten wrong category predictions on the committed model; no spam mistake on the test part at 0.40); README links to both cards.
- **Commits:** code, tests and docs 2696ed0

## similar-listings

Weeks 6–7 (plan of Sara and Anca), Anca's task "Model nou: listări similare": `GET /api/v1/listings/{id}/similar` with 5 similar Active listings, evaluated by how many of the first 5 are of the same category, with the same structure as the other models (trainer, interface, implementation, endpoint, tests, `docs/ml/similar.md`). Written by Ancuța Ilenei with Claude Code; the API tests were generated with the unit-testing (Run 29) and integration-testing (Run 40) skills, the ML tests written by hand like the other `CampusMarket.ML.Tests`.

### Run 1

- **Prompt:** `Next task: new model, similar listings. GET /api/v1/listings/{id}/similar: 5 similar active listings. Evaluation: how many of the first 5 are of the same category. Same structure: trainer, interface, implementation, endpoint, tests (SimilarTrainer.cs, ISimilarListingsFinder.cs, Features/Listings/GetSimilarListings/, docs/ml/similar.md).`
- **Decisions:**
    - The model is a text-to-vector pipeline learnt on the train part: `FeaturizeText` on the title (and the description), concatenated, `NormalizeLpNorm` to unit length, so the dot product is the cosine similarity. There is no label: similarity is judged by the category of the closest listings. Non-spam rows only.
    - Evaluation (`Evaluation/SimilarityEvaluation.cs`): precision@5 over sparse vectors (only the non-zero entries are compared). Listings with the same `TemplateId` are left out of each other's candidates: they are variations of the same item and would make the score trivial. The reference is the expected precision of 5 listings picked at random among the same candidates.
    - Two variants chosen on validation: title only 0.597, title + description 0.672 (kept). Test: 0.668 against 0.202 for a random pick. The trainer runs after the other three, so it changes nothing in their training (their metrics are identical to main after the run; their committed files were kept), and writes the new `similar-listings.zip` and `.json`.
    - API: `ISimilarListingsFinder` in `Common/Similarity/` returns one similarity per candidate; `SimilarListingsFinder` (Infrastructure/ML) gets the vectors from a `PredictionEnginePool<SimilarInput, SimilarVector>` and returns their dot product clamped to 0–1. The handler loads the target (any status, like `GET /listings/{id}`; unknown → 404 `Listing.NotFound`), takes every other Active listing, and keeps the five highest; ties by `CreatedAt` (newest first) then `Id`, the similarity rounded to two decimals. The ranking stays in the handler so it is unit tested without the model.
    - The fourth model file is checked by the ready health check (`MlModelsHealthCheck`) and by the Docker build, like the other three; the csproj already copies every `ml/models/*.zip`.
    - Postman: a `GetSimilarListings` folder (the published listing → at most 5, not itself, similarity 0–1 in decreasing order; a random id → 404), inserted as text so the rest of the collection keeps its formatting.
- **Checks:** build 0 warnings; 10 architecture, 491 unit, 61 ML and 280 integration tests pass; the new code of the API covered in the merged coverage (the files still uncovered are from #95, #96 and the profile PR); SARIF logs of the API and the two ML projects empty; newman against a fresh database: 149 requests, 293 assertions, 0 failed.
- **Result:** new `ml/.../SimilarTrainer.cs`, `ml/.../Evaluation/SimilarityEvaluation.cs`, `ml/models/similar-listings.zip` and `.json`; `Program.cs` (one line in the trainer list); new `Common/Similarity/ISimilarListingsFinder.cs`, `Infrastructure/ML/SimilarInput.cs`, `SimilarVector.cs`, `SimilarListingsFinder.cs`, `Features/Listings/GetSimilarListings/` (request, query, response, handler, endpoint); `ListingsEndpoints.cs`, `MlServiceCollectionExtensions.cs`, `MlModelsHealthCheck.cs`, `Dockerfile`; new `tests/CampusMarket.ML.Tests/SimilarListingsTests.cs` (10 tests: sparse vectors, dot and cosine, an empty vector, precision@k and the random pick on a worked example, items without candidates, the quality bar of the committed model, three hand-written pairs); new `docs/ml/similar.md`; README (endpoint, model section, Docker and ready check); Postman.
- **Commits:** code 17bdcfc; skill sections 7cb4df8; tests generated in cfb9e5a (see Run 29 of unit-testing and Run 40 of integration-testing)

## cors-render

The deployed frontend (`https://campus-app-pucf.onrender.com`) could not read any answer of the deployed API (`https://campus-app-latest.onrender.com`): the API sent no `Access-Control-Allow-Origin` for that origin. Written by Ancuța Ilenei with Claude Code.

### Run 1

- **Prompt:** `Sara asked: add the frontend URL https://campus-app-pucf.onrender.com to the CORS origins, check AllowAnyHeader/AllowAnyMethod (and AllowCredentials if cookies are used) and that UseCors runs before UseAuthorization and the endpoints.`
- **Decisions:** the policy already allowed any header and method, ran first in the pipeline and needed no credentials (the token goes in the `Authorization` header), so only the origin was missing. It is added to `Cors:AllowedOrigins` in `appsettings.json`, so it is deployed with the image and needs no setting in Render; `localhost:5173` stays for development. No new test: `CorsTests` already checks that the configured origins are allowed and the others are not.
- **Checks:** against the deployed API, a request with the Render origin got no CORS header. Locally with the new config: preflight and GET from the Render origin and from `localhost:5173` get `Access-Control-Allow-Origin`, another origin does not; 275 integration tests pass.
- **Result:** `appsettings.json` (the origin), README (the two places that list the allowed origins).
- **Commits:** code add985e

## conversations

Weeks 6–7 (plan of Sara and Anca), Anca's task "Mesaje între cumpărător și vânzător": a conversation about a listing, visible only to the two of them (another member → 403); send a message, read the messages, see my conversations. Written by Ancuța Ilenei with Claude Code; the tests were generated with the unit-testing (Run 30) and integration-testing (Run 41) skills. The chat page in the frontend comes in a separate PR.

### Run 1

- **Prompt:** `Next task from the plan: messages between buyer and seller. Conversation on a listing, visible only to the two (another member → 403). Send a message, see the messages, see my conversations. Domain/Conversations/, Features/Conversations/ (new).`
- **Decisions:**
    - Domain: `Conversation` (listing, seller, buyer, `CreatedAt`, `LastMessageAt`) owns its `Message`s, like `Listing` owns its reservations. The seller is taken from the listing, so a conversation has exactly two participants. `Start` refuses the seller's own listing and a listing that cannot be bought (only `Active` or `Reserved`); `Send` refuses anyone who is not one of the two (`ErrorType.Forbidden` → 403, an Admin too: the plan says only the two), an empty text and a text over 1000 characters (after trim).
    - Each message has a `Number` (1, 2, 3…): the order stays the same when two messages have the same time, and the tests never depend on the clock.
    - One conversation per buyer and listing (unique index). `POST /listings/{id}/conversations` adds the message to the existing conversation instead of answering 409, so the "write to the seller" button on the listing page works the same the first and the second time; the answer is always 201 with the message and `Location` to the conversation. An existing conversation keeps working after the listing is reserved or sold (to arrange the hand-over).
    - `GET /members/me/conversations` lists the conversations as buyer and as seller, with the other member's name and the last message, the latest first; no paging (a student has few conversations).
    - Cascade delete from the listing and from the two members, like reviews; no foreign key on the message sender (always one of the two).
    - Migration `AddConversations` (two new tables only). #108 and #111 have migrations too: the PR merged last regenerates its migration.
    - Postman: a `Conversations` folder (13 requests) that creates and publishes its own listing, so it does not depend on the state left by the other folders.
- **Checks:** build 0 warnings; 10 architecture, 541 unit, 69 ML and 309 integration tests pass; the new code covered in the merged coverage (an unused `DbSet<Message>` was removed after the coverage run; `has-pending-model-changes` confirms the migration still matches); SARIF of the API: only CA1861 in the migrations, as in the older ones (Sonar leaves the migrations out); newman against a fresh database: 162 requests, 318 assertions, 0 failed; the whole flow tried by hand on a running API first.
- **Result:** new `Domain/Conversations/` (`Conversation.cs`, `Message.cs`, `IConversationRepository.cs`), `Features/Conversations/` (four slices, `MessageResponse`, `SendMessageRequest`, `ConversationsEndpoints.cs`), `Infrastructure/Persistence/EfConversationRepository.cs`, `ConversationConfiguration.cs`, `MessageConfiguration.cs`, migration `AddConversations`; `CampusMarketDbContext.cs`, `CampusMarketSetup.cs` (repository and endpoints); README (use cases, endpoint table, a section); Postman.
- **Commits:** code 0993bf8; skill sections 64131b7; tests generated in b9400fc (see Run 30 of unit-testing and Run 41 of integration-testing)

## listing-photos

Weeks 6–7 (plan of Sara and Anca), Anca's task "Poze la listări": upload only jpg, png or webp, at most 2 MB, at most 5 per listing, only the seller; the files on disk (the Docker volume), only the name in the database. Written by Ancuța Ilenei with Claude Code; the tests were generated with the unit-testing (Run 31) and integration-testing (Run 42) skills. The photos on the listing page come with the frontend pages.

### Run 1

- **Prompt:** `Next task: photos on listings. Upload only jpg, png or webp, max 2 MB, max 5 per listing; only the seller. Files on disk (Docker volume), only the name in the database. Domain/Photos/, Features/Photos/, Common/Storage/IPhotoStorage.cs, Infrastructure/Storage/ (new).`
- **Decisions:**
    - The format is read from the first bytes of the file (`PhotoFormat.Detect`: JPEG `FF D8 FF`, PNG signature, WebP `RIFF…WEBP`), never from the file name or the content type the browser sends, which anyone can change. The stored name is `<photoId>.<ext>`, made by the domain, so a user-given name never reaches the disk.
    - The endpoint reads at most 2 MB + 1 byte of the upload: one byte more is enough to answer `Photo.TooLarge`, and a big file is never held in memory.
    - `Photo.Create` checks in this order: the seller (403), the 5-photo limit, empty, size, format. Any listing status (the seller can prepare a draft with photos).
    - Storage: `IPhotoStorage` in `Common/Storage/`, `LocalPhotoStorage` in `Infrastructure/Storage/`, one folder per listing (`<PhotosPath>/<listingId>/`), so deleting a listing removes all its files at once (`DeleteListingHandler` calls `DeleteListing` after the commit; the rows go by cascade). `Storage:PhotosPath` = `photos` locally (git-ignored), `/app/data/photos` in the Dockerfile, inside the volume of the database.
    - Order of the writes: upload saves the file, then the row; delete removes the row, then the file. If the second step fails, an unused file is left, never a row without its file. `GET /photos/{id}` also answers 404 when the row exists but the file is missing.
    - `GET /api/v1/photos/{id}` is public (an `<img src>` sends no token) with `Cache-Control: public, max-age=86400` (a photo never changes). `DELETE …/photos/{photoId}` was added so the seller can replace a photo once the 5 are used.
    - The upload endpoint has `DisableAntiforgery()`: the token is in the `Authorization` header, no cookies.
    - Migration `AddPhotos` (one new table).
    - Postman: a `Photos` folder (11 requests) with a real 1×1 PNG in `postman/files/listing-photo.png` (path relative to the repository root, where newman is run).
- **Checks:** build 0 warnings; 10 architecture, 581 unit, 69 ML and 338 integration tests pass; the new code covered in the merged coverage; SARIF of the API clean outside the migrations; newman against a fresh database: 173 requests, 338 assertions, 0 failed; the whole flow tried by hand on a running API (three formats, a GIF renamed `.png`, 2 MB + 1, the sixth photo, another member, delete, the folder gone with the listing, a JSON body → 415).
- **Result:** new `Domain/Photos/` (`Photo.cs`, `PhotoFormat.cs`, `IPhotoRepository.cs`), `Common/Storage/` (`IPhotoStorage.cs`, `PhotoStorageOptions.cs`), `Infrastructure/Storage/LocalPhotoStorage.cs`, `Infrastructure/Persistence/EfPhotoRepository.cs`, `PhotoConfiguration.cs`, migration `AddPhotos`, `Features/Photos/` (four slices, `PhotoResponse`, `PhotosEndpoints.cs`); `DeleteListingHandler.cs`, `CampusMarketDbContext.cs`, `CampusMarketSetup.cs`, `Dockerfile`, `.gitignore`; README (use cases, endpoint table, a section, the Render note); Postman.
- **Commits:** code f4520ff; skill sections 59b3660; tests generated in 237ab25 (see Run 31 of unit-testing and Run 42 of integration-testing)

### Run 2 (merge with main after #108, #117, #119, #80)

- **Prompt:** `#120 has conflicts after Sara's PRs went in; check everything that changed and make it all work.`
- **Decisions:**
    - Conflicts kept both sides (registrations, `DbSet`s, `.gitignore`, skills, Postman). The `AddPhotos` migration was regenerated on top of `AddPasswordResetTokens` from #108 (same content: one `Photos` table); `has-pending-model-changes` is clean.
    - Compared with the main before these merges (`4568297`), some content was lost in earlier conflict resolutions and is put back unchanged: the "Feature: Similar listings (Anca)" sections of both test skills; unit-testing Runs 21–23 in this file (before "Skill update v25 → v26"); 11 Postman requests (filter by condition, public profile, my profile, health live/ready) with their folders; the `sellerId` line of "Create listing - valid" and the `sellerId`, `convListingId`, `conversationId` collection variables. Every Postman request of main is unchanged.
- **Checks:** build 0 warnings; 10 architecture, 605 unit, 69 ML and 346 integration tests pass; newman against a fresh database: 175 requests, 342 assertions, 0 failed.
- **Commits:** the merge commit of this run

## listings-sort

Small extension of `GET /api/v1/listings`, written by Diana with Claude Code on top of the condition filter; the tests were generated afterwards with the unit-testing (Run 26) and integration-testing (Run 30) skills.

### Run 1

- **Prompt:** `Add a sort query parameter to GET /api/v1/listings (newest by default, price_asc, price_desc), with 400 Listings.InvalidSort for any other value. Update the README and the Postman collection, add the sections "Feature: Sorting (Diana)" to the two skills, run the skills for that section and dotnet test.`
- **Decisions:**
    - `Sort` is a string checked against `GetListingsQuery.AllowedSorts`, not an enum: an unknown value then reaches the handler and gets the usual `Error` body with `Listings.InvalidSort`, like `Listings.InvalidPage`. With an enum, ASP.NET Core would answer with its own `Request.Invalid` ProblemDetails.
    - The check comes right after the price range check, before the repository is read, so an invalid request never touches the data.
    - Every order ends with `ThenBy(l => l.Id)`, so the same query always returns the same pages even when prices are equal. `totalCount` and paging are unchanged; the sort is applied before paging.
    - No default value in the endpoint: `null` means `newest`, the same order as before.
- **Result:** `GetListingsQuery.Sort` and `AllowedSorts`, the `sort` parameter in `GetListingsEndpoint`, the check and the `switch` in `GetListingsHandler`. README: the `sort` row in the search table, the `Listings.InvalidSort` rows in both error tables. Postman, folder GetListings: "Get listings - sorted by price (200)" and "Get listings - unknown sort (400)". Unit tests (5 test methods, 7 cases): price ascending and descending, `newest` and no sort, equal prices by `Id`, unknown sort with the exact message. Integration tests (2): `?sort=price_asc` and `?sort=abc`. Build 0 errors, 0 warnings; all tests pass (442 unit, 223 integration).
- **Commits:** code 42998f7; skill sections b5da1ba; tests generated in 2d27370 (see Run 26 of unit-testing and Run 30 of integration-testing)

## notifications

Sara's notifications (#111), brought up to date and finished by Ancuța Ilenei with Claude Code at Sara's request: the tests generated with the skills (unit Run 33, integration Run 44), the merge with main and the fixes the tests and the coverage showed.

### Run 1

- **Prompt:** `Run the unit and integration skills on PR 111 (Sara's notifications), then bring it up to date with main and make everything work.`
- **Decisions:**
    - `EfNotificationRepository.GetByMemberAsync`: sort in memory (SQLite cannot order by `DateTimeOffset`; found by the first integration run).
    - Merge with main (conversations, password reset, photos): both sides kept; the migration `AddNotifications` regenerated on top of `AddPhotos` (one `Notifications` table); `has-pending-model-changes` is clean. The Postman collection keeps every request of main plus the three notification requests.
    - `MarkNotificationReadEndpoint`: the `_ => BadRequest` branch was removed, because the handler's only failure is `Notification.NotFound`; the endpoint answers 204 or 404.
    - Two integration tests added through the skill (row 2 of the integration run) for the list order and the paging errors.
- **Checks:** build 0 warnings; 10 architecture, 654 unit, 69 ML and 361 integration tests pass; the new notification code covered in the merged coverage; SARIF of the API clean; newman against a fresh database: 178 requests, 346 assertions, 0 failed.
- **Commits:** 4a1b0bb (fix), 4d5a11a (tests), c288b04 (merge with main, endpoint, row 2 tests), this PROMPTS commit

## frontend-browse

Weeks 6–7 (plan of Sara and Anca), Anca's frontend pages: home and search with filters and paging; the listing page with photos, similar listings, report and favorite buttons; messages (chat); my favorites; reviews on the seller's profile; Admin: the spam reports and hiding listings. (Publish and edit with the suggestions were already done by Sara in #117.) Written by Ancuța Ilenei with Claude Code.

### Run 1

- **Prompt:** `Next: my frontend pages from the plan (search with filters and paging, listing page with photos, similar, report and favorite, chat, my favorites, seller reviews, admin moderation). Same structure and look as the skeleton and Sara's pages.`
- **Decisions:**
    - Pages in the folders of the plan: `features/listings/` (search, listing page, card, photo controls; added next to Sara's files, her files unchanged), `features/messages/`, `features/favorites/`, `features/reviews/` (seller profile), `features/moderation/`; one `routes.tsx` each, one line each in `router.tsx`, new links in `menu.ts`.
    - Search: the filters (`search`, `category`, `condition`, `minPrice`, `maxPrice`, `sort` from #86) and the page are in the address, so a search can be shared and the back button works. Cards show the first photo of the listing, or a tile in the category colour.
    - Listing page: the buyer reserves, saves, writes to the seller (the first message opens the conversation) and reports; the seller adds and removes photos (`FormData` upload; `api/client.ts` now leaves the multipart `Content-Type` to the browser) and gets a link to Sara's edit page. Whether a listing is in my favorites is read from the first 100 favorites (there is no single-listing check).
    - Chat: the conversation reloads every 5 seconds (real time with SignalR is one of the extras, not done).
    - Romanian messages for the error codes of these pages in `app/errorMessage.ts`.
    - The menu: links stay on one line (the phone menu below 1180px, since a logged-in member has ten links); the "Ieșire" link was removed because the top bar already has the logout button (noted in the review of #115).
    - Every effect loads with `.then` and an `AbortController`; a change (hide, remove, send, photo) bumps a counter to load again, so the linter has nothing to say.
- **Checks:** `npm run build` and `npm run lint` clean. On a running API with seeded data, screenshots of every page (guest, buyer, seller, Admin; desktop and 600px) and a headless Chrome script that clicks the real buttons: save / unsave, reserve, write to the seller (lands in the conversation), report, upload and delete a photo, answer in the chat, Admin hides a listing, search sorted by price. All pass.
- **Result:** new `app/errorMessage.ts`, `features/listings/` (`BrowsePage`, `ListingPage`, `ListingCard`, `LatestListings`, `PhotoManager`, `photoSrc`, `publicTypes`, `browse.css`), `features/messages/`, `features/favorites/`, `features/reviews/`, `features/moderation/`; `api/client.ts`, `app/router.tsx`, `app/menu.ts`, `app/layout.css`, `app/pages/HomePage.tsx` (the newest listings under the hero) and `home.css`, `features/listings/routes.tsx`, `frontend/README.md` (pages table, upload, error texts).
- **Commits:** code 86a8e07

## demo-polish

The evening before the demo, Sara listed what did not look or work right: the pagination, the notifications, the messages, the Admin pages with a horizontal scroll, the categories page, and a spam listing that was not caught. Fixed by Ancuța Ilenei with Claude Code; the spam rules tests generated with the integration-testing skill (Run 47).

### Run 1

- **Prompt:** `Sara says: the pagination looks bad (the last row is not full), notifications don't work, the messages all look like they come from the same person, the admin pages scroll sideways, the categories page is unclear, and a spam listing was not caught. Check everything and make it all work.`
- **Findings and decisions:**
    - Messages and notifications work: tried with two separate users, the bubbles and the notification go to the right person. The login is kept in the browser (`localStorage`), so two tabs of the same browser are always the same account; the second account has to be in a private window or another browser. Nothing to change in the code; the notifications were in English (the API's texts), so `features/notifications/messageText.ts` turns the three known shapes into Romanian on the page (the API and its tests keep the English text).
    - The horizontal scroll: an Admin had 12 links in the top bar. The top bar now keeps Acasă, Anunțuri, Publică anunț, Mesaje, Notificări; the personal pages and the Admin pages are in a menu that opens from the avatar (with Ieșire), and in the phone menu below 900px (`menu.ts` has `place: 'account'`).
    - The pagination: the grid has 4 columns on a wide screen (3 and 2 on narrower ones), so the 12 listings of a page and the 8 on the home page fill whole rows; the listing page shows 4 similar listings (one row).
    - The categories page: a clearer explanation of what the names do and why the 5 categories are fixed.
    - Spam: the model (trained on generated data) catches "Vand cont Netflix… whatsapp" but misses short real-world spam ("click aici www…", "telegram @…", "plata in avans", "!!!"). `SpamDetector` now also flags a listing when a clear rule matches (`SpamRules`: phrases, links, Romanian phone numbers, `@handles`, `!!!`; diacritics removed, email addresses ignored), with probability 0.9 for the Admin. Clean listings (with an email in the description too) and the demo seed listings are not flagged. A flagged listing still appears in the search until an Admin hides it, as before (the model only reports, the Admin decides).
- **Checks:** `npm run build` and `npm run lint` clean; build 0 warnings; 10 architecture, 720 unit, 69 ML and 404 integration tests pass; SARIF of the API clean; on a running API with seeded data: two users chat and get notifications correctly, the Admin's top bar has 5 links and no horizontal scroll, the avatar menu opens and navigates (clicked in headless Chrome), 11 hand-picked texts (7 spam, 4 clean) all classified as expected.
- **Result:** new `Infrastructure/ML/SpamRules.cs`, `frontend/src/features/notifications/messageText.ts`; `SpamDetector.cs`, `frontend/src/app/Layout.tsx`, `menu.ts`, `layout.css`, `features/listings/browse.css`, `ListingPage.tsx`, `features/notifications/NotificationsPage.tsx`, `features/admin-categories/AdminCategoriesPage.tsx`; README and `docs/ml/spam.md` (the rules).
- **Commits:** code a727e25; skill section 1cba445; tests generated in 4912528

