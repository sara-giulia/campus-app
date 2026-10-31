# REVIEW.md

Reviews made with the `code-review` skill (`.claude/skills/code-review/SKILL.md`): v3, commit 506d7c0, and in week 4 v5, commit 742806e. Every row was checked by hand: the file and line exist and the problem is real.

## CampusMarket (our repository)

Found 2 problems: 1 Major, 1 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/CampusMarket.Api/Domain/Listings/Reservation.cs:32 | Major | domain invariants | `Reservation.Cancel()` is public, so a caller holding a `Reservation` (e.g. from `listing.Reservations`) can cancel it directly, leaving the parent `Listing` stuck in `Reserved` while the reservation becomes `Cancelled` — breaking the invariant stated in `Listing.CancelReservation` that "an active reservation always means the listing is Reserved". | Make `Cancel()` `internal` (like `Reservation.Create`) so only `Listing.CancelReservation` can transition the reservation and keep the listing's `Status`/`Version` in sync. |
| 2 | tests/CampusMarket.UnitTests/Domain/Listings/ListingTests.cs:289 | Minor | test quality | The test is named `Given_ReservedListing_When_CancellingAlreadyCancelledReservation_Then_ReturnsValidationError`, but the Arrange calls `listing.CancelReservation(reservation.Id)` before the Act, which moves the listing back to `Active`. At the moment of the Act the listing is `Active`, not `Reserved`, so the `Given_` part does not match what Arrange sets up. | Rename to `Given_ListingWithCancelledReservation_When_CancellingItAgain_Then_ReturnsValidationError` (or re-arrange so the listing is really in `Reserved` state before the Act). |

## CampusMarket by ioana-onut (https://github.com/ioana-onut/CampusMarket)

Found 1 problem: 1 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/CampusMarket.Api/Domain/Members/Member.cs:9 | Minor | naming | Private instance fields `listingIds` and `reservationIds` do not follow the `_camelCase` convention used by every other private instance field in the project (e.g. `_value` in `src/CampusMarket.Api/Domain/Common/Result.cs:28`, `_favorites` in `tests/CampusMarket.UnitTests/Fakes/FakeFavoriteRepository.cs:7`, `_listings` in `tests/CampusMarket.UnitTests/Fakes/FakeListingRepository.cs:7`, `_members` in `tests/CampusMarket.UnitTests/Fakes/FakeMemberRepository.cs:7`). | Rename the fields to `_listingIds` and `_reservationIds`, update their references on `src/CampusMarket.Api/Domain/Members/Member.cs:17`, `18`, `65`, `80`, `81`, `85`, `91`, `106`, `111`, `117`, and update `HasField("listingIds")` to `HasField("_listingIds")` on `src/CampusMarket.Api/Infrastructure/Persistence/Configurations/MemberConfiguration.cs:26` and `HasField("reservationIds")` to `HasField("_reservationIds")` on line 30. |

## Week 4 – code-review v5

Our repository, `main` at c4191e6 (after authentication, listing search and mutation testing). Skill v5, commit 742806e (committed as "v4"; renumbered because the groundwork PR made v4): it also reads the README and checks that every business rule in it has code that enforces it. Run by Ancuța Ilenei; every row was checked by hand: the file and line exist and the problem is real.

