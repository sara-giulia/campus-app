using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications.GetMyNotifications;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Notifications.GetMyNotifications;

public class GetMyNotificationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ListingId = new("22222222-2222-2222-2222-222222222222");

    private static Notification BuildNotification(Guid memberId, DateTimeOffset createdAt, string message = "Buyer reserved Algebra book") =>
        Notification.Create(memberId, NotificationType.ReservationCreated, message, ListingId, createdAt).Value;

    private static Notification BuildReadNotification(Guid memberId, DateTimeOffset createdAt, string message = "Buyer reserved Algebra book")
    {
        var notification = BuildNotification(memberId, createdAt, message);
        notification.MarkAsRead(createdAt.AddMinutes(5));
        return notification;
    }

    [Fact]
    public async Task Given_NoNotifications_When_Handling_Then_ReturnsEmptyPage()
    {
        var repository = new FakeNotificationRepository();
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        result.Value.UnreadCount.Should().Be(0);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_NotificationsOfTwoMembers_When_Handling_Then_ReturnsOnlyMine()
    {
        var mine = BuildNotification(MemberId, Now);
        var others = BuildNotification(OtherMemberId, Now.AddHours(1));
        var repository = new FakeNotificationRepository();
        repository.Add(mine);
        repository.Add(others);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(mine.Id);
        result.Value.TotalCount.Should().Be(1);
        result.Value.UnreadCount.Should().Be(1);
        repository.Items.Should().Equal(mine, others);
    }

    [Fact]
    public async Task Given_TwoNotifications_When_Handling_Then_ReturnsNewestFirstWithAllFields()
    {
        var older = BuildReadNotification(MemberId, Now, "Older");
        var newer = BuildNotification(MemberId, Now.AddHours(1), "Newer");
        var repository = new FakeNotificationRepository();
        repository.Add(older);
        repository.Add(newer);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newer.Id, older.Id);
        result.Value.Items[1].Type.Should().Be(NotificationType.ReservationCreated);
        result.Value.Items[1].Message.Should().Be("Older");
        result.Value.Items[1].ListingId.Should().Be(ListingId);
        result.Value.Items[1].CreatedAt.Should().Be(Now);
        result.Value.Items[1].ReadAt.Should().Be(Now.AddMinutes(5));
        result.Value.Items[0].ReadAt.Should().BeNull();
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(2);
        repository.Items.Should().Equal(older, newer);
    }

    [Fact]
    public async Task Given_ReadAndUnreadNotifications_When_HandlingUnreadOnly_Then_ReturnsOnlyUnread()
    {
        var read = BuildReadNotification(MemberId, Now);
        var unread = BuildNotification(MemberId, Now.AddHours(1));
        var repository = new FakeNotificationRepository();
        repository.Add(read);
        repository.Add(unread);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, UnreadOnly: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(unread.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        read.ReadAt.Should().Be(Now.AddMinutes(5));
        unread.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_ReadAndUnreadNotifications_When_HandlingWithoutFilter_Then_ReturnsBoth()
    {
        var read = BuildReadNotification(MemberId, Now);
        var unread = BuildNotification(MemberId, Now.AddHours(1));
        var repository = new FakeNotificationRepository();
        repository.Add(read);
        repository.Add(unread);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, UnreadOnly: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(unread.Id, read.Id);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_UnreadNotificationsOnSeveralPages_When_Handling_Then_UnreadCountIgnoresPagingAndFilter()
    {
        var repository = new FakeNotificationRepository();
        repository.Add(BuildNotification(MemberId, Now));
        repository.Add(BuildNotification(MemberId, Now.AddHours(1)));
        repository.Add(BuildNotification(MemberId, Now.AddHours(2)));
        repository.Add(BuildReadNotification(MemberId, Now.AddHours(3)));
        repository.Add(BuildNotification(OtherMemberId, Now.AddHours(4)));
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, UnreadOnly: true, Page: 1, PageSize: 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.UnreadCount.Should().Be(3);
        repository.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Given_ThreeNotifications_When_RequestingFirstPageOfTwo_Then_ReturnsTwoNewest()
    {
        var oldest = BuildNotification(MemberId, Now);
        var middle = BuildNotification(MemberId, Now.AddHours(1));
        var newest = BuildNotification(MemberId, Now.AddHours(2));
        var repository = new FakeNotificationRepository();
        repository.Add(oldest);
        repository.Add(middle);
        repository.Add(newest);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, Page: 1, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(newest.Id, middle.Id);
        result.Value.Count.Should().Be(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
        repository.Items.Should().Equal(oldest, middle, newest);
    }

    [Fact]
    public async Task Given_ThreeNotifications_When_RequestingSecondPageOfTwo_Then_ReturnsOldest()
    {
        var oldest = BuildNotification(MemberId, Now);
        var middle = BuildNotification(MemberId, Now.AddHours(1));
        var newest = BuildNotification(MemberId, Now.AddHours(2));
        var repository = new FakeNotificationRepository();
        repository.Add(oldest);
        repository.Add(middle);
        repository.Add(newest);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, Page: 2, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Id).Should().Equal(oldest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Given_ThreeNotifications_When_RequestingPageAfterLast_Then_ReturnsEmptyItems()
    {
        var repository = new FakeNotificationRepository();
        repository.Add(BuildNotification(MemberId, Now));
        repository.Add(BuildNotification(MemberId, Now.AddHours(1)));
        repository.Add(BuildNotification(MemberId, Now.AddHours(2)));
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, Page: 5, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_DefaultQuery_When_Handling_Then_UsesDefaultPaging()
    {
        var repository = new FakeNotificationRepository();
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(GetMyNotificationsQuery.DefaultPage);
        result.Value.PageSize.Should().Be(GetMyNotificationsQuery.DefaultPageSize);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Given_PageSizeAtLimit_When_Handling_Then_Succeeds(int pageSize)
    {
        var repository = new FakeNotificationRepository();
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, PageSize: pageSize), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(pageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Given_InvalidPage_When_Handling_Then_ReturnsInvalidPageError(int page)
    {
        var notification = BuildNotification(MemberId, Now);
        var repository = new FakeNotificationRepository();
        repository.Add(notification);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, Page: page), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notifications.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        repository.Items.Should().Equal(notification);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Given_InvalidPageSize_When_Handling_Then_ReturnsInvalidPageSizeError(int pageSize)
    {
        var notification = BuildNotification(MemberId, Now);
        var repository = new FakeNotificationRepository();
        repository.Add(notification);
        var handler = new GetMyNotificationsHandler(repository);

        var result = await handler.HandleAsync(new GetMyNotificationsQuery(MemberId, PageSize: pageSize), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notifications.InvalidPageSize");
        result.Error.Message.Should().Be($"Page size must be between 1 and {GetMyNotificationsQuery.MaxPageSize}.");
        repository.Items.Should().Equal(notification);
    }
}
