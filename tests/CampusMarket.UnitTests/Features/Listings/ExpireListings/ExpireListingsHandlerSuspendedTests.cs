using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Listings.ExpireListings;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Listings.ExpireListings;

public class ExpireListingsHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AfterExpiry = Now.AddDays(31);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _suspendedSeller = Member.Create("Seller One", "seller1@uni.edu", Now).Value;
    private readonly Member _otherSeller = Member.Create("Seller Two", "seller2@uni.edu", Now).Value;
    private readonly FakeListingRepository _listings = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Listing _suspendedListing;
    private readonly Listing _otherListing;
    private readonly ExpireListingsHandler _handler;

    public ExpireListingsHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_suspendedSeller.Id] = _suspendedSeller;
        members.Items[_otherSeller.Id] = _otherSeller;
        _suspendedListing = Active(_suspendedSeller.Id, "Algebra book");
        _otherListing = Active(_otherSeller.Id, "Lab coat");
        _suspendedSeller.Suspend(AdminId, "Scam", null, Now);
        _handler = new ExpireListingsHandler(_listings, members, _notifications, _unitOfWork, _emailSender,
            Options.Create(new FrontendOptions { BaseUrl = "https://campus.example/" }),
            new FixedTimeProvider(AfterExpiry), NullLogger<ExpireListingsHandler>.Instance);
    }

    private Listing Active(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_Expiring_Then_ListingExpiresWithNotificationButNoEmail()
    {
        var result = await _handler.HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _suspendedListing.Status.Should().Be(ListingStatus.Expired);
        _otherListing.Status.Should().Be(ListingStatus.Expired);
        _notifications.Items.Should().HaveCount(2);
        _notifications.Items.Should().ContainSingle(n => n.MemberId == _suspendedSeller.Id)
            .Which.Type.Should().Be(NotificationType.ListingExpired);
        _emailSender.Items.Select(e => e.To).Should().Equal("seller2@uni.edu");
        _unitOfWork.SaveCount.Should().Be(1);
    }
}
