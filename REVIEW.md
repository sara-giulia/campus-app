# REVIEW.md

Reviews made with the `code-review` skill (`.claude/skills/code-review/SKILL.md`, v3, commit 506d7c0). Every row was checked by hand: the file and line exist and the problem is real.

## CampusMarket (our repository)

Found 2 problems: 1 Major, 1 Minor.

| # | File:Line | Severity | Category | Problem | Proposed fix |
|---|---|---|---|---|---|
| 1 | src/CampusMarket.Api/Domain/Listings/Reservation.cs:32 | Major | domain invariants | `Reservation.Cancel()` is public, so a caller holding a `Reservation` (e.g. from `listing.Reservations`) can cancel it directly, leaving the parent `Listing` stuck in `Reserved` while the reservation becomes `Cancelled` — breaking the invariant stated in `Listing.CancelReservation` that "an active reservation always means the listing is Reserved". | Make `Cancel()` `internal` (like `Reservation.Create`) so only `Listing.CancelReservation` can transition the reservation and keep the listing's `Status`/`Version` in sync. |
| 2 | tests/CampusMarket.UnitTests/Domain/Listings/ListingTests.cs:289 | Minor | test quality | The test is named `Given_ReservedListing_When_CancellingAlreadyCancelledReservation_Then_ReturnsValidationError`, but the Arrange calls `listing.CancelReservation(reservation.Id)` before the Act, which moves the listing back to `Active`. At the moment of the Act the listing is `Active`, not `Reserved`, so the `Given_` part does not match what Arrange sets up. | Rename to `Given_ListingWithCancelledReservation_When_CancellingItAgain_Then_ReturnsValidationError` (or re-arrange so the listing is really in `Reserved` state before the Act). |

## <name of the other team> (their repository: <link>)

(to be added)

