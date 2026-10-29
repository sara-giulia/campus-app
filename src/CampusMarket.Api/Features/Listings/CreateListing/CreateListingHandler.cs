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
