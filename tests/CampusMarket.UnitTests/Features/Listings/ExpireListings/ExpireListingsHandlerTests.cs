using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Listings.ExpireListings;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Listings.ExpireListings;

public class ExpireListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = Now.AddDays(30);
    private static readonly DateTimeOffset AfterExpiry = ExpiresAt.AddHours(1);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherSellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BuyerId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MissingSellerId = new("99999999-9999-9999-9999-999999999999");

    private readonly FakeListingRepository _listings = new();
    private readonly FakeMemberRepository _members = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly List<string> _events = new();
    private readonly RecordingUnitOfWork _unitOfWork;
    private readonly RecordingEmailSender _emailSender;
    private readonly MovableTimeProvider _time = new(AfterExpiry);

    public ExpireListingsHandlerTests()
    {
        _unitOfWork = new RecordingUnitOfWork(_events);
        _emailSender = new RecordingEmailSender(_events);
        _members.Items[SellerId] = Member.Create("Seller One", "seller1@uni.edu", Now).Value;
        _members.Items[OtherSellerId] = Member.Create("Seller Two", "seller2@uni.edu", Now).Value;
    }

    private ExpireListingsHandler CreateHandler() =>
        new(_listings,
            _members,
            _notifications,
            _unitOfWork,
            _emailSender,
            Options.Create(new FrontendOptions { BaseUrl = "https://campus.example/" }),
            _time,
            NullLogger<ExpireListingsHandler>.Instance);

    private ExpireListingsHandler CreateHandler(ILogger<ExpireListingsHandler> logger) =>
        new(_listings,
            _members,
            _notifications,
            _unitOfWork,
            _emailSender,
            Options.Create(new FrontendOptions { BaseUrl = "https://campus.example/" }),
            _time,
            logger);

    private Listing AddListing(Guid sellerId, string title, ListingStatus status, DateTimeOffset? expiresAt = null)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        if (status != ListingStatus.Draft)
            listing.Publish(expiresAt ?? ExpiresAt);
        if (status == ListingStatus.Reserved)
            listing.Reserve(BuyerId, Now);
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    [Fact]
    public async Task Given_NoListings_When_Handling_Then_ReturnsZeroAndDoesNotSaveOrEmail()
    {
        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(0);
        _listings.UpdateCount.Should().Be(0);
        _notifications.Items.Should().BeEmpty();
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OneExpiredAndOthersNot_When_Handling_Then_ExpiresOnlyThatOneAndNotifiesAndEmailsTheSeller()
    {
        var expired = AddListing(SellerId, "Algebra book", ListingStatus.Active, ExpiresAt);
        var notYet = AddListing(SellerId, "Not yet", ListingStatus.Active, AfterExpiry.AddDays(1));
        var reserved = AddListing(SellerId, "Reserved", ListingStatus.Reserved, ExpiresAt);
        var draft = AddListing(SellerId, "Draft", ListingStatus.Draft);
        var versions = new[] { notYet.Version, reserved.Version, draft.Version };
        var seller = _members.Items[SellerId];

        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        expired.Status.Should().Be(ListingStatus.Expired);
        notYet.Status.Should().Be(ListingStatus.Active);
        reserved.Status.Should().Be(ListingStatus.Reserved);
        draft.Status.Should().Be(ListingStatus.Draft);
        new[] { notYet.Version, reserved.Version, draft.Version }.Should().Equal(versions);
        _listings.UpdateCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);

        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.Type.Should().Be(NotificationType.ListingExpired);
        notification.MemberId.Should().Be(SellerId);
        notification.Message.Should().Be("Your listing Algebra book expired");
        notification.ListingId.Should().Be(expired.Id);

        var email = _emailSender.Items.Should().ContainSingle().Subject;
        email.To.Should().Be(seller.Email.Value);
        email.Subject.Should().Be("Your CampusMarket listing expired");
        email.Body.Should().Contain("Algebra book");
        email.Body.Should().Contain("https://campus.example/my/listings");
        email.Body.Should().Be(
            $"Hello {seller.Name},{Environment.NewLine}{Environment.NewLine}" +
            $"Your listing \"Algebra book\" expired and is no longer shown in the search.{Environment.NewLine}" +
            $"Open My listings and press \"Reînnoiește\" to publish it again:{Environment.NewLine}" +
            "https://campus.example/my/listings");
    }

    [Fact]
    public async Task Given_TwoExpiredListings_When_Handling_Then_ReturnsTwoWithOneCommitTwoNotificationsAndTwoEmails()
    {
        var first = AddListing(SellerId, "First", ListingStatus.Active);
        var second = AddListing(OtherSellerId, "Second", ListingStatus.Active);

        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.Value.Should().Be(2);
        first.Status.Should().Be(ListingStatus.Expired);
        second.Status.Should().Be(ListingStatus.Expired);
        _listings.UpdateCount.Should().Be(2);
        _unitOfWork.SaveCount.Should().Be(1);
        _notifications.Items.Select(n => n.MemberId).Should().BeEquivalentTo(new[] { SellerId, OtherSellerId });
        _emailSender.Items.Select(e => e.To).Should().BeEquivalentTo(
            new[] { _members.Items[SellerId].Email.Value, _members.Items[OtherSellerId].Email.Value });
    }

    [Fact]
    public async Task Given_ClockMovedToExpiresAt_When_HandlingTwice_Then_ExpiresOnlyOnTheSecondRun()
    {
        var listing = AddListing(SellerId, "Algebra book", ListingStatus.Active, ExpiresAt);
        _time.Now = ExpiresAt.AddSeconds(-1);
        var handler = CreateHandler();

        var before = await handler.HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        before.Value.Should().Be(0);
        listing.Status.Should().Be(ListingStatus.Active);
        _unitOfWork.SaveCount.Should().Be(0);

        _time.Now = ExpiresAt;

        var after = await handler.HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        after.Value.Should().Be(1);
        listing.Status.Should().Be(ListingStatus.Expired);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_EmailSenderThrows_When_Handling_Then_ExpiryStaysCommittedAndNotificationAdded()
    {
        var listing = AddListing(SellerId, "Algebra book", ListingStatus.Active);
        _emailSender.ShouldThrow = _ => true;

        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        listing.Status.Should().Be(ListingStatus.Expired);
        _unitOfWork.SaveCount.Should().Be(1);
        _notifications.Items.Should().ContainSingle();
        _emailSender.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Given_EmailSenderThrows_When_Handling_Then_LogsOneWarningWithTheListingId()
    {
        var listing = AddListing(SellerId, "Algebra book", ListingStatus.Active);
        _emailSender.ShouldThrow = _ => true;
        var logger = new FakeLogger<ExpireListingsHandler>();

        var result = await CreateHandler(logger).HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Be($"Listing expiry email for listing {listing.Id} could not be sent.");
    }

    [Fact]
    public async Task Given_EmailSent_When_Handling_Then_LogsNothing()
    {
        AddListing(SellerId, "Algebra book", ListingStatus.Active);
        var logger = new FakeLogger<ExpireListingsHandler>();

        var result = await CreateHandler(logger).HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.Value.Should().Be(1);
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_FirstEmailThrows_When_HandlingTwoSellers_Then_SecondEmailIsStillSent()
    {
        AddListing(SellerId, "First", ListingStatus.Active);
        AddListing(OtherSellerId, "Second", ListingStatus.Active);
        var firstAddress = _members.Items[SellerId].Email.Value;
        var secondAddress = _members.Items[OtherSellerId].Email.Value;
        _emailSender.ShouldThrow = to => to == firstAddress;

        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.Value.Should().Be(2);
        _emailSender.Attempts.Should().Be(2);
        _emailSender.Items.Should().ContainSingle().Which.To.Should().Be(secondAddress);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_EmailSenderCancelled_When_Handling_Then_CancellationPropagatesAfterTheCommit()
    {
        AddListing(SellerId, "Algebra book", ListingStatus.Active);
        _emailSender.ThrowCancelled = true;

        var act = () => CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_SellerDoesNotExist_When_Handling_Then_ExpiryCommittedAndNoEmail()
    {
        var listing = AddListing(MissingSellerId, "Orphan", ListingStatus.Active);

        var result = await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        listing.Status.Should().Be(ListingStatus.Expired);
        _unitOfWork.SaveCount.Should().Be(1);
        _notifications.Items.Should().ContainSingle();
        _emailSender.Attempts.Should().Be(0);
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ExpiredListing_When_Handling_Then_EmailIsSentAfterTheCommit()
    {
        AddListing(SellerId, "Algebra book", ListingStatus.Active);

        await CreateHandler().HandleAsync(new ExpireListingsCommand(), CancellationToken.None);

        _events.Should().Equal("commit", "email");
    }

    private sealed class RecordingUnitOfWork(List<string> events) : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> CommitAsync(CancellationToken ct)
        {
            SaveCount++;
            events.Add("commit");
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingEmailSender(List<string> events) : IEmailSender
    {
        public List<FakeEmailSender.SentEmail> Items { get; } = new();
        public int Attempts { get; private set; }
        public Func<string, bool> ShouldThrow { get; set; } = _ => false;
        public bool ThrowCancelled { get; set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            Attempts++;
            if (ThrowCancelled)
                throw new OperationCanceledException();
            if (ShouldThrow(to))
                throw new InvalidOperationException("SMTP is down");

            events.Add("email");
            Items.Add(new FakeEmailSender.SentEmail(to, subject, body));
            return Task.CompletedTask;
        }
    }
}
