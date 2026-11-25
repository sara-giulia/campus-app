using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure.Persistence.Seed;

namespace CampusMarket.UnitTests.Infrastructure.Persistence.Seed;

public class DemoListingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_NoSellerIds_When_BuildingListings_Then_ThrowsArgumentException()
    {
        var sellerIds = new List<Guid>();

        Action act = () => DemoListings.BuildListings(sellerIds, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("sellerIds");
    }

    [Fact]
    public void Given_OnlyAnEmptySellerId_When_BuildingListings_Then_SkipsEveryListing()
    {
        var sellerIds = new List<Guid> { Guid.Empty };

        var listings = DemoListings.BuildListings(sellerIds, Now);

        listings.Should().BeEmpty();
    }

    [Fact]
    public void Given_SameSellerIdTwice_When_BuildingListings_Then_ReservationFailsAndEveryListingStaysActive()
    {
        var sellerId = Guid.NewGuid();
        var sellerIds = new List<Guid> { sellerId, sellerId };

        var listings = DemoListings.BuildListings(sellerIds, Now);

        listings.Should().HaveCount(DemoListings.ListingCount);
        listings.Should().OnlyContain(l => l.Status == ListingStatus.Active);
    }
}
