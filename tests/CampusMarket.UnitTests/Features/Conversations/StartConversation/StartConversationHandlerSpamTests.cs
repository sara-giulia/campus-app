using CampusMarket.Api.Common.Moderation;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.StartConversation;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.StartConversation;

public class StartConversationHandlerSpamTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private const string SpamMessage =
        "The message looks like a scam (money or card data asked for, or a suspicious link) and was not sent.";

    private readonly Member _seller = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _buyer = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Listing _listing;
    private readonly Listing _draft;
    private readonly FakeListingRepository _listings = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeMemberBlockRepository _blocks = new();
    private readonly FakeMessageSpamDetector _detector = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly StartConversationHandler _handler;

    public StartConversationHandlerSpamTests()
    {
        _listing = Listing.Create(_seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        _listing.Publish(Now.AddDays(30));
        _draft = Listing.Create(_seller.Id, "Physics book", Category.Textbook, Condition.Good, 30m, Now).Value;
        _listings.Items[_listing.Id] = _listing;
        _listings.Items[_draft.Id] = _draft;
        _members.Items[_seller.Id] = _seller;
        _members.Items[_buyer.Id] = _buyer;
        _handler = new StartConversationHandler(_listings, _members, _conversations, _blocks, _detector, _unitOfWork, new FixedTimeProvider(Now));
    }

    private Conversation SeedExistingConversation()
    {
        var existing = Conversation.Start(_buyer.Id, _listing, "Salut", Now.AddMinutes(-5)).Value;
        _conversations.Items.Add(existing);
        return existing;
    }

    [Fact]
    public async Task Given_CleanVerdict_When_StartingConversation_Then_SavesMessageWithProbabilityAndNotSuspicious()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Clean, 0.05);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Mai e disponibil?"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuspicious.Should().BeFalse();
        _conversations.Items.Should().ContainSingle();
        _conversations.Items[0].Messages[0].SpamProbability.Should().Be(0.05);
        _conversations.Items[0].Messages[0].IsSuspicious.Should().BeFalse();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SuspiciousVerdict_When_StartingConversation_Then_SavesMessageAsSuspicious()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Suspicious, 0.7);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Intra pe www.oferta.ro"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuspicious.Should().BeTrue();
        _conversations.Items.Should().ContainSingle();
        _conversations.Items[0].Messages[0].SpamProbability.Should().Be(0.7);
        _conversations.Items[0].Messages[0].IsSuspicious.Should().BeTrue();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BlockedVerdictAndNoConversation_When_StartingConversation_Then_ReturnsMessageSpamAndAddsNoConversation()
    {
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Blocked, 0.99);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Trimite 150 lei avans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Spam");
        result.Error.Message.Should().Be(SpamMessage);
        _conversations.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingConversationAndCleanVerdict_When_BuyerWritesAgain_Then_SavesMessageWithProbability()
    {
        var existing = SeedExistingConversation();
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Clean, 0.05);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Mai e disponibil?"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be(2);
        result.Value.IsSuspicious.Should().BeFalse();
        existing.Messages[1].SpamProbability.Should().Be(0.05);
        _conversations.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ExistingConversationAndBlockedVerdict_When_BuyerWritesAgain_Then_ReturnsMessageSpamAndDoesNotCommit()
    {
        SeedExistingConversation();
        _detector.Result = new MessageSpamCheck(MessageSpamVerdict.Blocked, 0.99);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Trimite 150 lei avans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Spam");
        result.Error.Message.Should().Be(SpamMessage);
        _conversations.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TextWithSpaces_When_StartingConversation_Then_DetectorGetsTrimmedTextOnce()
    {
        await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "  Buna  "), CancellationToken.None);

        _detector.CallCount.Should().Be(1);
        _detector.LastText.Should().Be("Buna");
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, Guid.NewGuid(), "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Listing.NotFound");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new StartConversationCommand(Guid.NewGuid(), _listing.Id, "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Member.NotFound");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerBlockedBuyer_When_Handling_Then_DetectorIsNotCalled()
    {
        _blocks.Add(MemberBlock.Create(_seller.Id, _buyer.Id, Now).Value);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Member.Blocked");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerOnOwnListing_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new StartConversationCommand(_seller.Id, _listing.Id, "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Conversation.OwnListing");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _draft.Id, "Salut"), CancellationToken.None);

        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyTextAndNoConversation_When_Handling_Then_DetectorIsNotCalled()
    {
        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "   "), CancellationToken.None);

        result.Error.Code.Should().Be("Message.Empty");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyTextAndExistingConversation_When_Handling_Then_DetectorIsNotCalled()
    {
        SeedExistingConversation();

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, "   "), CancellationToken.None);

        result.Error.Code.Should().Be("Message.Empty");
        _detector.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TooLongText_When_Handling_Then_DetectorIsNotCalled()
    {
        var text = new string('a', Message.MaxTextLength + 1);

        var result = await _handler.HandleAsync(new StartConversationCommand(_buyer.Id, _listing.Id, text), CancellationToken.None);

        result.Error.Code.Should().Be("Message.TooLong");
        _detector.CallCount.Should().Be(0);
    }
}
