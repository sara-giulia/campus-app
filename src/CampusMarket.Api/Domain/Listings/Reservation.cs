using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Listings;

public sealed class Reservation
{
    public Guid Id { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid MemberId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public ReservationStatus Status { get; private set; }

    private Reservation() { }

    internal static Reservation Create(
        Guid listingId,
        Guid memberId,
        DateTimeOffset createdAt)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            MemberId = memberId,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.AddHours(48),
            Status = ReservationStatus.Active
        };
    }

    // Internal: only Listing.CancelReservation may cancel, so the listing's Status and Version stay in sync.
    internal Result<Reservation> Cancel()
    {
        if (Status != ReservationStatus.Active)
            return Result<Reservation>.Failure(Error.Validation("Reservation.NotActive", "Only an active reservation can be cancelled."));

        Status = ReservationStatus.Cancelled;

        return this;
    }

    // Internal: only Listing.ConfirmSale may complete, so the listing's Status and Version stay in sync.
    internal Result<Reservation> Complete()
    {
        if (Status != ReservationStatus.Active)
            return Result<Reservation>.Failure(Error.Validation("Reservation.NotActive", "Only an active reservation can be completed."));

        Status = ReservationStatus.Completed;

        return this;
    }

    public bool IsExpired(DateTimeOffset currentTime)
    {
        return Status == ReservationStatus.Active && currentTime >= ExpiresAt;
    }
}