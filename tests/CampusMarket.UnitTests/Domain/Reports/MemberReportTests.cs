using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.UnitTests.Domain.Reports;

public class MemberReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ReporterId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReportedId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ThirdMemberId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FourthMemberId = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static Conversation ConversationBetween(Guid buyerId, Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish(Now.AddDays(30));
        return Conversation.Start(buyerId, listing, "Mai e disponibil?", Now).Value;
    }

    private static MemberReport Pending() =>
        MemberReport.Create(ReporterId, ReportedId, "Rude messages", null, Now).Value;

    [Fact]
    public void Given_ValidDataWithoutConversation_When_Creating_Then_ReturnsPendingReportWithEveryField()
    {
        var result = MemberReport.Create(ReporterId, ReportedId, "  Rude messages  ", null, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ReporterId.Should().Be(ReporterId);
        result.Value.ReportedMemberId.Should().Be(ReportedId);
        result.Value.Reason.Should().Be("Rude messages");
        result.Value.ConversationId.Should().BeNull();
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.Resolution.Should().Be(ReportResolution.Pending);
        result.Value.IsPending.Should().BeTrue();
        result.Value.ResolvedAt.Should().BeNull();
        result.Value.ResolvedById.Should().BeNull();
    }

    [Fact]
    public void Given_ConversationWhereReporterIsBuyer_When_Creating_Then_StoresConversationId()
    {
        var conversation = ConversationBetween(ReporterId, ReportedId);

        var result = MemberReport.Create(ReporterId, ReportedId, "Rude messages", conversation, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConversationId.Should().Be(conversation.Id);
    }

    [Fact]
    public void Given_ConversationWhereReporterIsSeller_When_Creating_Then_StoresConversationId()
    {
        var conversation = ConversationBetween(ReportedId, ReporterId);

        var result = MemberReport.Create(ReporterId, ReportedId, "Rude messages", conversation, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConversationId.Should().Be(conversation.Id);
    }

    [Fact]
    public void Given_TwoCreates_When_Creating_Then_IdsAreDifferent()
    {
        var first = Pending();

        var second = Pending();

        second.Id.Should().NotBe(first.Id);
    }

    [Fact]
    public void Given_EmptyReporter_When_Creating_Then_ReturnsReporterEmpty()
    {
        var result = MemberReport.Create(Guid.Empty, ReportedId, "Rude messages", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ReporterEmpty");
        result.Error.Message.Should().Be("Reporter id must not be empty.");
    }

    [Fact]
    public void Given_EmptyReporterAndEmptyReported_When_Creating_Then_ReturnsReporterEmptyBeforeSelf()
    {
        var result = MemberReport.Create(Guid.Empty, Guid.Empty, "Rude messages", null, Now);

        result.Error.Code.Should().Be("MemberReport.ReporterEmpty");
    }

    [Fact]
    public void Given_ReporterIsReported_When_Creating_Then_ReturnsSelf()
    {
        var result = MemberReport.Create(ReporterId, ReporterId, "Rude messages", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.Self");
        result.Error.Message.Should().Be("You cannot report yourself.");
    }

    [Fact]
    public void Given_SelfReportWithEmptyReason_When_Creating_Then_ReturnsSelfFirst()
    {
        var result = MemberReport.Create(ReporterId, ReporterId, "", null, Now);

        result.Error.Code.Should().Be("MemberReport.Self");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyReason_When_Creating_Then_ReturnsReasonEmpty(string? reason)
    {
        var result = MemberReport.Create(ReporterId, ReportedId, reason, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
    }

    [Fact]
    public void Given_EmptyReasonAndForeignConversation_When_Creating_Then_ReturnsReasonEmptyFirst()
    {
        var conversation = ConversationBetween(ThirdMemberId, FourthMemberId);

        var result = MemberReport.Create(ReporterId, ReportedId, " ", conversation, Now);

        result.Error.Code.Should().Be("MemberReport.ReasonEmpty");
    }

    [Fact]
    public void Given_ReasonOfMaxLength_When_Creating_Then_Succeeds()
    {
        var result = MemberReport.Create(ReporterId, ReportedId, new string('a', Report.MaxReasonLength), null, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().HaveLength(Report.MaxReasonLength);
    }

    [Fact]
    public void Given_ReasonOfMaxLengthWithSpacesAround_When_Creating_Then_SucceedsTrimmed()
    {
        var reason = "  " + new string('a', Report.MaxReasonLength) + "  ";

        var result = MemberReport.Create(ReporterId, ReportedId, reason, null, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be(new string('a', Report.MaxReasonLength));
    }

    [Fact]
    public void Given_ReasonLongerThanMax_When_Creating_Then_ReturnsReasonTooLong()
    {
        var result = MemberReport.Create(ReporterId, ReportedId, new string('a', Report.MaxReasonLength + 1), null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ReasonTooLong");
        result.Error.Message.Should().Be($"Reason must have at most {Report.MaxReasonLength} characters.");
    }

    [Fact]
    public void Given_ConversationOfReporterWithThirdMember_When_Creating_Then_ReturnsConversationInvalid()
    {
        var conversation = ConversationBetween(ReporterId, ThirdMemberId);

        var result = MemberReport.Create(ReporterId, ReportedId, "Rude messages", conversation, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
        result.Error.Message.Should().Be("The conversation must be between you and the reported member.");
    }

    [Fact]
    public void Given_ConversationOfReportedWithThirdMember_When_Creating_Then_ReturnsConversationInvalid()
    {
        var conversation = ConversationBetween(ThirdMemberId, ReportedId);

        var result = MemberReport.Create(ReporterId, ReportedId, "Rude messages", conversation, Now);

        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
    }

    [Fact]
    public void Given_ConversationOfTwoOtherMembers_When_Creating_Then_ReturnsConversationInvalid()
    {
        var conversation = ConversationBetween(ThirdMemberId, FourthMemberId);

        var result = MemberReport.Create(ReporterId, ReportedId, "Rude messages", conversation, Now);

        result.Error.Code.Should().Be("MemberReport.ConversationInvalid");
    }

    [Fact]
    public void Given_ConversationInvalidError_When_Reading_Then_HasCodeTypeAndMessage()
    {
        var error = MemberReport.ConversationInvalidError();

        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("MemberReport.ConversationInvalid");
        error.Message.Should().Be("The conversation must be between you and the reported member.");
    }

    [Fact]
    public void Given_PendingReport_When_Confirming_Then_IsConfirmedWithTimeAndAdmin()
    {
        var report = Pending();

        var result = report.Confirm(AdminId, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(report);
        report.Resolution.Should().Be(ReportResolution.Confirmed);
        report.ResolvedAt.Should().Be(Now.AddHours(1));
        report.ResolvedById.Should().Be(AdminId);
        report.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Given_PendingReport_When_Dismissing_Then_IsDismissedWithTimeAndAdmin()
    {
        var report = Pending();

        var result = report.Dismiss(AdminId, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(report);
        report.Resolution.Should().Be(ReportResolution.Dismissed);
        report.ResolvedAt.Should().Be(Now.AddHours(1));
        report.ResolvedById.Should().Be(AdminId);
        report.IsPending.Should().BeFalse();
    }

    [Fact]
    public void Given_ConfirmedReport_When_Dismissing_Then_ReturnsAlreadyResolvedAndKeepsConfirmation()
    {
        var report = Pending();
        report.Confirm(AdminId, Now.AddHours(1));

        var result = report.Dismiss(Guid.NewGuid(), Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.AlreadyResolved");
        result.Error.Message.Should().Be("The report has already been resolved.");
        report.Resolution.Should().Be(ReportResolution.Confirmed);
        report.ResolvedAt.Should().Be(Now.AddHours(1));
        report.ResolvedById.Should().Be(AdminId);
    }

    [Fact]
    public void Given_DismissedReport_When_Confirming_Then_ReturnsAlreadyResolvedAndKeepsDismissal()
    {
        var report = Pending();
        report.Dismiss(AdminId, Now.AddHours(1));

        var result = report.Confirm(Guid.NewGuid(), Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Report.AlreadyResolved");
        result.Error.Message.Should().Be("The report has already been resolved.");
        report.Resolution.Should().Be(ReportResolution.Dismissed);
        report.ResolvedAt.Should().Be(Now.AddHours(1));
        report.ResolvedById.Should().Be(AdminId);
    }

    [Fact]
    public void Given_ConfirmedReport_When_ConfirmingAgain_Then_ReturnsAlreadyResolved()
    {
        var report = Pending();
        report.Confirm(AdminId, Now.AddHours(1));

        var result = report.Confirm(AdminId, Now.AddHours(2));

        result.Error.Code.Should().Be("Report.AlreadyResolved");
        report.ResolvedAt.Should().Be(Now.AddHours(1));
    }
}
