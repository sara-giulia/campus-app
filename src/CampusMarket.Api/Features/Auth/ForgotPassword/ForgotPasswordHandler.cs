using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Features.Auth.ForgotPassword;

// Always "succeeds" with the same response so the endpoint cannot be used to
// enumerate registered emails. When the member exists a reset token is saved
// and a link is emailed; when it does not, we just return the same body.
public sealed class ForgotPasswordHandler(
    IMemberRepository members,
    IPasswordResetTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    TimeProvider timeProvider)
    : ICommandHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    public async Task<Result<ForgotPasswordResponse>> HandleAsync(
        ForgotPasswordCommand command, CancellationToken ct)
    {
        var emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
            return ForgotPasswordResponse.Default;

        var member = (await members.GetAllAsync(ct)).FirstOrDefault(m => m.Email == emailResult.Value);
        if (member is null)
            return ForgotPasswordResponse.Default;

        var now = timeProvider.GetUtcNow();
        var (token, plain) = PasswordResetToken.Create(member.Id, now);
        tokens.Add(token);
        await unitOfWork.CommitAsync(ct);

        var link = $"{frontendOptions.Value.BaseUrl.TrimEnd('/')}/reset-password?token={plain}";
        var body =
            $"Hello {member.Name},{Environment.NewLine}{Environment.NewLine}" +
            $"You asked to reset your CampusMarket password. Open the link below to set a new one (valid for 30 minutes):{Environment.NewLine}" +
            $"{link}{Environment.NewLine}{Environment.NewLine}" +
            "If you did not ask for this, ignore this email.";
        await emailSender.SendAsync(member.Email.Value, "Reset your CampusMarket password", body, ct);

        return ForgotPasswordResponse.Default;
    }
}
