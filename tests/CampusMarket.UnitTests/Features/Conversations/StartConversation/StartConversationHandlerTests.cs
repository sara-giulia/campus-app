using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Conversations.StartConversation;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Conversations.StartConversation;

public class StartConversationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Member BuildSeller() =>
        Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;

    private static Member BuildBuyer() =>
        Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Listing BuildDraftListing(Guid sellerId) =>
        Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildActiveListing(Guid sellerId)
    {
        var listing = BuildDraftListing(sellerId);
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static StartConversationHandler BuildHandler(
        FakeListingRepository listings,
        FakeMemberRepository members,
        FakeConversationRepository conversations,
        FakeUnitOfWork unitOfWork) =>
        new(listings, members, conversations, new FakeMemberBlockRepository(), new FakeMessageSpamDetector(), unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_NoConversationYet_When_Handling_Then_StartsConversationAndSavesOnce()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "  Mai e disponibil?  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        conversations.Items.Should().ContainSingle();
        result.Value.ConversationId.Should().Be(conversations.Items[0].Id);
        result.Value.Id.Should().Be(conversations.Items[0].Messages[0].Id);
        result.Value.Number.Should().Be(1);
        result.Value.SenderId.Should().Be(buyer.Id);
        result.Value.Text.Should().Be("Mai e disponibil?");
        result.Value.SentAt.Should().Be(Now);
        conversations.Items[0].ListingId.Should().Be(listing.Id);
        conversations.Items[0].BuyerId.Should().Be(buyer.Id);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_BuyerAlreadyHasConversation_When_Handling_Then_AddsMessageToItAndSavesOnce()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var existing = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "  Pot sa vin azi?  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        conversations.Items.Should().ContainSingle();
        result.Value.ConversationId.Should().Be(existing.Id);
        result.Value.Number.Should().Be(2);
        result.Value.SenderId.Should().Be(buyer.Id);
        result.Value.Text.Should().Be("Pot sa vin azi?");
        result.Value.SentAt.Should().Be(Now);
        existing.Messages.Should().HaveCount(2);
        existing.LastMessageAt.Should().Be(Now);
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ExistingConversationAndListingNowSold_When_Handling_Then_StillSendsMessage()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var existing = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        var reservation = listing.Reserve(buyer.Id, Now.AddMinutes(-5)).Value;
        listing.ConfirmSale(reservation.Id, Now.AddMinutes(-1));
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "Multumesc!");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        listing.Status.Should().Be(ListingStatus.Sold);
        result.Value.ConversationId.Should().Be(existing.Id);
        result.Value.Number.Should().Be(2);
        existing.Messages.Should().HaveCount(2);
        conversations.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsListingNotFoundAndDoesNotSave()
    {
        var buyer = BuildBuyer();
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listingId, "Mai e disponibil?");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsMemberNotFoundAndDoesNotSave()
    {
        var seller = BuildSeller();
        var listing = BuildActiveListing(seller.Id);
        var buyerId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyerId, listing.Id, "Mai e disponibil?");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{buyerId}' was not found.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListingAndUnknownMember_When_Handling_Then_ReturnsListingNotFound()
    {
        var listingId = Guid.NewGuid();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(Guid.NewGuid(), listingId, "Mai e disponibil?");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{listingId}' was not found.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_Seller_When_Handling_Then_ReturnsOwnListingAndDoesNotSave()
    {
        var seller = BuildSeller();
        var listing = BuildActiveListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(seller.Id, listing.Id, "Mai e disponibil?");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.OwnListing");
        result.Error.Message.Should().Be("A seller cannot start a conversation about their own listing.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsListingNotAvailableAndDoesNotSave()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildDraftListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "Mai e disponibil?");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Conversation.ListingNotAvailable");
        result.Error.Message.Should().Be("A conversation can only be started about a published listing.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoConversationAndEmptyText_When_Handling_Then_ReturnsMessageEmptyAndDoesNotSave()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "   ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Empty");
        result.Error.Message.Should().Be("The message must not be empty.");
        conversations.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ExistingConversationAndEmptyText_When_Handling_Then_ReturnsMessageEmptyAndAddsNothing()
    {
        var seller = BuildSeller();
        var buyer = BuildBuyer();
        var listing = BuildActiveListing(seller.Id);
        var existing = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now.AddMinutes(-10)).Value;
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        members.Items[buyer.Id] = buyer;
        var conversations = new FakeConversationRepository();
        conversations.Items.Add(existing);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, conversations, unitOfWork);
        var command = new StartConversationCommand(buyer.Id, listing.Id, "");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Message.Empty");
        result.Error.Message.Should().Be("The message must not be empty.");
        conversations.Items.Should().ContainSingle();
        existing.Messages.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
