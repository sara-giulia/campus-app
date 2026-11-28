using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.SuspendMember;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Logging;

namespace CampusMarket.UnitTests.Features.Moderation.SuspendMember;

public class SuspendMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BuyerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherSellerId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReporterId = new("33333333-3333-3333-3333-333333333333");

    private readonly Member _member = Member.Create("Ana Pop", "ana@campus.ro", Now.AddDays(-30)).Value;
    private readonly FakeMemberRepository _members = new();
    private readonly FakeMemberReportRepository _reports = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeListingRepository _listings = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeSuspensionCheck _suspensionCheck = new();
    private readonly FakeLiveEvents _liveEvents = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeLogger<SuspendMemberHandler> _logger = new();
    private readonly SuspendMemberHandler _handler;

    public SuspendMemberHandlerTests()
    {
        _members.Items[_member.Id] = _member;
        _handler = new SuspendMemberHandler(
            _members, _reports, _refreshTokens, _listings, _offers, _notifications, _unitOfWork,
            _suspensionCheck, _liveEvents, _emailSender, new FixedTimeProvider(Now), _logger);
    }

    private Listing SeedActiveListing(Guid sellerId, string title)
    {
        var listing = Listing.Create(sellerId, title, Category.Textbook, Condition.Good, 150m, Now.AddDays(-2)).Value;
        listing.Publish(Now.AddDays(30));
        _listings.Items[listing.Id] = listing;
        return listing;
    }

    private (Listing Listing, Reservation Reservation) SeedReservedListing(Guid sellerId, Guid buyerId, string title)
    {
        var listing = SeedActiveListing(sellerId, title);
        var reservation = listing.Reserve(buyerId, Now.AddHours(-1)).Value;
        return (listing, reservation);
    }

    private Offer SeedOffer(Listing listing, Guid buyerId, decimal amount)
    {
        var offer = Offer.Make(listing, buyerId, amount, Now.AddHours(-2)).Value;
        _offers.Add(offer);
        return offer;
    }

    private Task<Result<Guid>> SuspendAsync(Guid memberId, string? reason = "Scam attempts", DateTimeOffset? until = null) =>
        _handler.HandleAsync(new SuspendMemberCommand(memberId, AdminId, reason, until), CancellationToken.None);

    private static string PermanentBody(string name, string reason)
    {
        var nl = Environment.NewLine;
        var ro =
            $"Salut {name},{nl}{nl}" +
            $"Contul tău CampusMarket a fost suspendat definitiv.{nl}" +
            $"Motiv: {reason}{nl}{nl}" +
            "Nu te mai poți autentifica, iar anunțurile tale nu mai sunt vizibile. " +
            $"Rezervările și ofertele în curs au fost anulate.{nl}" +
            "Dacă crezi că este o greșeală, contactează un administrator CampusMarket.";
        var en =
            $"Hello {name},{nl}{nl}" +
            $"Your CampusMarket account was suspended permanently.{nl}" +
            $"Reason: {reason}{nl}{nl}" +
            "You can no longer sign in and your listings are no longer visible. " +
            $"Your ongoing reservations and offers were cancelled.{nl}" +
            "If you think this is a mistake, contact a CampusMarket administrator.";
        return $"{ro}{nl}{nl}----------{nl}{nl}{en}{nl}";
    }

    [Fact]
    public async Task Given_Student_When_SuspendingUntilADate_Then_SuspendsMemberUpdatesAndSavesOnce()
    {
        var result = await SuspendAsync(_member.Id, "  Scam attempts  ", Now.AddDays(7));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_member.Id);
        _member.SuspendedAt.Should().Be(Now);
        _member.SuspendedUntil.Should().Be(Now.AddDays(7));
        _member.SuspensionReason.Should().Be("Scam attempts");
        _member.SuspendedById.Should().Be(AdminId);
        _members.UpdateCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_Student_When_SuspendingPermanently_Then_HasNoEnd()
    {
        var result = await SuspendAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        _member.SuspendedUntil.Should().BeNull();
        _member.IsSuspended(Now.AddYears(5)).Should().BeTrue();
    }

    [Fact]
    public async Task Given_PendingReports_When_Suspending_Then_ConfirmsThemAndLeavesOthers()
    {
        var first = MemberReport.Create(ReporterId, _member.Id, "Rude", null, Now.AddHours(-3)).Value;
        var second = MemberReport.Create(BuyerId, _member.Id, "Scam", null, Now.AddHours(-2)).Value;
        var otherMember = MemberReport.Create(ReporterId, OtherSellerId, "Rude", null, Now.AddHours(-1)).Value;
        _reports.Add(first);
        _reports.Add(second);
        _reports.Add(otherMember);

        await SuspendAsync(_member.Id);

        first.Resolution.Should().Be(ReportResolution.Confirmed);
        first.ResolvedAt.Should().Be(Now);
        first.ResolvedById.Should().Be(AdminId);
        second.Resolution.Should().Be(ReportResolution.Confirmed);
        second.ResolvedById.Should().Be(AdminId);
        otherMember.IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task Given_RefreshTokens_When_Suspending_Then_RevokesOnlyTheMembersActiveTokens()
    {
        var first = RefreshToken.Create(_member.Id, "token-a", Now.AddHours(-2));
        var second = RefreshToken.Create(_member.Id, "token-b", Now.AddHours(-1));
        var revoked = RefreshToken.Create(_member.Id, "token-c", Now.AddHours(-3));
        revoked.Revoke(Now.AddHours(-2));
        var other = RefreshToken.Create(BuyerId, "token-d", Now.AddHours(-1));
        _refreshTokens.Items[first.Id] = first;
        _refreshTokens.Items[second.Id] = second;
        _refreshTokens.Items[revoked.Id] = revoked;
        _refreshTokens.Items[other.Id] = other;

        await SuspendAsync(_member.Id);

        first.RevokedAt.Should().Be(Now);
        second.RevokedAt.Should().Be(Now);
        revoked.RevokedAt.Should().Be(Now.AddHours(-2));
        other.RevokedAt.Should().BeNull();
        _refreshTokens.UpdateCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_ReservationOnMembersListing_When_Suspending_Then_CancelsItAndNotifiesTheBuyer()
    {
        var (listing, reservation) = SeedReservedListing(_member.Id, BuyerId, "Algebra book");

        await SuspendAsync(_member.Id);

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        listing.Status.Should().Be(ListingStatus.Active);
        _listings.UpdateCount.Should().Be(1);
        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(BuyerId);
        notification.Type.Should().Be(NotificationType.ReservationCancelled);
        notification.Message.Should().Be("Your reservation for Algebra book was cancelled: the seller is no longer available");
        notification.ListingId.Should().Be(listing.Id);
        notification.CreatedAt.Should().Be(Now);
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Given_ReservationMadeByMember_When_Suspending_Then_CancelsItAndNotifiesTheSeller()
    {
        var (listing, reservation) = SeedReservedListing(OtherSellerId, _member.Id, "Physics notes");

        await SuspendAsync(_member.Id);

        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        listing.Status.Should().Be(ListingStatus.Active);
        _listings.UpdateCount.Should().Be(1);
        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(OtherSellerId);
        notification.Type.Should().Be(NotificationType.ReservationCancelled);
        notification.Message.Should().Be("The reservation of Physics notes was cancelled: the buyer is no longer available");
        notification.ListingId.Should().Be(listing.Id);
        notification.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Given_ReservationBetweenTwoOtherMembers_When_Suspending_Then_IsUntouched()
    {
        var (listing, reservation) = SeedReservedListing(OtherSellerId, BuyerId, "Physics notes");

        await SuspendAsync(_member.Id);

        reservation.Status.Should().Be(ReservationStatus.Active);
        listing.Status.Should().Be(ListingStatus.Reserved);
        _listings.UpdateCount.Should().Be(0);
        _notifications.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MembersListingsWithoutActiveReservation_When_Suspending_Then_ListingsAreUntouched()
    {
        var active = SeedActiveListing(_member.Id, "Algebra book");
        var (cancelledListing, cancelled) = SeedReservedListing(_member.Id, BuyerId, "Physics notes");
        cancelledListing.CancelReservation(cancelled.Id);

        await SuspendAsync(_member.Id);

        active.Status.Should().Be(ListingStatus.Active);
        cancelledListing.Status.Should().Be(ListingStatus.Active);
        cancelled.Status.Should().Be(ReservationStatus.Cancelled);
        _listings.UpdateCount.Should().Be(0);
        _notifications.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_PendingOfferOnMembersListing_When_Suspending_Then_RejectsItAndNotifiesTheBuyer()
    {
        var listing = SeedActiveListing(_member.Id, "Algebra book");
        var offer = SeedOffer(listing, BuyerId, 99.50m);

        await SuspendAsync(_member.Id);

        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.RespondedAt.Should().Be(Now);
        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(BuyerId);
        notification.Type.Should().Be(NotificationType.OfferRejected);
        notification.Message.Should().Be("The offer of 99.5 lei for Algebra book was closed: the other member is no longer available");
        notification.ListingId.Should().Be(listing.Id);
        notification.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Given_PendingOfferMadeByMember_When_Suspending_Then_RejectsItAndNotifiesTheSeller()
    {
        var listing = SeedActiveListing(OtherSellerId, "Physics notes");
        var offer = SeedOffer(listing, _member.Id, 120m);

        await SuspendAsync(_member.Id);

        offer.Status.Should().Be(OfferStatus.Rejected);
        offer.RespondedAt.Should().Be(Now);
        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(OtherSellerId);
        notification.Type.Should().Be(NotificationType.OfferRejected);
        notification.Message.Should().Be("The offer of 120 lei for Physics notes was closed: the other member is no longer available");
        notification.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public async Task Given_PendingOfferWhoseListingIsNotInRepository_When_Suspending_Then_NotificationHasEmptyTitle()
    {
        var listing = Listing.Create(OtherSellerId, "Physics notes", Category.Textbook, Condition.Good, 150m, Now.AddDays(-2)).Value;
        listing.Publish(Now.AddDays(30));
        var offer = SeedOffer(listing, _member.Id, 120m);

        await SuspendAsync(_member.Id);

        offer.Status.Should().Be(OfferStatus.Rejected);
        var notification = _notifications.Items.Should().ContainSingle().Subject;
        notification.MemberId.Should().Be(OtherSellerId);
        notification.Message.Should().Be("The offer of 120 lei for  was closed: the other member is no longer available");
    }

    [Fact]
    public async Task Given_PendingOfferBetweenTwoOtherMembers_When_Suspending_Then_IsUntouched()
    {
        var listing = SeedActiveListing(OtherSellerId, "Physics notes");
        var offer = SeedOffer(listing, BuyerId, 120m);

        await SuspendAsync(_member.Id);

        offer.Status.Should().Be(OfferStatus.Pending);
        offer.RespondedAt.Should().BeNull();
        _notifications.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_OfferStoredPendingButWithdrawnInMemory_When_Suspending_Then_NoNotification()
    {
        var listing = SeedActiveListing(OtherSellerId, "Physics notes");
        var offer = SeedOffer(listing, _member.Id, 120m);
        offer.Withdraw(_member.Id, Now.AddMinutes(-5));

        await SuspendAsync(_member.Id);

        offer.Status.Should().Be(OfferStatus.Withdrawn);
        offer.RespondedAt.Should().Be(Now.AddMinutes(-5));
        _notifications.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_Success_When_Suspending_Then_InvalidatesCacheAndDisconnectsTheMember()
    {
        await SuspendAsync(_member.Id);

        _suspensionCheck.InvalidateCount.Should().Be(1);
        _liveEvents.Disconnected.Should().Equal(_member.Id);
    }

    [Fact]
    public async Task Given_Success_When_Suspending_Then_SendsTheFullEmailAndLogsNothing()
    {
        await SuspendAsync(_member.Id, "  Scam attempts  ");

        var email = _emailSender.Items.Should().ContainSingle().Subject;
        email.To.Should().Be("ana@campus.ro");
        email.Subject.Should().Be("Contul tău CampusMarket a fost suspendat / Your CampusMarket account was suspended");
        email.Body.Should().Be(PermanentBody("Ana Pop", "Scam attempts"));
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_EmailFails_When_Suspending_Then_SucceedsCommitsAndLogsWarning()
    {
        _emailSender.ThrowOnSend = new InvalidOperationException("SMTP down");

        var result = await SuspendAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        _member.IsSuspended(Now).Should().BeTrue();
        _unitOfWork.SaveCount.Should().Be(1);
        _suspensionCheck.InvalidateCount.Should().Be(1);
        _liveEvents.Disconnected.Should().Equal(_member.Id);
        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Be($"Suspension email for member {_member.Id} could not be sent.");
    }

    [Fact]
    public async Task Given_EmailCancelled_When_Suspending_Then_CancellationPropagates()
    {
        _emailSender.ThrowOnSend = new OperationCanceledException();

        var act = () => SuspendAsync(_member.Id);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownMember_When_Suspending_Then_ReturnsNotFoundAndDoesNothing()
    {
        var unknownId = Guid.NewGuid();

        var result = await SuspendAsync(unknownId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        _unitOfWork.SaveCount.Should().Be(0);
        _members.UpdateCount.Should().Be(0);
        _suspensionCheck.InvalidateCount.Should().Be(0);
        _liveEvents.Disconnected.Should().BeEmpty();
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_AdminSuspendsThemselves_When_Handling_Then_ReturnsSuspensionSelf()
    {
        var result = await _handler.HandleAsync(
            new SuspendMemberCommand(_member.Id, _member.Id, "Scam attempts", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Suspension.Self");
        result.Error.Message.Should().Be("You cannot suspend yourself.");
        _member.SuspendedAt.Should().BeNull();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AlreadySuspendedMember_When_Suspending_Then_ReturnsConflictAndChangesNothing()
    {
        _member.Suspend(AdminId, "First", null, Now.AddDays(-1));
        var report = MemberReport.Create(ReporterId, _member.Id, "Rude", null, Now.AddHours(-1)).Value;
        _reports.Add(report);
        var token = RefreshToken.Create(_member.Id, "token-a", Now.AddHours(-1));
        _refreshTokens.Items[token.Id] = token;
        var (listing, reservation) = SeedReservedListing(_member.Id, BuyerId, "Algebra book");
        var offerListing = SeedActiveListing(OtherSellerId, "Physics notes");
        var offer = SeedOffer(offerListing, _member.Id, 120m);

        var result = await SuspendAsync(_member.Id, "Second");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.AlreadySuspended");
        result.Error.Message.Should().Be("The member is already suspended. Lift the suspension first.");
        _member.SuspensionReason.Should().Be("First");
        report.IsPending.Should().BeTrue();
        token.RevokedAt.Should().BeNull();
        reservation.Status.Should().Be(ReservationStatus.Active);
        listing.Status.Should().Be(ListingStatus.Reserved);
        offer.Status.Should().Be(OfferStatus.Pending);
        _notifications.Items.Should().BeEmpty();
        _members.UpdateCount.Should().Be(0);
        _listings.UpdateCount.Should().Be(0);
        _refreshTokens.UpdateCount.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(0);
        _suspensionCheck.InvalidateCount.Should().Be(0);
        _liveEvents.Disconnected.Should().BeEmpty();
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_EmptyReason_When_Suspending_Then_ReturnsReasonEmptyAndSavesNothing()
    {
        var result = await SuspendAsync(_member.Id, "  ");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Suspension.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
        _member.SuspendedAt.Should().BeNull();
        _unitOfWork.SaveCount.Should().Be(0);
        _emailSender.Items.Should().BeEmpty();
    }
}
