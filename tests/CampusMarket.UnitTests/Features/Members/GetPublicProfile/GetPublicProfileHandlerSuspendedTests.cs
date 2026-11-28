using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Members.GetPublicProfile;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Members.GetPublicProfile;

public class GetPublicProfileHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _member = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly Listing _listing;
    private readonly GetPublicProfileHandler _handler;

    public GetPublicProfileHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_member.Id] = _member;
        _listing = Listing.Create(_member.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        var listings = new FakeListingRepository();
        listings.Items[_listing.Id] = _listing;
        _member.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _handler = new GetPublicProfileHandler(members, listings, new FakeReviewRepository(), _clock);
    }

    [Fact]
    public async Task Given_SuspendedMember_When_ReadingProfile_Then_ReturnsMemberNotFound()
    {
        var result = await _handler.HandleAsync(new GetPublicProfileQuery(_member.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{_member.Id}' was not found.");
    }

    [Fact]
    public async Task Given_SuspendedMember_When_AdminReadsProfile_Then_ReturnsItWithListings()
    {
        var result = await _handler.HandleAsync(new GetPublicProfileQuery(_member.Id, IsAdmin: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_member.Id);
        result.Value.ActiveListings.Select(l => l.Id).Should().Equal(_listing.Id);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_ReadingProfile_Then_ReturnsIt()
    {
        _clock.Now = Now.AddDays(1);

        var result = await _handler.HandleAsync(new GetPublicProfileQuery(_member.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(_member.Id);
    }
}
