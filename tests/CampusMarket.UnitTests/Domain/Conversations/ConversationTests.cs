using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Conversations;

public class ConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildActiveListing()
    {
        var listing = BuildDraftListing();
        listing.Publish();
        return listing;
    }

    private static Listing BuildReservedListing()
    {
        var listing = BuildActiveListing();
        listing.Reserve(OtherMemberId, Now);
        return listing;
    }

    private static Listing BuildSoldListing()
    {
        var listing = BuildActiveListing();
        var reservation = listing.Reserve(OtherMemberId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);
        return listing;
    }

    private static Listing BuildHiddenListing()
    {
        var listing = BuildActiveListing();
        listing.Hide();
        return listing;
    }

    private static Conversation BuildConversation() =>
        Conversation.Start(BuyerId, BuildActiveListing(), "Mai e disponibil?", Now).Value;

    [Fact]
    public void Given_ActiveListing_When_Starting_Then_ReturnsConversationWithFirstMessage()
    {
        var listing = BuildActiveListing();

        var result = Conversation.Start(BuyerId, listing, "  Mai e disponibil?  ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.SellerId.Should().Be(SellerId);
        result.Value.BuyerId.Should().Be(BuyerId);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.LastMessageAt.Should().Be(Now);
        result.Value.Messages.Should().ContainSingle();
        result.Value.Messages[0].Number.Should().Be(1);
        result.Value.Messages[0].SenderId.Should().Be(BuyerId);
        result.Value.Messages[0].Text.Should().Be("Mai e disponibil?");
        result.Value.Messages[0].SentAt.Should().Be(Now);
        result.Value.Messages[0].ConversationId.Should().Be(result.Value.Id);
        result.Value.Messages[0].Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Given_ReservedListing_When_Starting_Then_ReturnsSuccess()
    {
        var listing = BuildReservedListing();

        var result = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public void Given_DraftListing_When_Starting_Then_ReturnsListingNotAvailable()
    {
        var listing = BuildDraftListing();

        var result = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        result.Error.Message.Should().Be("A conversation can only be started about a published listing.");
    }

    [Fact]
    public void Given_SoldListing_When_Starting_Then_ReturnsListingNotAvailable()
    {
        var listing = BuildSoldListing();

        var result = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        result.Error.Message.Should().Be("A conversation can only be started about a published listing.");
    }

    [Fact]
    public void Given_HiddenListing_When_Starting_Then_ReturnsListingNotAvailable()
    {
        var listing = BuildHiddenListing();

        var result = Conversation.Start(BuyerId, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        result.Error.Message.Should().Be("A conversation can only be started about a published listing.");
    }

    [Fact]
    public void Given_Seller_When_Starting_Then_ReturnsOwnListing()
    {
        var listing = BuildActiveListing();

        var result = Conversation.Start(SellerId, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.OwnListing");
        result.Error.Message.Should().Be("A seller cannot start a conversation about their own listing.");
    }

    [Fact]
    public void Given_EmptyBuyerId_When_Starting_Then_ReturnsBuyerEmpty()
    {
        var listing = BuildActiveListing();

        var result = Conversation.Start(Guid.Empty, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.BuyerEmpty");
        result.Error.Message.Should().Be("Buyer id must not be empty.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyText_When_Starting_Then_ReturnsMessageEmpty(string? text)
    {
        var listing = BuildActiveListing();

        var result = Conversation.Start(BuyerId, listing, text, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Empty");
        result.Error.Message.Should().Be("The message must not be empty.");
    }

    [Fact]
    public void Given_TextOfMaxLength_When_Starting_Then_ReturnsSuccess()
    {
        var listing = BuildActiveListing();
        var text = new string('a', Message.MaxTextLength);

        var result = Conversation.Start(BuyerId, listing, text, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Messages[0].Text.Should().Be(text);
    }

    [Fact]
    public void Given_TextLongerThanMax_When_Starting_Then_ReturnsMessageTooLong()
    {
        var listing = BuildActiveListing();
        var text = new string('a', Message.MaxTextLength + 1);

        var result = Conversation.Start(BuyerId, listing, text, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.TooLong");
        result.Error.Message.Should().Be($"The message must have at most {Message.MaxTextLength} characters.");
    }

    [Fact]
    public void Given_EmptyBuyerIdOnDraftListing_When_Starting_Then_ReturnsBuyerEmpty()
    {
        var listing = BuildDraftListing();

        var result = Conversation.Start(Guid.Empty, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.BuyerEmpty");
        result.Error.Message.Should().Be("Buyer id must not be empty.");
    }

    [Fact]
    public void Given_SellerOnDraftListing_When_Starting_Then_ReturnsOwnListing()
    {
        var listing = BuildDraftListing();

        var result = Conversation.Start(SellerId, listing, "Mai e disponibil?", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.OwnListing");
        result.Error.Message.Should().Be("A seller cannot start a conversation about their own listing.");
    }

    [Fact]
    public void Given_EmptyTextOnDraftListing_When_Starting_Then_ReturnsListingNotAvailable()
    {
        var listing = BuildDraftListing();

        var result = Conversation.Start(BuyerId, listing, "", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        result.Error.Message.Should().Be("A conversation can only be started about a published listing.");
    }

    [Fact]
    public void Given_Conversation_When_SellerAnswers_Then_AddsMessageNumberTwo()
    {
        var conversation = BuildConversation();
        var later = Now.AddMinutes(5);

        var result = conversation.Send(SellerId, "  Da, este  ", later);

        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be(2);
        result.Value.SenderId.Should().Be(SellerId);
        result.Value.Text.Should().Be("Da, este");
        result.Value.SentAt.Should().Be(later);
        result.Value.ConversationId.Should().Be(conversation.Id);
        conversation.Messages.Should().HaveCount(2);
        conversation.LastMessageAt.Should().Be(later);
    }

    [Fact]
    public void Given_ConversationWithTwoMessages_When_BuyerWritesAgain_Then_AddsMessageNumberThree()
    {
        var conversation = BuildConversation();
        conversation.Send(SellerId, "Da, este", Now.AddMinutes(5));
        var later = Now.AddMinutes(10);

        var result = conversation.Send(BuyerId, "Il iau maine", later);

        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be(3);
        result.Value.SenderId.Should().Be(BuyerId);
        conversation.Messages.Should().HaveCount(3);
        conversation.LastMessageAt.Should().Be(later);
    }

    [Fact]
    public void Given_Conversation_When_OtherMemberSends_Then_ReturnsForbiddenAndChangesNothing()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(OtherMemberId, "Salut", Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can write in this conversation.");
        conversation.Messages.Should().ContainSingle();
        conversation.LastMessageAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Conversation_When_SendingEmptyText_Then_ReturnsMessageEmptyAndAddsNothing()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(SellerId, "   ", Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Empty");
        result.Error.Message.Should().Be("The message must not be empty.");
        conversation.Messages.Should().ContainSingle();
        conversation.LastMessageAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Conversation_When_SendingTooLongText_Then_ReturnsMessageTooLongAndAddsNothing()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(SellerId, new string('a', Message.MaxTextLength + 1), Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.TooLong");
        result.Error.Message.Should().Be($"The message must have at most {Message.MaxTextLength} characters.");
        conversation.Messages.Should().ContainSingle();
        conversation.LastMessageAt.Should().Be(Now);
    }

    [Fact]
    public void Given_Conversation_When_OtherMemberSendsEmptyText_Then_ReturnsNotParticipant()
    {
        var conversation = BuildConversation();

        var result = conversation.Send(OtherMemberId, "", Now.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Conversation.NotParticipant");
        result.Error.Message.Should().Be("Only the buyer and the seller can write in this conversation.");
        conversation.Messages.Should().ContainSingle();
    }

    [Fact]
    public void Given_Conversation_When_CheckingBuyer_Then_IsParticipant()
    {
        var conversation = BuildConversation();

        var result = conversation.IsParticipant(BuyerId);

        result.Should().BeTrue();
    }

    [Fact]
    public void Given_Conversation_When_CheckingSeller_Then_IsParticipant()
    {
        var conversation = BuildConversation();

        var result = conversation.IsParticipant(SellerId);

        result.Should().BeTrue();
    }

    [Fact]
    public void Given_Conversation_When_CheckingOtherMember_Then_IsNotParticipant()
    {
        var conversation = BuildConversation();

        var result = conversation.IsParticipant(OtherMemberId);

        result.Should().BeFalse();
    }

    [Fact]
    public void Given_Conversation_When_SellerAsksForOtherParticipant_Then_ReturnsBuyer()
    {
        var conversation = BuildConversation();

        var result = conversation.OtherParticipant(SellerId);

        result.Should().Be(BuyerId);
    }

    [Fact]
    public void Given_Conversation_When_BuyerAsksForOtherParticipant_Then_ReturnsSeller()
    {
        var conversation = BuildConversation();

        var result = conversation.OtherParticipant(BuyerId);

        result.Should().Be(SellerId);
    }
}
