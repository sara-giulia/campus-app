---
name: cqrs
description: Turns CampusMarket slices into CQRS commands, queries and handlers (hand-written, no MediatR) that return Result<T> and use IUnitOfWork. Use when adding or changing a command, a query or a handler, or when an endpoint still talks to a repository directly.
---

# CQRS skill for CampusMarket

## Project facts (do not change these)

- .NET 10 minimal API in `src/CampusMarket.Api`, root namespace `CampusMarket.Api`.
- `Result<T>` and `Error` (with `ErrorType`: `Validation`, `NotFound`, `Conflict`) already exist in `Domain/Common/`. Reuse them as they are.
- Repository interfaces live next to the domain class (`IListingRepository`, `IMemberRepository`). `AddAsync` returns `Task<bool>` (false = duplicate Id), `GetByIdAsync` returns `null` when missing, `GetAllAsync` returns a snapshot, `Update` and `Delete` take the loaded entity.
- Slices live in `Features/<Resource>/<UseCase>/`, routes are under `/api/v1`.
- Behaviour must not change: same routes, same status codes, same JSON, same error bodies.

## 1. Shared pieces in `Common/CQRS/` (namespace `CampusMarket.Api.Common.CQRS`)

Create these once. If a file already exists, leave it as it is.

| File | Content |
|---|---|
| `ICommand.cs` | `public interface ICommand<TResult> { }` (empty marker) |
| `IQuery.cs` | `public interface IQuery<TResult> { }` (empty marker) |
| `ICommandHandler.cs` | `ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>` with `Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken ct)` |
| `IQueryHandler.cs` | `IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>` with `Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct)` |
| `HandlerRegistrationExtensions.cs` | `AddHandlers(this IServiceCollection services, Assembly assembly)` |

`AddHandlers` rules:

- Scan the assembly by reflection for classes that are not abstract.
- For every implemented interface whose generic definition is `ICommandHandler<,>` or `IQueryHandler<,>`, register it with `services.AddTransient(interfaceType, implementationType)`.
- Handlers are always Transient.
- No mediator library (no MediatR, no Mediator, no similar package).

Wire it in `CampusMarketSetup.AddCampusMarket`:

```csharp
services.AddHandlers(typeof(CampusMarketSetup).Assembly);
services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
```
### Decision: handler signature

`HandleAsync` returns `Task<Result<TResult>>` and takes `CancellationToken ct` without a default value. The course uses `Task<TResult>` and `CancellationToken cancellationToken = default`; we do not. Reasons: a handler cannot forget to return a `Result` for an expected failure, and every caller must pass the token on. Both differences are intentional.

## 2. Unit of work

- `IUnitOfWork` lives in `Domain/Common/` (namespace `CampusMarket.Api.Domain.Common`) with one method: `Task<int> CommitAsync(CancellationToken ct);`
- **Decision:** `CommitAsync` returns `Task<int>`, like EF Core, so issue #2 can swap the implementation without touching any handler. The course code returns `Task<Guid>`; we do not.
- For now the implementation is `Infrastructure/InMemoryUnitOfWork.cs`: it does nothing and returns `Task.FromResult(0)`, because the data is already saved in the in-memory dictionaries. Registered as Singleton (like the in-memory repositories). Issue #2 replaces it with `EfUnitOfWork` (Scoped).
- Command handlers call `CommitAsync` once, after `Add`, `Update` or `Delete`, and only when everything before it succeeded.
- Query handlers never receive `IUnitOfWork` and never call `CommitAsync`.

## 3. Folder layout and naming

One folder per use case, keeping the current slice folders. Keep the existing request and response records unchanged (same names, same fields).

Command slice (`CreateListing`, `UpdateListing`, `DeleteListing`, `CreateMember`, `UpdateMember`, `DeleteMember`):

```
Features/Listings/CreateListing/
  CreateListingRequest.cs     # HTTP body (already exists)
  CreateListingCommand.cs     # internal command
  CreateListingHandler.cs
  CreateListingResponse.cs    # only if the slice returns a body
  CreateListingEndpoint.cs
```

Query slice (`GetListingById`, `GetListings`, `GetMemberById`, `GetMembers`):

```
Features/Listings/GetListingById/
  GetListingByIdRequest.cs    # route parameters (only if the slice has input)
  GetListingByIdQuery.cs
  GetListingByIdHandler.cs
  GetListingByIdResponse.cs
  GetListingByIdEndpoint.cs
```

Naming: `<UseCase>Command`, `<UseCase>Query`, `<UseCase>Handler`. Commands and queries are `sealed record` types. Handlers are `sealed class` types with constructor injection of interfaces only.

