using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.Api.Features.Offers.WithdrawOffer;

// The buyer takes back their own Pending offer. No notification: the seller sees it in the conversation.
public sealed class WithdrawOfferHandler(
    IOfferRepository offers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<WithdrawOfferCommand, OfferResponse>
{
    public async Task<Result<OfferResponse>> HandleAsync(WithdrawOfferCommand command, CancellationToken ct)
    {
        var offer = await offers.GetByIdAsync(command.OfferId, ct);
        if (offer is null)
            return Result<OfferResponse>.Failure(
                Error.NotFound("Offer.NotFound", $"Offer '{command.OfferId}' was not found."));

        var withdrawn = offer.Withdraw(command.MemberId, timeProvider.GetUtcNow());
        if (withdrawn.IsFailure)
            return Result<OfferResponse>.Failure(withdrawn.Error);

        await unitOfWork.CommitAsync(ct);

        return OfferResponse.From(offer);
    }
}
