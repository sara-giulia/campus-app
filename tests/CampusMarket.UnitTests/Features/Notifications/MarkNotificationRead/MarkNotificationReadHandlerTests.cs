using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications.MarkNotificationRead;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Notifications.MarkNotificationRead;

public class MarkNotificationReadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ListingId = new("22222222-2222-2222-2222-222222222222");

    private static Notification BuildNotification(Guid memberId) =>
        Notification.Create(memberId, NotificationType.ReservationCreated, "Buyer reserved Algebra book", ListingId, Now.AddHours(-1)).Value;

    [Fact]
    public async Task Given_MyUnreadNotification_When_MarkingAsRead_Then_SetsReadAtAndSavesOnce()
    {
        var notification = BuildNotification(MemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(notification);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkNotificationReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkNotificationReadCommand(notification.Id, MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(notification.Id);
        notification.ReadAt.Should().Be(Now);
        notification.IsRead.Should().BeTrue();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_MyAlreadyReadNotification_When_MarkingAsRead_Then_KeepsFirstReadAt()
    {
        var notification = BuildNotification(MemberId);
        notification.MarkAsRead(Now.AddMinutes(-30));
        var repository = new FakeNotificationRepository();
        repository.Add(notification);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkNotificationReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkNotificationReadCommand(notification.Id, MemberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.ReadAt.Should().Be(Now.AddMinutes(-30));
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownNotification_When_MarkingAsRead_Then_ReturnsNotFoundAndDoesNotSave()
    {
        var repository = new FakeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkNotificationReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));
        var unknownId = Guid.NewGuid();

        var result = await handler.HandleAsync(new MarkNotificationReadCommand(unknownId, MemberId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Notification.NotFound");
        result.Error.Message.Should().Be($"Notification '{unknownId}' was not found.");
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMembersNotification_When_MarkingAsRead_Then_ReturnsNotFoundAndLeavesItUnread()
    {
        var notification = BuildNotification(OtherMemberId);
        var repository = new FakeNotificationRepository();
        repository.Add(notification);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new MarkNotificationReadHandler(repository, unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(new MarkNotificationReadCommand(notification.Id, MemberId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Notification.NotFound");
        result.Error.Message.Should().Be($"Notification '{notification.Id}' was not found.");
        notification.ReadAt.Should().BeNull();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
