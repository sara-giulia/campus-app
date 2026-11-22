using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications.MarkAllNotificationsRead;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Notifications.MarkAllNotificationsRead;

public class MarkAllNotificationsReadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ListingId = new("22222222-2222-2222-2222-222222222222");

    private static Notification BuildNotification(Guid memberId) =>
        Notification.Create(memberId, NotificationType.ReservationCreated, "Buyer reserved Algebra book", ListingId, Now.AddHours(-1)).Value;

    [Fact]
    public async Task Given_MyUnreadNotifications_When_MarkingAllAsRead_Then_AllBecomeReadAndSavesOnce()
    {
        var first = BuildNotification(MemberId);
        var second = BuildNotification(MemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(first);
        repository.Add(second);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkAllNotificationsReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkAllNotificationsReadCommand(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        first.ReadAt.Should().Be(Now);
        second.ReadAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AlreadyReadNotification_When_MarkingAllAsRead_Then_KeepsItsReadAtAndCountsOnlyUnread()
    {
        var alreadyRead = BuildNotification(MemberId);
        alreadyRead.MarkAsRead(Now.AddMinutes(-30));
        var unread = BuildNotification(MemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(alreadyRead);
        repository.Add(unread);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkAllNotificationsReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkAllNotificationsReadCommand(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        alreadyRead.ReadAt.Should().Be(Now.AddMinutes(-30));
        unread.ReadAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnotherMembersUnreadNotification_When_MarkingAllAsRead_Then_ItStaysUnread()
    {
        var mine = BuildNotification(MemberId);
        var others = BuildNotification(OtherMemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(mine);
        repository.Add(others);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkAllNotificationsReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkAllNotificationsReadCommand(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        mine.ReadAt.Should().Be(Now);
        others.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Given_NoUnreadNotifications_When_MarkingAllAsRead_Then_ReturnsZeroAndDoesNotSave()
    {
        var others = BuildNotification(OtherMemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(others);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkAllNotificationsReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkAllNotificationsReadCommand(MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        others.ReadAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
