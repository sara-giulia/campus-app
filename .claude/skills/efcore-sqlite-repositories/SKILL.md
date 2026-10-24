---
name: efcore-sqlite-repositories
description: Replaces the in-memory repositories of CampusMarket with EF Core and SQLite. Use it when you work on the database or the repositories.
---

# EF Core + SQLite for CampusMarket

We want to save the data in a SQLite database instead of in memory. The handlers and endpoints should keep working without changes.

## What to do

- Add the package `Microsoft.EntityFrameworkCore.Sqlite` (version `10.0.*`) to `src/CampusMarket.Api`.
- Create a `CampusMarketDbContext` in `Infrastructure/Persistence/` with a `DbSet<Listing>` and a `DbSet<Member>`.
- Create `EfListingRepository` and `EfMemberRepository` that implement `IListingRepository` and `IMemberRepository`.
- Create `EfUnitOfWork` that implements `IUnitOfWork` and calls `SaveChangesAsync` on the DbContext.
- Register everything in `CampusMarketSetup` instead of the in-memory classes.

## Configuration and startup

- Keep the signature `AddCampusMarket(this IServiceCollection services)` exactly as it is. Do not add parameters.
- The connection string lives in `appsettings.json`, never in the code:

  ```json
  "ConnectionStrings": { "CampusMarket": "Data Source=campusmarket.db" }
  ```

- Read it when the DbContext is registered:

  ```csharp
  services.AddDbContext<CampusMarketDbContext>((sp, options) =>
      options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("CampusMarket")));
  ```

- `MapCampusMarket` only maps routes. The database is created in a separate method in `CampusMarketSetup`:

  ```csharp
  public static WebApplication EnsureCampusMarketDatabase(this WebApplication app)
  ```

  It opens a scope, gets the `CampusMarketDbContext` and calls `Database.EnsureCreated()`. `Program.cs` calls it right after `builder.Build()`.

## Old in-memory code

- Delete `Infrastructure/InMemoryListingRepository.cs`, `Infrastructure/InMemoryMemberRepository.cs` and `Infrastructure/InMemoryUnitOfWork.cs`. Nothing uses them after the switch, so they would be dead code.

## Mapping

- One configuration class per entity, in `Infrastructure/Persistence/Configurations/`: `ListingConfiguration`, `ReservationConfiguration`, `MemberConfiguration`, each implementing `IEntityTypeConfiguration<T>`.
- `OnModelCreating` only calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusMarketDbContext).Assembly)`.
- Every `Id` is the primary key with `ValueGeneratedNever()`, because the domain creates the Guid.
- `Listing.Title` is required with max length `Listing.MaxTitleLength`. `Member.Name` is required with max length 100.
- `Listing.Version` is a concurrency token (`IsConcurrencyToken()`).
- `Money` and `Email` are value objects, store them as a single column.
- Store enums as text.
- A listing has a list of reservations.

## Rules

- Do not change the handlers or the endpoints.
- Keep the same behaviour as the in-memory repositories: `GetByIdAsync` returns `null` when missing, `AddAsync` returns `false` for a duplicate Id.

## Example

```csharp
public sealed class EfMemberRepository(CampusMarketDbContext db) : IMemberRepository
{
    public async Task<bool> AddAsync(Member member, CancellationToken ct = default)
    {
        if (await db.Members.AnyAsync(m => m.Id == member.Id, ct))
            return false;

        db.Members.Add(member);
        return true;
    }

    // GetByIdAsync, GetAllAsync, Update and Delete in the same style
}
```

