using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Conversations.SendMessage;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.SendMessage;

public class SendMessageHandlerSuspendedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _stranger = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly Conversation _conversation;
    private readonly FakeMessageSpamDetector _detector = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MovableTimeProvider _clock = new(Now);
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerSuspendedTests()
    {
        var members = new FakeMemberRepository();
        members.Items[_seller.Id] = _seller;
        members.Items[_buyer.Id] = _buyer;
        members.Items[_stranger.Id] = _stranger;
        var listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        _conversation = Conversation.Start(_buyer.Id, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(_conversation);
        _handler = new SendMessageHandler(conversations, new FakeMemberBlockRepository(), members, _detector, _unitOfWork, _clock);
    }

    private Task<Result<MessageResponse>> SendAsync(Guid senderId) =>
        _handler.HandleAsync(new SendMessageCommand(senderId, _conversation.Id, "Da, este"), CancellationToken.None);

    [Fact]
    public async Task Given_SuspendedSeller_When_BuyerWrites_Then_ReturnsMemberSuspendedAndSendsNothing()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);

        var result = await SendAsync(_buyer.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Conversation.MemberSuspended");
        result.Error.Message.Should().Be("This member is not available at the moment.");
        _conversation.Messages.Should().ContainSingle();
        _detector.CallCount.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedBuyer_When_SellerWrites_Then_ReturnsMemberSuspended()
    {
        _buyer.Suspend(AdminId, "Scam", null, Now);

        var result = await SendAsync(_seller.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conversation.MemberSuspended");
        _conversation.Messages.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedSeller_When_StrangerWrites_Then_ReturnsNotParticipant()
    {
        _seller.Suspend(AdminId, "Scam", null, Now);

        var result = await SendAsync(_stranger.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SuspendedStranger_When_BuyerWrites_Then_Succeeds()
    {
        _stranger.Suspend(AdminId, "Scam", null, Now);

        var result = await SendAsync(_buyer.Id);

        result.IsSuccess.Should().BeTrue();
        _conversation.Messages.Should().HaveCount(2);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspensionOver_When_BuyerWrites_Then_Succeeds()
    {
        _seller.Suspend(AdminId, "Scam", Now.AddDays(1), Now);
        _clock.Now = Now.AddDays(1);

        var result = await SendAsync(_buyer.Id);

        result.IsSuccess.Should().BeTrue();
        _conversation.Messages.Should().HaveCount(2);
    }
}
