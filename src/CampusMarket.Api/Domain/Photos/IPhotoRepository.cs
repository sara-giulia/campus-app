namespace CampusMarket.Api.Domain.Photos;

public interface IPhotoRepository
{
    /// <summary>
    /// Adds a photo. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    Task AddAsync(Photo photo, CancellationToken ct = default);

    /// <summary>
    /// The photo, or null.
    /// </summary>
    Task<Photo?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// A snapshot of the photos of a listing, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Photo>> GetByListingAsync(Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// How many photos the listing has.
    /// </summary>
    Task<int> CountForListingAsync(Guid listingId, CancellationToken ct = default);

    /// <summary>
    /// Removes the photo. Saved by IUnitOfWork.CommitAsync.
    /// </summary>
    void Delete(Photo photo);
}