What each handler returns (`TResult`):

| Slice type | `TResult` |
|---|---|
| Create | the response record (for example `CreateListingResponse`) |
| Update and Delete | `Guid`, the id of the entity that was changed (the endpoint ignores it and answers 204) |
| GetById and GetAll | the response record |

`GetListings` and `GetMembers` have no input: their query is an empty record (`GetListingsQuery()`), and they have no request record.

## 4. Handler rules

- The handler does the work: it calls the domain (`Listing.Create`, `listing.UpdateDetails`, ...), uses the repository through its interface and, for commands, calls the unit of work.
- It returns `Result<T>` for every expected failure and never throws for them:
  - entity not found → `Error.NotFound(...)`
  - invalid data (the domain returned a failure) → pass the domain `Error` on unchanged (it is `Error.Validation`)
  - duplicate Id (`AddAsync` returned false) → `Error.Conflict(...)`
- Keep the same error codes and messages the endpoints return today (for example `Listing.NotFound`, `Listing.Duplicate`).
- Update and Delete: load with `GetByIdAsync`, return NotFound if `null`, then call the domain or the repository, then `ncSaveChangesAsy`.
- Unique email (the README says `CreateMember` needs a unique email; the same goes for `UpdateMember`). The repository has no "find by email" method, so the handler uses `GetAllAsync`:
  - `CreateMemberHandler`: after `Member.Create` succeeded and before `AddAsync`, if any member returned by `GetAllAsync` has the same `Email`, return `Error.Conflict("Member.EmailTaken", $"A member with email '{member.Email.Value}' already exists.")` and do not add or save anything.
  - `UpdateMemberHandler`: after the `NotFound` check and **before** `member.Update`, build the new email with `Email.Create(command.Email)`. If it is valid and any **other** member (`m.Id != member.Id`) has that `Email`, return the same `Member.EmailTaken` conflict, without changing the member and without saving. If it is not valid, go on: `member.Update` returns the validation error as today. Keeping your own email is allowed.
  - `Email` is a record already normalized to lower case, so compare with `==`.
- If the slices already have their command, query and handler, keep them and only add what this skill asks for that is missing.
- Clock: take `TimeProvider` in the constructor and call `GetUtcNow()`. Never use `DateTime.Now` or `DateTime.UtcNow`.

## 5. Endpoint rules

- The endpoint only builds the command or query from the request, calls the handler and maps the `Result` to an HTTP status with `TypedResults`.
- The endpoint injects the handler interface (`ICommandHandler<,>` or `IQueryHandler<,>`), never a repository, never `IUnitOfWork`.
- No business rules and no `if` on data in the endpoint. The only `if` allowed is `result.IsFailure`.
- Mapping by `ErrorType`: `Validation` → 400 `BadRequest`, `NotFound` → 404 `NotFound`, `Conflict` → 409 `Conflict`. Success: Create → 201 `CreatedAtRoute` with Location, Get → 200 `Ok`, Update and Delete → 204 `NoContent`.
- The `Results<...>` return type lists only the statuses the slice can return, and the mapping contains only those arms.
- If a slice can fail with just one `ErrorType`, map it directly. If it can fail with two or more, use a `switch` on `result.Error.Type`.
- `UpdateMember` can now also fail with `Conflict` (email taken): its return type becomes `Results<NoContent, NotFound<Error>, BadRequest<Error>, Conflict<Error>>` and the `switch` gets the arm `ErrorType.Conflict => TypedResults.Conflict(result.Error)`. `CreateMember` already maps `Conflict` to 409.
- Keep `.WithName("<UseCase>")` and the route names exactly as today (`CreatedAtRoute` points to `"GetListingById"` / `"GetMemberById"`).

## 6. Never do

- Never add a new repository, or `new InMemory...Repository()`, inside a handler.
- Never let a handler call another handler.
- Never throw an exception for an expected failure (not found, invalid data, duplicate).
- Never call `CommitAsync` in a query handler, and never change state in a query handler.
- Never use MediatR or any mediator package.
- Never put a repository, `IUnitOfWork` or business rule in an endpoint.
- Never change existing files in `Domain/`; the only new file there is `IUnitOfWork.cs`. Never change the repositories, `tests/` or `postman/` to make the refactor easier.
- Never change routes, status codes, JSON field names or error bodies. The only new status is 409 `Member.EmailTaken` in `CreateMember` and `UpdateMember`.
- Never rename the existing request and response records.

## 7. Example: command slice (`CreateListing`)

`CreateListingCommand.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public sealed record CreateListingCommand(
    Guid SellerId,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price) : ICommand<CreateListingResponse>;
```

