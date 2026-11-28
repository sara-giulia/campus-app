using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Moderation.ReportListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.ReportListing;

public class ReportListingHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _reporter = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _otherSeller = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly FakeReportRepository _reports = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly ReportListingHandler _handler;

    public ReportListingHandlerSuspendedTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        members.Items[_reporter.Id] = _reporter;
        members.Items[_otherSeller.Id] = _otherSeller;
        _handler = new ReportListingHandler(listings, members, _reports, _unitOfWork, _clock);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_ReportingListing_Then_ReturnsListingNotFoundAndSavesNothing()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);

        var result = await _handler.HandleAsync(new ReportListingCommand(_reporter.Id, _listing.Id, "Fake photos"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{_listing.Id}' was not found.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedSellerAndUnknownReporter_When_ReportingListing_Then_ReturnsListingNotFoundFirst()
    {
        _seller.Suspend(AdminId, "Scam", null, Now);

        var result = await _handler.HandleAsync(new ReportListingCommand(Guid.NewGuid(), _listing.Id, "Fake photos"), CancellationToken.None);

        result.Error.Code.Should().Be("Listing.NotFound");
    }

    [Fact]
    public async Task Given_SuspensionOver_When_ReportingListing_Then_Succeeds()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new ReportListingCommand(_reporter.Id, _listing.Id, "Fake photos"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _reports.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnotherSellerSuspended_When_ReportingListing_Then_Succeeds()
    {
        _otherSeller.Suspend(AdminId, "Scam", null, Now);

        var result = await _handler.HandleAsync(new ReportListingCommand(_reporter.Id, _listing.Id, "Fake photos"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _reports.Items.Should().ContainSingle();
    }
}
