# CampusMarket

A second-hand marketplace for students on campus: textbooks, problem sets, laptops and lab equipment.
Sellers publish listings, buyers reserve them, and an AI module suggests a fair asking price.

## How to run

1. Start the API: open `CampusMarket.sln` and run the **http** profile, or run `dotnet run --project src/CampusMarket.Api --launch-profile http`. It listens on `http://localhost:5112`.
2. In Postman: **Import** (`Ctrl+O`) the file `postman/CampusMarket.postman_collection.json`.
3. Right-click the **CampusMarket** collection → **Run collection** → **Run**. Every request has tests; the `baseUrl` collection variable is `http://localhost:5112` (change it if your port is different).

Unit tests: `dotnet test` from the repository root.

---

## Table of contents

1. [Team](#1-team)
2. [Project overview](#2-project-overview)
3. [Use cases](#3-use-cases)
4. [Domain model](#4-domain-model)
5. [Key flows](#5-key-flows)
6. [Architecture](#6-architecture)
7. [Work packages](#7-work-packages)
8. [How we work](#8-how-we-work)

---

## 1. Team

| # | Name | GitHub |
|---|------|--------|
| 1 | Matasel Sara Giulia | `@sara-giulia` |
| 2 | Ilenei Ancuta-Bianca | `@Anca110` |
| 3 | Coman Carina | `@Carinacoman` |
| 4 | Preutu-Grigore Diana-Smaranda | `@dianapreutu` |

## 2. Project overview

| | |
|---|---|
| **Domain** | Second-hand marketplace for students |
| **Aggregates** | `Member`, `Listing`, `Reservation` |
| **Business slices** | 9 (see [Use cases](#3-use-cases)) |
| **Hard rule** | Two buyers reserve the same listing at the same moment: one succeeds, the other gets **409 Conflict** from the `Version` check |
| **AI module** | `SuggestPrice`: ML.NET regression that suggests an asking price with a range |

### Business rules

- A listing has a title, category, condition (`New`, `Good`, `Worn`), asking price, status (`Draft`, `Active`, `Reserved`, `Sold`) and a `Version` for optimistic concurrency.
- A reservation **expires after 48h** (computed with `TimeProvider`).
- A buyer can hold **at most 3 active reservations**.
- A seller **cannot reserve their own listing**.
- A **sold** listing cannot be reserved.
- The AI price is only a **suggestion**: it is never applied automatically, the seller decides.

### Tech stack

- .NET 10, ASP.NET Core **minimal APIs**
- **Vertical slices** with hand-written **CQRS** handlers (commands and queries)
- **Repository + Unit of Work** over **EF Core** with **SQLite**
- `Result<T>` for expected errors (no exceptions for business rule violations)
- `TimeProvider` for anything that depends on the clock
- **xUnit** with hand-written fakes
- **Postman** collections for API tests
- **SonarQube Cloud** for code quality and coverage, with a quality gate of **100% coverage on new code**
- **ML.NET** (`Microsoft.ML`, `Microsoft.ML.FastTree`) for the AI module

## 3. Use cases

### Actors

- **Member**: any registered student. A member acts as a **Seller** when they manage their own listings and as a **Buyer** when they reserve someone else's listing.
- **Visitor**: browses listings without acting on them.

```mermaid
flowchart LR
    Visitor((Visitor))
    Seller((Seller))
    Buyer((Buyer))

    subgraph CampusMarket
        UC1[Register as member]
        UC2[Browse and filter listings]
        UC3[View listing details]
        UC4[Create listing draft]
        UC5[Get suggested price]
        UC6[Publish listing]
        UC7[Confirm sale]
        UC8[Reserve listing]
        UC9[Cancel reservation]
        UC10[View my reservations]
    end

    Visitor --> UC1
    Visitor --> UC2
    Visitor --> UC3
    Seller --> UC4
    Seller --> UC5
    Seller --> UC6
    Seller --> UC7
    Buyer --> UC8
    Buyer --> UC9
    Buyer --> UC10
```

### Use cases mapped to slices

| Use case | Slice | Endpoint (planned) | Rules checked |
|---|---|---|---|
| Register as member | `CreateMember` | `POST /api/v1/members` | unique email |
| Create listing draft | `CreateListing` | `POST /api/v1/listings` | price > 0, valid category and condition |
| Publish listing | `PublishListing` | `POST /api/v1/listings/{id}/publish` | only the owner; only from `Draft` |
| Browse and filter listings | `GetListings` | `GET /api/v1/listings?category=&minPrice=&maxPrice=&page=&pageSize=` | only `Active` listings; paging |
| View listing details | `GetListingById` | `GET /api/v1/listings/{id}` | 404 if missing |
| Reserve listing | `ReserveListing` | `POST /api/v1/listings/{id}/reservations` | not own listing; not sold; max 3 active; **409 on concurrent reservation** |
| Cancel reservation | `CancelReservation` | `POST /api/v1/reservations/{id}/cancel` | only the buyer; only if active |
| Confirm sale | `ConfirmSale` | `POST /api/v1/listings/{id}/confirm-sale` | only the seller; only if `Reserved` |
| View my reservations | `GetMyReservations` | `GET /api/v1/members/{id}/reservations` | expired reservations shown as expired |
| Get suggested price | `SuggestPrice` (AI) | `POST /api/v1/listings/price-suggestion` | suggestion only, never applied |

All endpoints are versioned under `/api/v1/` (see [Design decisions](#design-decisions)).

### Implemented so far: CRUD slices

| Resource | Slice | Endpoint | Responses |
|---|---|---|---|
| Listing | `CreateListing` | `POST /api/v1/listings` | 201 + `Location`, 400, 409 |
| Listing | `GetListingById` | `GET /api/v1/listings/{id}` | 200, 404 |
| Listing | `GetListings` | `GET /api/v1/listings` | 200 |
| Listing | `UpdateListing` | `PUT /api/v1/listings/{id}` | 204, 400, 404 |
| Listing | `DeleteListing` | `DELETE /api/v1/listings/{id}` | 204, 404 |
| Member | `CreateMember` | `POST /api/v1/members` | 201 + `Location`, 400, 409 |
| Member | `GetMemberById` | `GET /api/v1/members/{id}` | 200, 404 |
| Member | `GetMembers` | `GET /api/v1/members` | 200 |
| Member | `UpdateMember` | `PUT /api/v1/members/{id}` | 204, 400, 404 |
| Member | `DeleteMember` | `DELETE /api/v1/members/{id}` | 204, 404 |

Both resources are stored in memory (`ConcurrentDictionary`) behind `IListingRepository` / `IMemberRepository`, registered as singletons. Endpoints depend only on the interfaces. Errors are returned as `{ "code", "message", "type" }`.

### Example scenario: reserving a listing

1. Ana publishes "Calculus textbook, Good condition, 40 RON".
2. Mihai sees it in the listing search and reserves it. The listing becomes `Reserved`.
3. Mihai has 48 hours to pick it up. If nothing happens, the reservation expires and the listing becomes `Active` again.
4. When they meet, Ana confirms the sale. The listing becomes `Sold` and can no longer be reserved.

## 4. Domain model

```mermaid
classDiagram
    class Member {
        Guid Id
        string Name
        string Email
    }

    class Listing {
        Guid Id
        Guid SellerId
        string Title
        Category Category
        Condition Condition
        decimal AskingPrice
        ListingStatus Status
        int Version
        DateTimeOffset CreatedAt
        Publish() Result
        Reserve(buyerId, now) Result
        ConfirmSale(sellerId) Result
    }

    class Reservation {
        Guid Id
        Guid ListingId
        Guid BuyerId
        DateTimeOffset CreatedAt
        DateTimeOffset ExpiresAt
        ReservationStatus Status
        Cancel(buyerId) Result
        IsExpired(now) bool
    }

    class ListingStatus {
        <<enumeration>>
        Draft
        Active
        Reserved
        Sold
    }

    class Condition {
        <<enumeration>>
        New
        Good
        Worn
    }

    class ReservationStatus {
        <<enumeration>>
        Active
        Cancelled
        Expired
        Completed
    }

    Member "1" --> "0..*" Listing : sells
    Member "1" --> "0..*" Reservation : holds
    Listing "1" --> "0..*" Reservation : has
    Listing ..> ListingStatus
    Listing ..> Condition
    Reservation ..> ReservationStatus
```

**Relationships:** a `Member` sells many `Listing`s (**one-to-many**, through `Listing.SellerId`). Members and listings are also connected **many-to-many** through the join entity `Reservation` (`MemberId` + `ListingId`): a member can reserve many listings, and a listing can have many reservations over time. A `Listing` owns its reservations and exposes them only as `IReadOnlyList<Reservation>`.

**Value objects:** `Money` (amount > 0) and `Email` (normalized to lower case, one `@` with text before and after) are immutable records that can only be created through a factory returning `Result<T>`.

### Listing lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft : CreateListing
    Draft --> Active : PublishListing
    Active --> Reserved : ReserveListing
    Reserved --> Active : CancelReservation / reservation expired (48h)
    Reserved --> Sold : ConfirmSale
    Sold --> [*]
```

Any other transition returns an error through `Result<T>` (for example "a sold listing cannot be reserved").

## 5. Key flows

### Hard rule: two buyers reserve the same listing at the same moment

Optimistic concurrency: `Listing.Version` is a concurrency token in EF Core. Both requests read version 5; the first save changes it to 6; the second save finds that the row no longer has version 5, EF Core throws `DbUpdateConcurrencyException`, and the handler turns it into **409 Conflict**.

```mermaid
sequenceDiagram
    actor A as Buyer A
    actor B as Buyer B
    participant API as ReserveListing handler
    participant DB as SQLite

    A->>API: POST /api/v1/listings/42/reservations
    B->>API: POST /api/v1/listings/42/reservations
    API->>DB: load listing 42 (Version = 5)
    API->>DB: load listing 42 (Version = 5)
    Note over API: both pass the business rules
    API->>DB: UPDATE ... WHERE Id = 42 AND Version = 5
    DB-->>API: 1 row updated (Version = 6)
    API-->>A: 201 Created
    API->>DB: UPDATE ... WHERE Id = 42 AND Version = 5
    DB-->>API: 0 rows updated
    API-->>B: 409 Conflict
```

We will test this with an integration test that runs two reservations in parallel against the same listing and checks that exactly one succeeds.


## 6. Architecture

### Planned folder structure

```
CampusMarket.sln
├── src/
│   ├── CampusMarket.Api/
│   │   ├── Features/                 # one folder per slice
│   │   │   ├── Members/CreateMember/
│   │   │   ├── Listings/CreateListing/
│   │   │   ├── Listings/PublishListing/
│   │   │   ├── Listings/GetListings/
│   │   │   ├── Listings/GetListingById/
│   │   │   ├── Reservations/ReserveListing/
│   │   │   ├── Reservations/CancelReservation/
│   │   │   ├── Listings/ConfirmSale/
│   │   │   ├── Reservations/GetMyReservations/
│   │   │   └── Pricing/SuggestPrice/
│   │   ├── Domain/                   # Member, Listing, Reservation, enums
│   │   ├── Infrastructure/           # DbContext, repositories, UnitOfWork
│   │   ├── Common/                   # Result<T>, Error, endpoint helpers
│   │   └── Program.cs
│   └── CampusMarket.Train/           # console tool: train + evaluate the price model
├── tests/
│   ├── CampusMarket.UnitTests/
│   └── CampusMarket.IntegrationTests/
├── data/
│   └── synthetic-sales.csv           # SYNTHETIC training data
├── postman/
│   └── CampusMarket.postman_collection.json
└── README.md
```

Each slice folder contains the request/response records, the handler and the endpoint mapping for that one use case.

### Design decisions

| Decision | Why |
|---|---|
| Vertical slices | Each use case lives in one folder, so team members rarely edit the same files. |
| Repository + Unit of Work | Handlers do not depend on EF Core directly and can be tested with fakes. |
| `Result<T>` | Business rule violations are expected outcomes, not exceptions. The endpoint maps them to HTTP codes. |
| `TimeProvider` | The 48h expiry can be tested by moving a fake clock forward. |
| `Version` concurrency token | Solves the hard rule without locks. |
| API versioning (`/api/v1/`) | All endpoints live in one versioned route group. If a contract ever has to change in a breaking way, we add `/api/v2/` next to it instead of breaking existing clients and Postman tests. |


## 7. Work packages

The project is divided into five work packages.

| Package | Slices and tasks | Tests |
|---|---|---|
| **Shared infrastructure** | Solution setup, `Result<T>`, DbContext, repositories, Unit of Work, `TimeProvider` registration, `/api/v1` route group, error → HTTP mapping | Unit tests for `Result<T>` |
| **Member** | `CreateMember` | Handler tests |
| **Listing** | `CreateListing`, `PublishListing`, `GetListings` (filters + paging), `GetListingById` | Domain rules for `Listing`, query tests |
| **Reservation** | `ReserveListing`, `CancelReservation`, `ConfirmSale`, `GetMyReservations`, 48h expiry, max 3 reservations, concurrency 409 | Rules + fake clock tests, parallel reservation integration test |
| **AI module** | Synthetic CSV generator, `CampusMarket.Train` (train + evaluate + baseline), `IPriceSuggester`, ML.NET and Fake implementations, `SuggestPrice` endpoint | Fake suggester tests, endpoint test |

However the work is split, every slice comes with its tests, its Postman requests and a README update, and every pull request is reviewed by another member.

**Order matters:** the shared infrastructure blocks everything else, so it is done first. The AI module does not depend on the database, so it can be worked on in parallel.



## 8. How we work

### Git workflow

- `main` is always working.
- Each task gets its own branch: `feature/reserve-listing`, `fix/listing-paging`.
- Work is merged through a **pull request** reviewed by at least one other member.
- Small commits with clear messages: `Add 48h expiry to Reservation`.

### Definition of done for a slice

- [ ] Endpoint works and returns the right HTTP codes
- [ ] Business rules return `Result` errors, not exceptions
- [ ] Unit tests for the rules
- [ ] Request(s) added to the Postman collection
- [ ] SonarQube Cloud quality gate passed, with **100% coverage on new code**
- [ ] Reviewed and merged into `main`

### Communication

- Weekly team meeting (after the lab) to check progress and plan the next week.
- Tasks tracked as GitHub Issues, one per slice, assigned to whoever picks it up.
- A team chat group for day-to-day questions.



---