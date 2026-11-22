using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword, string ConfirmNewPassword) : ICommand<Guid>;
