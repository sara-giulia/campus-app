using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Views;

namespace CampusMarket.UnitTests.Domain.Views;

public class ListingViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ListingId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VisitorId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Given_MemberId_When_BuildingKey_Then_UsesMemberPrefix()
    {
        ListingView.KeyOf(MemberId, null).Should().Be("m:22222222222222222222222222222222");
    }

    [Fact]
    public void Given_VisitorId_When_BuildingKey_Then_UsesVisitorPrefix()
    {
        ListingView.KeyOf(null, VisitorId).Should().Be("v:33333333333333333333333333333333");
    }

    [Fact]
    public void Given_MemberAndVisitor_When_BuildingKey_Then_MemberWins()
    {
        ListingView.KeyOf(MemberId, VisitorId).Should().Be("m:22222222222222222222222222222222");
    }

    [Fact]
    public void Given_ValidArguments_When_Creating_Then_SetsAllFields()
    {
        var view = ListingView.Create(ListingId, "v:abc", Now);

        view.Id.Should().NotBeEmpty();
        view.ListingId.Should().Be(ListingId);
        view.ViewerKey.Should().Be("v:abc");
        view.CreatedAt.Should().Be(Now);
        view.Day.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void Given_TimeWhoseUtcDayDiffersFromItsOwnDay_When_Creating_Then_DayIsTheUtcDay()
    {
        // 00:30 on 2 January at +02:00 is 22:30 on 1 January in UTC.
        var view = ListingView.Create(ListingId, "v:abc", new DateTimeOffset(2026, 1, 2, 0, 30, 0, TimeSpan.FromHours(2)));

        view.Day.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void Given_Nothing_When_BuildingViewerMissingError_Then_IsValidationViewViewerMissing()
    {
        var error = ListingView.ViewerMissingError();

        error.Code.Should().Be("View.ViewerMissing");
        error.Type.Should().Be(ErrorType.Validation);
        error.Message.Should().Be("Log in or send a valid visitor id in the X-Visitor-Id header.");
    }
}