Found 14 problems: 0 Critical, 10 Major, 4 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/CampusMarket.Api/Features/Listings/UpdateListing/UpdateListingHandler.cs:28 | Major | error handling | `Listing.Version` is a concurrency token, but `SaveChangesAsync` is not protected. If two PUTs hit the same listing at once, EF Core throws `DbUpdateConcurrencyException` and the client gets 500. The README says: "EF Core throws `DbUpdateConcurrencyException`, and the handler turns it into **409 Conflict**". | Catch `DbUpdateConcurrencyException` (for example in `EfUnitOfWork`, returning a Result or a domain exception type) and return `Error.Conflict("Listing.ConcurrencyConflict", ...)`. Add `Conflict<Error>` to the endpoint. |
| 2 | src/CampusMarket.Api/Features/Members/CreateMember/CreateMemberHandler.cs:33 | Major | error handling | The unique email is checked with `GetAllAsync` and then saved, so two requests can both pass the check. The second save then breaks the unique index on `Email` and throws `DbUpdateException`, which gives 500 instead of 409 `Member.EmailTaken`. | Catch the unique-constraint `DbUpdateException` on save and return `Error.Conflict("Member.EmailTaken", ...)`. |
| 3 | src/CampusMarket.Api/Features/Auth/Register/RegisterHandler.cs:44 | Major | error handling | Same race as in `CreateMemberHandler`: two registrations with the same email can both pass the `GetAllAsync` check. The second one fails on the unique index with an unhandled `DbUpdateException` (500 instead of 409). | Handle the unique-constraint violation on save and map it to `Member.EmailTaken` (Conflict). |
| 4 | src/CampusMarket.Api/Features/Members/UpdateMember/UpdateMemberHandler.cs:36 | Major | error handling | Same race: two members switching to the same email at the same time both pass the check. The second `SaveChangesAsync` throws on the unique index and returns 500 instead of 409. | Handle the unique-constraint violation on save and return `Member.EmailTaken` (Conflict). |
| 5 | src/CampusMarket.Api/Domain/Listings/Listing.cs:72 | Major | domain invariants | README, Publish listing: "only the owner; only from `Draft`". `Publish()` takes no caller id, so it cannot enforce "only the owner". | Change it to `Publish(Guid sellerId)` and return a validation error such as `Listing.NotOwner` when `sellerId != SellerId`. |
| 6 | src/CampusMarket.Api/Domain/Listings/Listing.cs:84 | Major | domain invariants | README: "A buyer can hold **at most 3 active reservations**". No code enforces this. `Reserve` only checks status, an empty buyer and the buyer's own listing. | Pass the buyer's current active reservation count (from the ReserveListing handler) into `Reserve` and fail with `Reservation.LimitReached` when it is 3 or more. |
| 7 | src/CampusMarket.Api/Domain/Listings/Listing.cs:104 | Major | domain invariants | README, Cancel reservation: "only the buyer; only if active" (the domain model has `Cancel(buyerId)`). `CancelReservation(reservationId)` and `Reservation.Cancel()` never check who is cancelling. | Add a `buyerId` parameter and return a validation error when `reservation.MemberId != buyerId`. |
| 8 | src/CampusMarket.Api/Domain/Listings/Listing.cs:5 | Major | domain invariants | README, Confirm sale: "only the seller; only if `Reserved`", and "A **sold** listing cannot be reserved". `Listing` has no `ConfirmSale` method, so the `Sold` status can never be reached and neither rule is enforced. | Add `ConfirmSale(Guid sellerId)` that requires `sellerId == SellerId` and `Status == Reserved`, sets `Sold`, marks the reservation `Completed` and increments `Version`. |
| 9 | src/CampusMarket.Api/Domain/Listings/Reservation.cs:42 | Major | domain invariants | README: "If nothing happens, the reservation expires and the listing becomes `Active` again". `IsExpired` only reports expiry. Nothing sets `ReservationStatus.Expired` or puts the listing back to `Active`, so an expired listing stays `Reserved` and `Reserve` keeps rejecting it. | Add a domain method (for example `Listing.ExpireReservations(now)`) that marks expired reservations `Expired` and sets the listing back to `Active`. Call it before `Reserve` or before reading the listing. |
| 10 | src/CampusMarket.Api/CampusMarketSetup.cs:105 | Major | domain invariants | The admin seed compares the stored, lower-case `m.Email.Value` with the raw `Seed:AdminEmail` value. If the configured email has upper-case letters, the existing admin is not found, a second admin with the same email is created, and the unique index makes `SaveChanges` throw at startup (README rule: "unique email"). | Normalize first with `Email.Create(email)` and compare `m.Email == normalized.Value` (or query `db.Members.Any(m => m.Email == normalized.Value)`). |
| 11 | tests/CampusMarket.IntegrationTests/Auth/LoginTests.cs:68 | Minor | test quality | `Given_WrongPassword_When_LoggingIn_Then_Returns401` checks only the status and the body, not the database state. The same gap is in `Given_InvalidEmailFormat...` (line 87) and `Given_EmptyPassword...` (line 104). The integration-testing rules require every test to check the status, the body and the database. | Add `QueryDbAsync(db => db.Set<Member>().CountAsync())` and assert the seeded member is unchanged (or the count is 0 when nothing was seeded). |
| 12 | tests/CampusMarket.UnitTests/Domain/Listings/ReservationTests.cs:66 | Minor | test quality | `Given_CancelledReservation_When_CheckingExpirationAfterExpiry_Then_ReturnsFalse` says "AfterExpiry", but the test checks `Now.AddHours(48)`, which is exactly the expiry moment, not after it. | Use `Now.AddHours(49)`, or rename the test to `...When_CheckingExpirationAtExpiry...`. |
| 13 | tests/CampusMarket.UnitTests/Domain/Listings/ListingTests.cs:240 | Minor | naming | `Given_ActiveListing_When_BuyerIdIsEmpty_Then_ReturnsValidationError` puts a state in the `When` part instead of the action (`Reserve`). This breaks the `Given_<state>_When_<action>_Then_<expected>` convention that the other tests follow. | Rename it to `Given_ActiveListing_When_ReservingWithEmptyBuyerId_Then_ReturnsValidationError`. |
| 14 | src/CampusMarket.Api/Domain/Common/Result.cs:5 | Minor | naming | The private fields `_value` and `_error` use an underscore prefix. The rest of `src/` names private fields without one (`Listing.reservations`, `AspNetPasswordHasher.hasher`). | Rename them to `value` and `error`, or agree on one convention for private fields and apply it everywhere. |

### Checked by hand

- **1–4:** no `DbUpdateConcurrencyException` or `DbUpdateException` is caught anywhere in `src/`. The README (section on optimistic concurrency) promises 409.
- **5:** true on `main`. This week the `PublishListing` slice (Carina) checks the owner in the handler and returns 403 `Listing.NotOwner`, so the rule will be enforced there, not in `Listing.Publish()`.
- **6, 8:** the "max 3 active reservations" rule and `ConfirmSale` are in the README but are not built yet (Reservation work package).
- **7:** true, but the README and the team disagree: the README says "only the buyer", the week 4 decision is "the buyer, the seller or an Admin". The README has to be updated together with `CancelReservation`.
- **9:** true; the automatic expiry after 48 h was moved to week 7 by the team.
- **10:** true; today it does not show, because `Seed:AdminEmail` in `appsettings.Development.json` is already lower case.
- **11–14:** the lines and names are exact.
- **Not reported again:** the two v3 rows above (`Reservation.cs:32` and `ListingTests.cs:289`) are still in the code, but v5 did not report them (see `PROMPTS.md`, code-review run 5). They are fixed this week in the ownership PR.

