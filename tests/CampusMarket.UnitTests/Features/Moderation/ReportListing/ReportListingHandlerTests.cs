using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.ReportListing;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.ReportListing;

public class ReportListingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");

    private static Member BuildMember(string email = "ana@campus.ro") =>
        Member.Create("Ana Pop", email, Now).Value;

    private static Listing BuildDraftListing(Guid sellerId) =>
        Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildPublishedListing(Guid sellerId)
    {
        var listing = BuildDraftListing(sellerId);
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    private static ReportListingHandler BuildHandler(
        FakeListingRepository listings,
        FakeMemberRepository members,
        FakeReportRepository reports,
        FakeUnitOfWork unitOfWork) =>
        new(listings, members, reports, unitOfWork, new FixedTimeProvider(Now));

    [Fact]
    public async Task Given_PublishedListingAndExistingMember_When_Handling_Then_StoresReportAndSavesOnce()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(member.Id, listing.Id, "  Fake photos  ");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reports.Items.Should().ContainSingle();
        result.Value.Id.Should().Be(reports.Items[0].Id);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.CreatedAt.Should().Be(Now);
        reports.Items[0].ReporterId.Should().Be(member.Id);
        reports.Items[0].ListingId.Should().Be(listing.Id);
        reports.Items[0].Reason.Should().Be("Fake photos");
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownListing_When_Handling_Then_ReturnsListingNotFoundAndDoesNotSave()
    {
        var member = BuildMember();
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var unknownListingId = Guid.NewGuid();
        var command = new ReportListingCommand(member.Id, unknownListingId, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownMember_When_Handling_Then_ReturnsMemberNotFoundAndDoesNotSave()
    {
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var unknownMemberId = Guid.NewGuid();
        var command = new ReportListingCommand(unknownMemberId, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownMemberId}' was not found.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownListingAndUnknownMember_When_Handling_Then_ReturnsListingNotFoundFirst()
    {
        var listings = new FakeListingRepository();
        var members = new FakeMemberRepository();
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var unknownListingId = Guid.NewGuid();
        var command = new ReportListingCommand(Guid.NewGuid(), unknownListingId, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Listing.NotFound");
        result.Error.Message.Should().Be($"Listing '{unknownListingId}' was not found.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_SellerReportsOwnListing_When_Handling_Then_ReturnsOwnListingAndDoesNotSave()
    {
        var seller = BuildMember();
        var listing = BuildPublishedListing(seller.Id);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[seller.Id] = seller;
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(seller.Id, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.OwnListing");
        result.Error.Message.Should().Be("A seller cannot report their own listing.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_DraftListing_When_Handling_Then_ReturnsListingNotAvailableAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildDraftListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(member.Id, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ListingNotAvailable");
        result.Error.Message.Should().Be("Only an active or reserved listing can be reported.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyReason_When_Handling_Then_ReturnsReasonEmptyAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(member.Id, listing.Id, "");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_MemberAlreadyReportedListing_When_Handling_Then_ReturnsAlreadyExistsAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(member.Id, listing, "reason", Now.AddHours(-1)).Value);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(member.Id, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Report.AlreadyExists");
        result.Error.Message.Should().Be("You have already reported this listing.");
        reports.Items.Should().ContainSingle();
        unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_AnotherMemberAlreadyReportedListing_When_Handling_Then_StoresSecondReport()
    {
        var first = BuildMember("ana@campus.ro");
        var second = BuildMember("ion@campus.ro");
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[first.Id] = first;
        members.Items[second.Id] = second;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(first.Id, listing, "reason", Now.AddHours(-1)).Value);
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(second.Id, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        reports.Items.Should().HaveCount(2);
        reports.Items.Select(r => r.ReporterId).Should().BeEquivalentTo(new Guid?[] { first.Id, second.Id });
        unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_RepositoryRejectsId_When_Handling_Then_ReturnsDuplicateAndDoesNotSave()
    {
        var member = BuildMember();
        var listing = BuildPublishedListing(SellerId);
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var members = new FakeMemberRepository();
        members.Items[member.Id] = member;
        var reports = new FakeReportRepository { RejectAdd = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = BuildHandler(listings, members, reports, unitOfWork);
        var command = new ReportListingCommand(member.Id, listing.Id, "Fake photos");

        var result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Report.Duplicate");
        result.Error.Message.Should().Match("A report with id '*' already exists.");
        reports.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);
    }
}
