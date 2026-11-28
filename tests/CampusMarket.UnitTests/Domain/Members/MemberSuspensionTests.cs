using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Domain.Members;

public class MemberSuspensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static Member Student() => Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

    private static Member Admin() =>
        Member.Register("Admin One", "admin@campus.ro", "hashed:secret12", Now, MemberRole.Admin).Value;

    [Fact]
    public void Given_Student_When_SuspendingUntilADate_Then_StoresEveryFieldAndReturnsSameInstance()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "  Scam attempts  ", Now.AddDays(7), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(member);
        member.SuspendedAt.Should().Be(Now);
        member.SuspendedUntil.Should().Be(Now.AddDays(7));
        member.SuspensionReason.Should().Be("Scam attempts");
        member.SuspendedById.Should().Be(AdminId);
        member.IsSuspended(Now).Should().BeTrue();
    }

    [Fact]
    public void Given_Student_When_SuspendingWithoutEnd_Then_IsSuspendedPermanently()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", null, Now);

        result.IsSuccess.Should().BeTrue();
        member.SuspendedAt.Should().Be(Now);
        member.SuspendedUntil.Should().BeNull();
        member.SuspensionReason.Should().Be("Scam attempts");
        member.SuspendedById.Should().Be(AdminId);
        member.IsSuspended(Now.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public void Given_Member_When_SuspendingThemselves_Then_ReturnsForbiddenAndChangesNothing()
    {
        var member = Student();

        var result = member.Suspend(member.Id, "Scam attempts", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Suspension.Self");
        result.Error.Message.Should().Be("You cannot suspend yourself.");
        member.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public void Given_Admin_When_SuspendingThemselves_Then_ReturnsSelfBeforeCannotSuspendAdmin()
    {
        var admin = Admin();

        var result = admin.Suspend(admin.Id, "Scam attempts", null, Now);

        result.Error.Code.Should().Be("Suspension.Self");
        result.Error.Message.Should().Be("You cannot suspend yourself.");
    }

    [Fact]
    public void Given_Admin_When_Suspending_Then_ReturnsForbiddenAndChangesNothing()
    {
        var admin = Admin();

        var result = admin.Suspend(AdminId, "Scam attempts", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Member.CannotSuspendAdmin");
        result.Error.Message.Should().Be("An Admin cannot be suspended.");
        admin.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public void Given_Admin_When_SuspendingWithEmptyReason_Then_ReturnsCannotSuspendAdminFirst()
    {
        var admin = Admin();

        var result = admin.Suspend(AdminId, "", null, Now);

        result.Error.Code.Should().Be("Member.CannotSuspendAdmin");
    }

    [Fact]
    public void Given_SuspendedMember_When_SuspendingAgain_Then_ReturnsConflictAndKeepsFirstSuspension()
    {
        var member = Student();
        member.Suspend(AdminId, "First", Now.AddDays(7), Now);

        var result = member.Suspend(Guid.NewGuid(), "Second", null, Now.AddDays(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.AlreadySuspended");
        result.Error.Message.Should().Be("The member is already suspended. Lift the suspension first.");
        member.SuspendedAt.Should().Be(Now);
        member.SuspendedUntil.Should().Be(Now.AddDays(7));
        member.SuspensionReason.Should().Be("First");
        member.SuspendedById.Should().Be(AdminId);
    }

    [Fact]
    public void Given_SuspendedMember_When_SuspendingAgainWithEmptyReason_Then_ReturnsAlreadySuspendedFirst()
    {
        var member = Student();
        member.Suspend(AdminId, "First", null, Now);

        var result = member.Suspend(AdminId, "", null, Now);

        result.Error.Code.Should().Be("Member.AlreadySuspended");
    }

    [Fact]
    public void Given_EndedTemporarySuspension_When_SuspendingAgain_Then_Succeeds()
    {
        var member = Student();
        member.Suspend(AdminId, "First", Now.AddDays(1), Now);

        var result = member.Suspend(AdminId, "Second", null, Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        member.SuspendedAt.Should().Be(Now.AddDays(1));
        member.SuspendedUntil.Should().BeNull();
        member.SuspensionReason.Should().Be("Second");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyReason_When_Suspending_Then_ReturnsValidationAndChangesNothing(string? reason)
    {
        var member = Student();

        var result = member.Suspend(AdminId, reason, null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Suspension.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
        member.SuspendedAt.Should().BeNull();
        member.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Given_EmptyReasonAndPastEnd_When_Suspending_Then_ReturnsReasonEmptyFirst()
    {
        var member = Student();

        var result = member.Suspend(AdminId, " ", Now.AddDays(-1), Now);

        result.Error.Code.Should().Be("Suspension.ReasonEmpty");
    }

    [Fact]
    public void Given_ReasonOfMaxLength_When_Suspending_Then_Succeeds()
    {
        var member = Student();

        var result = member.Suspend(AdminId, new string('a', Member.MaxSuspensionReasonLength), null, Now);

        result.IsSuccess.Should().BeTrue();
        member.SuspensionReason.Should().HaveLength(Member.MaxSuspensionReasonLength);
    }

    [Fact]
    public void Given_ReasonOfMaxLengthWithSpacesAround_When_Suspending_Then_SucceedsWithTrimmedReason()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "  " + new string('a', Member.MaxSuspensionReasonLength) + "  ", null, Now);

        result.IsSuccess.Should().BeTrue();
        member.SuspensionReason.Should().Be(new string('a', Member.MaxSuspensionReasonLength));
    }

    [Fact]
    public void Given_ReasonLongerThanMax_When_Suspending_Then_ReturnsValidationAndChangesNothing()
    {
        var member = Student();

        var result = member.Suspend(AdminId, new string('a', Member.MaxSuspensionReasonLength + 1), null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Suspension.ReasonTooLong");
        result.Error.Message.Should().Be($"Reason must have at most {Member.MaxSuspensionReasonLength} characters.");
        member.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public void Given_EndEqualToNow_When_Suspending_Then_ReturnsUntilInPastAndChangesNothing()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", Now, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Suspension.UntilInPast");
        result.Error.Message.Should().Be("The end of the suspension must be in the future.");
        member.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public void Given_EndInThePast_When_Suspending_Then_ReturnsUntilInPast()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", Now.AddSeconds(-1), Now);

        result.Error.Code.Should().Be("Suspension.UntilInPast");
    }

    [Fact]
    public void Given_EndOneSecondAfterNow_When_Suspending_Then_Succeeds()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", Now.AddSeconds(1), Now);

        result.IsSuccess.Should().BeTrue();
        member.SuspendedUntil.Should().Be(Now.AddSeconds(1));
    }

    [Fact]
    public void Given_EndExactlyAtMaxLength_When_Suspending_Then_Succeeds()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", Now.Add(Member.MaxSuspensionLength), Now);

        result.IsSuccess.Should().BeTrue();
        member.SuspendedUntil.Should().Be(Now.Add(Member.MaxSuspensionLength));
    }

    [Fact]
    public void Given_EndOneSecondAfterMaxLength_When_Suspending_Then_ReturnsTooLongAndChangesNothing()
    {
        var member = Student();

        var result = member.Suspend(AdminId, "Scam attempts", Now.Add(Member.MaxSuspensionLength).AddSeconds(1), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Suspension.TooLong");
        result.Error.Message.Should().Be(
            "A temporary suspension lasts at most 365 days; leave the end empty for a permanent one.");
        member.SuspendedAt.Should().BeNull();
    }

    [Fact]
    public void Given_MaxSuspensionLength_When_Reading_Then_Is365Days()
    {
        var length = Member.MaxSuspensionLength;

        length.Should().Be(TimeSpan.FromDays(365));
    }

    [Fact]
    public void Given_NeverSuspendedMember_When_CheckingIsSuspended_Then_ReturnsFalse()
    {
        var member = Student();

        var suspended = member.IsSuspended(Now);

        suspended.Should().BeFalse();
    }

    [Fact]
    public void Given_TemporarySuspension_When_CheckingOneSecondBeforeEnd_Then_ReturnsTrue()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", Now.AddDays(1), Now);

        var suspended = member.IsSuspended(Now.AddDays(1).AddSeconds(-1));

        suspended.Should().BeTrue();
    }

    [Fact]
    public void Given_TemporarySuspension_When_CheckingAtEnd_Then_ReturnsFalse()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", Now.AddDays(1), Now);

        var suspended = member.IsSuspended(Now.AddDays(1));

        suspended.Should().BeFalse();
    }

    [Fact]
    public void Given_SuspendedMember_When_LiftingSuspension_Then_ClearsEveryFieldAndReturnsSameInstance()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", Now.AddDays(7), Now);

        var result = member.LiftSuspension(Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(member);
        member.SuspendedAt.Should().BeNull();
        member.SuspendedUntil.Should().BeNull();
        member.SuspensionReason.Should().BeNull();
        member.SuspendedById.Should().BeNull();
        member.IsSuspended(Now.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Given_PermanentSuspension_When_LiftingSuspension_Then_Succeeds()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", null, Now);

        var result = member.LiftSuspension(Now.AddYears(2));

        result.IsSuccess.Should().BeTrue();
        member.IsSuspended(Now.AddYears(2)).Should().BeFalse();
    }

    [Fact]
    public void Given_NeverSuspendedMember_When_LiftingSuspension_Then_ReturnsNotSuspended()
    {
        var member = Student();

        var result = member.LiftSuspension(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.NotSuspended");
        result.Error.Message.Should().Be("The member is not suspended.");
    }

    [Fact]
    public void Given_EndedTemporarySuspension_When_LiftingSuspension_Then_ReturnsNotSuspendedAndKeepsFields()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", Now.AddDays(1), Now);

        var result = member.LiftSuspension(Now.AddDays(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.NotSuspended");
        result.Error.Message.Should().Be("The member is not suspended.");
        member.SuspendedAt.Should().Be(Now);
        member.SuspendedUntil.Should().Be(Now.AddDays(1));
        member.SuspensionReason.Should().Be("Scam attempts");
        member.SuspendedById.Should().Be(AdminId);
    }

    [Fact]
    public void Given_TemporarySuspension_When_GettingAccountSuspendedError_Then_ShowsTheEndInUtc()
    {
        var member = Student();
        var until = new DateTimeOffset(2026, 1, 8, 12, 30, 0, TimeSpan.FromHours(2));
        member.Suspend(AdminId, "Scam attempts", until, Now);

        var error = member.AccountSuspendedError();

        error.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be("Auth.AccountSuspended");
        error.Message.Should().Be("This account is suspended until 2026-01-08 10:30 UTC.");
    }

    [Fact]
    public void Given_PermanentSuspension_When_GettingAccountSuspendedError_Then_SaysPermanently()
    {
        var member = Student();
        member.Suspend(AdminId, "Scam attempts", null, Now);

        var error = member.AccountSuspendedError();

        error.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be("Auth.AccountSuspended");
        error.Message.Should().Be("This account is suspended permanently.");
    }

    [Fact]
    public void Given_AccountSuspendedCode_When_Reading_Then_IsAuthAccountSuspended()
    {
        var code = Member.AccountSuspendedCode;

        code.Should().Be("Auth.AccountSuspended");
    }
}
