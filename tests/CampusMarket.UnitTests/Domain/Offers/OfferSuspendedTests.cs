using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Offers;

namespace CampusMarket.UnitTests.Domain.Offers;

public class OfferSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AnsweredAt = Now.AddMinutes(1);
    private static readonly DateTimeOffset Later = Now.AddHours(1);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");

    private static (Listing Listing, Offer Offer) PendingOffer()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 100m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return (listing, Offer.Make(listing, BuyerId, 80m, Now).Value);
    }

    private static Offer AnsweredOffer(OfferStatus status)
    {
        var (listing, offer) = PendingOffer();
        var answered = status switch
        {
            OfferStatus.Accepted => offer.Accept(SellerId, listing, AnsweredAt).IsSuccess,
            OfferStatus.Rejected => offer.Reject(SellerId, AnsweredAt).IsSuccess,
            OfferStatus.Countered => offer.Counter(SellerId, listing, 90m, AnsweredAt).IsSuccess,
            _ => offer.Withdraw(BuyerId, AnsweredAt).IsSuccess
        };
        return answered ? offer : throw new InvalidOperationException($"Could not put the offer in {status}.");
    }

    [Fact]
    public void Given_PendingOffer_When_ClosingBecauseMemberSuspended_Then_ReturnsTrueAndRejects()
    {
        var (_, offer) = PendingOffer();

        var closed = offer.CloseBecauseMemberSuspended(Later);

        closed.Should().BeTrue();
        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.RespondedAt.Should().Be(Later);
    }

    [Theory]
    [InlineData(OfferStatus.Accepted)]
    [InlineData(OfferStatus.Rejected)]
    [InlineData(OfferStatus.Countered)]
    [InlineData(OfferStatus.Withdrawn)]
    public void Given_AnsweredOffer_When_ClosingBecauseMemberSuspended_Then_ReturnsFalseAndChangesNothing(OfferStatus status)
    {
        var offer = AnsweredOffer(status);

        var closed = offer.CloseBecauseMemberSuspended(Later);

        closed.Should().BeFalse();
        offer.Status.Should().Be(status);
        offer.RespondedAt.Should().Be(AnsweredAt);
    }
}
