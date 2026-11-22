using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Notifications;

namespace CampusMarket.UnitTests.Domain.Notifications;

public class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ListingId = new("22222222-2222-2222-2222-222222222222");

    private static Notification BuildNotification() =>
        Notification.Create(MemberId, NotificationType.ReservationCreated, "Buyer reserved Algebra book", ListingId, Now).Value;

    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsUnreadNotificationWithAllFields()
    {
        var result = Notification.Create(MemberId, NotificationType.ListingSold, "Algebra book was sold to you", ListingId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.MemberId.Should().Be(MemberId);
        result.Value.Type.Should().Be(NotificationType.ListingSold);
        result.Value.Message.Should().Be("Algebra book was sold to you");
        result.Value.ListingId.Should().Be(ListingId);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.ReadAt.Should().BeNull();
        result.Value.IsRead.Should().BeFalse();
    }

    [Fact]
    public void Given_MessageWithSpacesAround_When_Creating_Then_StoresTrimmedMessage()
    {
        var result = Notification.Create(MemberId, NotificationType.ReservationCreated, "  Buyer reserved Algebra book  ", ListingId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("Buyer reserved Algebra book");
    }

    [Fact]
    public void Given_NoListingId_When_Creating_Then_ReturnsNotificationWithNullListingId()
    {
        var result = Notification.Create(MemberId, NotificationType.ReservationExpired, "Your reservation expired", null, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().BeNull();
    }

    [Fact]
    public void Given_TwoValidCalls_When_Creating_Then_ReturnsDifferentIds()
    {
        var first = Notification.Create(MemberId, NotificationType.ReservationCreated, "First", ListingId, Now);
        var second = Notification.Create(MemberId, NotificationType.ReservationCreated, "Second", ListingId, Now);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().NotBe(second.Value.Id);
    }

    [Fact]
    public void Given_EmptyMemberId_When_Creating_Then_ReturnsMemberEmptyError()
    {
        var result = Notification.Create(Guid.Empty, NotificationType.ReservationCreated, "Buyer reserved Algebra book", ListingId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notification.MemberEmpty");
        result.Error.Message.Should().Be("Member id must not be empty.");
    }

    [Theory]
    [InlineData(99)]
    [InlineData(-1)]
    public void Given_UndefinedType_When_Creating_Then_ReturnsTypeInvalidError(int type)
    {
        var result = Notification.Create(MemberId, (NotificationType)type, "Buyer reserved Algebra book", ListingId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notification.TypeInvalid");
        result.Error.Message.Should().Be("Unknown notification type.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Given_EmptyMessage_When_Creating_Then_ReturnsMessageEmptyError(string? message)
    {
        var result = Notification.Create(MemberId, NotificationType.ReservationCreated, message!, ListingId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notification.MessageEmpty");
        result.Error.Message.Should().Be("Message must not be empty.");
    }

    [Fact]
    public void Given_MessageLongerThanMax_When_Creating_Then_ReturnsMessageTooLongError()
    {
        var message = new string('a', Notification.MaxMessageLength + 1);

        var result = Notification.Create(MemberId, NotificationType.ReservationCreated, message, ListingId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Notification.MessageTooLong");
        result.Error.Message.Should().Be($"Message must have at most {Notification.MaxMessageLength} characters.");
    }

    [Fact]
    public void Given_MessageOfExactlyMaxLength_When_Creating_Then_Succeeds()
    {
        var message = new string('a', Notification.MaxMessageLength);

        var result = Notification.Create(MemberId, NotificationType.ReservationCreated, message, ListingId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().HaveLength(Notification.MaxMessageLength);
    }

    [Fact]
    public void Given_MessageOfMaxLengthWithSpacesAround_When_Creating_Then_Succeeds()
    {
        var message = "  " + new string('a', Notification.MaxMessageLength) + "  ";

        var result = Notification.Create(MemberId, NotificationType.ReservationCreated, message, ListingId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().HaveLength(Notification.MaxMessageLength);
    }

    [Fact]
    public void Given_UnreadNotification_When_MarkingAsRead_Then_SetsReadAt()
    {
        var notification = BuildNotification();

        notification.MarkAsRead(Now.AddHours(1));

        notification.ReadAt.Should().Be(Now.AddHours(1));
        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void Given_ReadNotification_When_MarkingAsReadAgain_Then_KeepsFirstReadAt()
    {
        var notification = BuildNotification();
        notification.MarkAsRead(Now.AddHours(1));

        notification.MarkAsRead(Now.AddHours(2));

        notification.ReadAt.Should().Be(Now.AddHours(1));
        notification.IsRead.Should().BeTrue();
    }
}
