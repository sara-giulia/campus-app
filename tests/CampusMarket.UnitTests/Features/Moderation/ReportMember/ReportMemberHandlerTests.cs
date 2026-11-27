using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.ReportMember;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.ReportMember;

public class ReportMemberHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _reporter = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
    private readonly Member _reported = Member.Create("Ion Ionescu", "ion@campus.ro", Now).Value;
    private readonly Member _third = Member.Create("Dan Popa", "dan@campus.ro", Now).Value;
    private readonly FakeMemberRepository _members = new();
    private readonly FakeConversationRepository _conversations = new();
    private readonly FakeMemberReportRepository _reports = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ReportMemberHandler _handler;

    public ReportMemberHandlerTests()
    {
        _members.Items[_reporter.Id] = _reporter;
        _members.Items[_reported.Id] = _reported;
        _members.Items[_third.Id] = _third;
        _handler = new ReportMemberHandler(_members, _conversations, _reports, _unitOfWork, new FixedTimeProvider(Now));
    }

    private Conversation SeedConversation(Member buyer, Member seller)
    {
        var listing = Listing.Create(seller.Id, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        var conversation = Conversation.Start(buyer.Id, listing, "Mai e disponibil?", Now).Value;
        _conversations.Items.Add(conversation);
        return conversation;
    }

    private Task<Result<ReportMemberResponse>> ReportAsync(Guid reporterId, Guid reportedId, string? reason, Guid? conversationId = null) =>
        _handler.HandleAsync(new ReportMemberCommand(reporterId, reportedId, reason, conversationId), CancellationToken.None);

    [Fact]
    public async Task Given_ValidReport_When_Handling_Then_StoresPendingReportAndSavesOnce()
    {
        var result = await ReportAsync(_reporter.Id, _reported.Id, "  Rude messages  ");

        result.IsSuccess.Should().BeTrue();
        var stored = _reports.Items.Should().ContainSingle().Subject;
        result.Value.Id.Should().Be(stored.Id);
        result.Value.ReportedMemberId.Should().Be(_reported.Id);
        result.Value.CreatedAt.Should().Be(Now);
        stored.ReporterId.Should().Be(_reporter.Id);
        stored.ReportedMemberId.Should().Be(_reported.Id);
        stored.Reason.Should().Be("Rude messages");
        stored.ConversationId.Should().BeNull();
        stored.CreatedAt.Should().Be(Now);
        stored.IsPending.Should().BeTrue();
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ConversationBetweenTheTwo_When_Handling_Then_StoresConversationId()
    {
        var conversation = SeedConversation(_reporter, _reported);

        var result = await ReportAsync(_reporter.Id, _reported.Id, "Rude messages", conversation.Id);

        result.IsSuccess.Should().BeTrue();
        _reports.Items.Should().ContainSingle().Which.ConversationId.Should().Be(conversation.Id);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_UnknownReportedMember_When_Handling_Then_ReturnsNotFoundAndSavesNothing()
    {
        var unknownId = Guid.NewGuid();

        var result = await ReportAsync(_reporter.Id, unknownId, "Rude messages");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownReporter_When_Handling_Then_ReturnsNotFoundWithReporterId()
    {
        var unknownId = Guid.NewGuid();

        var result = await ReportAsync(unknownId, _reported.Id, "Rude messages");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_BothMembersUnknown_When_Handling_Then_ReturnsReportedMemberNotFoundFirst()
    {
        var unknownReporter = Guid.NewGuid();
        var unknownReported = Guid.NewGuid();

        var result = await ReportAsync(unknownReporter, unknownReported, "Rude messages");

        result.Error.Message.Should().Be($"Member '{unknownReported}' was not found.");
    }

    [Fact]
    public async Task Given_UnknownConversation_When_Handling_Then_ReturnsConversationInvalidAndSavesNothing()
    {
        var result = await ReportAsync(_reporter.Id, _reported.Id, "Rude messages", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
        result.Error.Message.Should().Be("The conversation must be between you and the reported member.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_UnknownConversationAndEmptyReason_When_Handling_Then_ReturnsConversationInvalidFirst()
    {
        var result = await ReportAsync(_reporter.Id, _reported.Id, "", Guid.NewGuid());

        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
    }

    [Fact]
    public async Task Given_ConversationWithThirdMember_When_Handling_Then_ReturnsConversationInvalidAndSavesNothing()
    {
        var conversation = SeedConversation(_reporter, _third);

        var result = await ReportAsync(_reporter.Id, _reported.Id, "Rude messages", conversation.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
        result.Error.Message.Should().Be("The conversation must be between you and the reported member.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_ReporterReportsThemselves_When_Handling_Then_ReturnsSelfAndSavesNothing()
    {
        var result = await ReportAsync(_reporter.Id, _reporter.Id, "Rude messages");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.Self");
        result.Error.Message.Should().Be("You cannot report yourself.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_EmptyReason_When_Handling_Then_ReturnsReasonEmptyAndSavesNothing()
    {
        var result = await ReportAsync(_reporter.Id, _reported.Id, "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
        _reports.Items.Should().BeEmpty();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingReportOfSamePair_When_Handling_Then_ReturnsAlreadyExistsAndKeepsOneReport()
    {
        _reports.Add(MemberReport.Create(_reporter.Id, _reported.Id, "First", null, Now.AddHours(-1)).Value);

        var result = await ReportAsync(_reporter.Id, _reported.Id, "Second");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("MemberReport.AlreadyExists");
        result.Error.Message.Should().Be("You have already reported this member; an Admin will check it.");
        _reports.Items.Should().ContainSingle();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_PendingReportAndTooLongReason_When_Handling_Then_ReturnsReasonTooLongBeforeAlreadyExists()
    {
        _reports.Add(MemberReport.Create(_reporter.Id, _reported.Id, "First", null, Now.AddHours(-1)).Value);

        var result = await ReportAsync(_reporter.Id, _reported.Id, new string('a', Report.MaxReasonLength + 1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ReasonTooLong");
        result.Error.Message.Should().Be($"Reason must have at most {Report.MaxReasonLength} characters.");
        _reports.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Given_ResolvedReportOfSamePair_When_Handling_Then_ReportsAgain()
    {
        var old = MemberReport.Create(_reporter.Id, _reported.Id, "First", null, Now.AddHours(-1)).Value;
        old.Dismiss(AdminId, Now.AddMinutes(-30));
        _reports.Add(old);

        var result = await ReportAsync(_reporter.Id, _reported.Id, "Second");

        result.IsSuccess.Should().BeTrue();
        _reports.Items.Should().HaveCount(2);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_AnotherReporterHasPendingReport_When_Handling_Then_Succeeds()
    {
        _reports.Add(MemberReport.Create(_third.Id, _reported.Id, "First", null, Now.AddHours(-1)).Value);

        var result = await ReportAsync(_reporter.Id, _reported.Id, "Second");

        result.IsSuccess.Should().BeTrue();
        _reports.Items.Should().HaveCount(2);
    }
}