`CreateListingHandler.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public sealed class CreateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateListingCommand, CreateListingResponse>
{
    public async Task<Result<CreateListingResponse>> HandleAsync(
        CreateListingCommand command, CancellationToken ct)
    {
        var created = Listing.Create(
            command.SellerId, command.Title, command.Category,
            command.Condition, command.Price, timeProvider.GetUtcNow());

        if (created.IsFailure)
            return Result<CreateListingResponse>.Failure(created.Error);

        var listing = created.Value;

        var added = await repository.AddAsync(listing, ct);
        if (!added)
            return Result<CreateListingResponse>.Failure(
                Error.Conflict("Listing.Duplicate", $"A listing with id '{listing.Id}' already exists."));

        await unitOfWork.CommitAsync(ct);

        return CreateListingResponse.From(listing);
    }
}
```

`CreateListingEndpoint.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.CreateListing;

public static class CreateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateListing");
    }

    private static async Task<Results<CreatedAtRoute<CreateListingResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateListingRequest request,
        ICommandHandler<CreateListingCommand, CreateListingResponse> handler,
        CancellationToken ct)
    {
        var command = new CreateListingCommand(
            request.SellerId, request.Title, request.Category, request.Condition, request.Price);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.Conflict => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.CreatedAtRoute(
            result.Value, "GetListingById", new { id = result.Value.Id });
    }
}
```

## 8. Example: query slice (`GetListingById`)

`GetListingByIdQuery.cs`

```csharp
using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public sealed record GetListingByIdQuery(Guid Id) : IQuery<GetListingByIdResponse>;
```

`GetListingByIdHandler.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public sealed class GetListingByIdHandler(IListingRepository repository)
    : IQueryHandler<GetListingByIdQuery, GetListingByIdResponse>
{
    public async Task<Result<GetListingByIdResponse>> HandleAsync(
        GetListingByIdQuery query, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(query.Id, ct);

        if (listing is null)
            return Result<GetListingByIdResponse>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{query.Id}' was not found."));

        return GetListingByIdResponse.From(listing);
    }
}
```

`GetListingByIdEndpoint.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.GetListingById;

public static class GetListingByIdEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetListingById");
    }

    private static async Task<Results<Ok<GetListingByIdResponse>, NotFound<Error>>> Handle(
        [AsParameters] GetListingByIdRequest request,
        IQueryHandler<GetListingByIdQuery, GetListingByIdResponse> handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetListingByIdQuery(request.Id), ct);

        if (result.IsFailure)
            return TypedResults.NotFound(result.Error);

        return TypedResults.Ok(result.Value);
    }
}
```
## 9. Example: update slice (`UpdateListing`)

This is the only kind of slice that maps two error types: `NotFound` → 404 and `Validation` → 400. Success answers 204 with an empty body. The request record (`UpdateListingRequest`) stays as it is; the id comes from the route.

`UpdateListingCommand.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed record UpdateListingCommand(
    Guid Id,
    string Title,
    Category Category,
    Condition Condition,
    decimal Price) : ICommand<Guid>;
```

`UpdateListingHandler.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public sealed class UpdateListingHandler(
    IListingRepository repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateListingCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        UpdateListingCommand command, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(command.Id, ct);

        if (listing is null)
            return Result<Guid>.Failure(
                Error.NotFound("Listing.NotFound", $"Listing '{command.Id}' was not found."));

        var updated = listing.UpdateDetails(
            command.Title, command.Category, command.Condition, command.Price);

        if (updated.IsFailure)
            return Result<Guid>.Failure(updated.Error);

        repository.Update(listing);
        await unitOfWork.CommitAsync(ct);

        return listing.Id;
    }
}
```

`UpdateListingEndpoint.cs`

```csharp
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Listings.UpdateListing;

public static class UpdateListingEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateListing");
    }

    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        UpdateListingRequest request,
        ICommandHandler<UpdateListingCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new UpdateListingCommand(
            id, request.Title, request.Category, request.Condition, request.Price);

        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return result.Error.Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };

        return TypedResults.NoContent();
    }
}
```

## 10. Check after generating

- `dotnet build` finishes with 0 errors and 0 warnings.
- `dotnet test` passes (all existing tests), because they prove the behaviour did not change. New tests for the email check come from the testing skills; never edit `tests/`.
- No file in `Features/` mentions `IListingRepository`, `IMemberRepository` or `IUnitOfWork` except the `*Handler.cs` files.
- Every slice of the 10 has a command or query and a handler.
- The Postman collection still passes (Run collection, 36/36).


