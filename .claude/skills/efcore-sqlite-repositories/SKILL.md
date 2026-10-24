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
- Use the connection string `Data Source=campusmarket.db`.

## Mapping

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