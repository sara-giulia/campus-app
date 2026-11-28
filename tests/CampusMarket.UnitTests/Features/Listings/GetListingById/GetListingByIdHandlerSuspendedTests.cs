using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings.GetListingById;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Listings.GetListingById;

public class GetListingByIdHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ViewerId = new("11111111-1111-1111-1111-111111111111");

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly FakeListingRepository _repository = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _listing;
    private readonly GetListingByIdHandler _handler;

    public GetListingByIdHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _repository.Items[_listing.Id] = _listing;
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetListingByIdHandler(_repository, members, _clock);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_MemberOpensListing_Then_ReturnsListingNotFound()
    {
        var result = await _handler.HandleAsync(new GetListingByIdQuery(_listing.Id, ViewerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{_listing.Id}' was not found.");
        _repository.Items.Should().ContainKey(_listing.Id);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_AnonymousOpensListing_Then_ReturnsListingNotFound()
    {
        var result = await _handler.HandleAsync(new GetListingByIdQuery(_listing.Id), CancellationToken.None);

        result.Error.Code.Should().Be("Listing.NotFound");
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_AdminOpensListing_Then_ReturnsIt()
    {
        var result = await _handler.HandleAsync(new GetListingByIdQuery(_listing.Id, AdminId, IsAdmin: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_listing.Id);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_MemberOpensListing_Then_ReturnsIt()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetListingByIdQuery(_listing.Id, ViewerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_listing.Id);
    }
}
