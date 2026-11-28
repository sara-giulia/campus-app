using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Moderation.LiftSuspension;
using CampusMarket.UnitTests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampusMarket.UnitTests.Features.Moderation.LiftSuspension;

public class LiftSuspensionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly Member _member = Member.Create("Ana Pop", "ana@campus.ro", Now.AddDays(-30)).Value;
    private readonly FakeMemberRepository _members = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeSuspensionCheck _suspensionCheck = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeLogger<LiftSuspensionHandler> _logger = new();
    private readonly LiftSuspensionHandler _handler;

    public LiftSuspensionHandlerTests()
    {
        _members.Items[_member.Id] = _member;
        _handler = new LiftSuspensionHandler(
            _members, _unitOfWork, _suspensionCheck, _emailSender,
            Options.Create(new FrontendOptions { BaseUrl = "https://campus.example/" }),
            new FixedTimeProvider(Now), _logger);
    }

    private Task<Result<Guid>> LiftAsync(Guid memberId) =>
        _handler.HandleAsync(new LiftSuspensionCommand(memberId), CancellationToken.None);

    private static string LiftedBody(string name, string link)
    {
        var nl = Environment.NewLine;
        var ro =
            $"Salut {name},{nl}{nl}" +
            "Suspendarea contului tău CampusMarket a fost ridicată. Te poți autentifica din nou, " +
            $"iar anunțurile tale sunt din nou vizibile:{nl}{link}";
        var en =
            $"Hello {name},{nl}{nl}" +
            "The suspension of your CampusMarket account was lifted. You can sign in again " +
            $"and your listings are visible again:{nl}{link}";
        return $"{ro}{nl}{nl}----------{nl}{nl}{en}{nl}";
    }

    [Fact]
    public async Task Given_SuspendedMember_When_Lifting_Then_ClearsSuspensionUpdatesAndSavesOnce()
    {
        _member.Suspend(AdminId, "Scam attempts", Now.AddDays(5), Now.AddDays(-1));

        var result = await LiftAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(_member.Id);
        _member.IsSuspended(Now).Should().BeFalse();
        _member.SuspendedAt.Should().BeNull();
        _member.SuspensionReason.Should().BeNull();
        _members.UpdateCount.Should().Be(1);
        _unitOfWork.SaveCount.Should().Be(1);
        _suspensionCheck.InvalidateCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_PermanentlySuspendedMember_When_Lifting_Then_SendsTheFullEmailAndLogsNothing()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddDays(-1));

        await LiftAsync(_member.Id);

        var email = _emailSender.Items.Should().ContainSingle().Subject;
        email.To.Should().Be("ana@campus.ro");
        email.Subject.Should().Be("Contul tău CampusMarket a fost reactivat / Your CampusMarket account was reactivated");
        email.Body.Should().Be(LiftedBody("Ana Pop", "https://campus.example/my/listings"));
        email.Body.Should().NotContain("Scam attempts");
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_EmailFails_When_Lifting_Then_SucceedsAndLogsWarning()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddDays(-1));
        _emailSender.ThrowOnSend = new InvalidOperationException("SMTP down");

        var result = await LiftAsync(_member.Id);

        result.IsSuccess.Should().BeTrue();
        _member.IsSuspended(Now).Should().BeFalse();
        _unitOfWork.SaveCount.Should().Be(1);
        _suspensionCheck.InvalidateCount.Should().Be(1);
        var entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Be($"Suspension lifted email for member {_member.Id} could not be sent.");
    }

    [Fact]
    public async Task Given_EmailCancelled_When_Lifting_Then_CancellationPropagates()
    {
        _member.Suspend(AdminId, "Scam attempts", null, Now.AddDays(-1));
        _emailSender.ThrowOnSend = new OperationCanceledException();

        var act = () => LiftAsync(_member.Id);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownMember_When_Lifting_Then_ReturnsNotFoundAndDoesNothing()
    {
        var unknownId = Guid.NewGuid();

        var result = await LiftAsync(unknownId);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Member.NotFound");
        result.Error.Message.Should().Be($"Member '{unknownId}' was not found.");
        _unitOfWork.SaveCount.Should().Be(0);
        _suspensionCheck.InvalidateCount.Should().Be(0);
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_MemberNotSuspended_When_Lifting_Then_ReturnsNotSuspendedAndDoesNothing()
    {
        var result = await LiftAsync(_member.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Member.NotSuspended");
        result.Error.Message.Should().Be("The member is not suspended.");
        _members.UpdateCount.Should().Be(0);
        _unitOfWork.SaveCount.Should().Be(0);
        _suspensionCheck.InvalidateCount.Should().Be(0);
        _emailSender.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TemporarySuspensionAlreadyOver_When_Lifting_Then_ReturnsNotSuspended()
    {
        _member.Suspend(AdminId, "Scam attempts", Now, Now.AddDays(-1));

        var result = await LiftAsync(_member.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Member.NotSuspended");
        _member.SuspendedAt.Should().Be(Now.AddDays(-1));
        _unitOfWork.SaveCount.Should().Be(0);
    }
}
