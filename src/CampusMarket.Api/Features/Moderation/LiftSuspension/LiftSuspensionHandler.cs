using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Moderation.LiftSuspension;

// An Admin ends a suspension before its date (or a permanent one). The member can log in again and
// their listings are back in every query (nothing was changed on them). What the suspension cancelled
// (reservations, offers) stays cancelled. The member gets an email after the commit.
public sealed partial class LiftSuspensionHandler(
    IMemberRepository members,
    IUnitOfWork unitOfWork,
    ISuspensionCheck suspensionCheck,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider,
    ILogger<LiftSuspensionHandler> logger)
    : ICommandHandler<LiftSuspensionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(LiftSuspensionCommand command, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(command.MemberId, ct);
        if (member is null)
            return Result<Guid>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{command.MemberId}' was not found."));

        var lifted = member.LiftSuspension(timeProvider.GetUtcNow());
        if (lifted.IsFailure)
            return Result<Guid>.Failure(lifted.Error);

        members.Update(member);
        await unitOfWork.CommitAsync(ct);

        suspensionCheck.Invalidate();

        var link = $"{frontendOptions.Value.BaseUrl.TrimEnd('/')}/my/listings";
        try
        {
            await emailSender.SendAsync(
                member.Email.Value, SuspensionEmails.LiftedSubject, SuspensionEmails.LiftedBody(member, link), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogEmailFailed(logger, member.Id, ex);
        }

        return member.Id;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Suspension lifted email for member {MemberId} could not be sent.")]
    private static partial void LogEmailFailed(ILogger logger, Guid memberId, Exception exception);
}
