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
- **Change to SKILL.md:** none; v2 is the final version.
- **Commits:** skill a90b234; generated code 9c099c5

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

---

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

---

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


