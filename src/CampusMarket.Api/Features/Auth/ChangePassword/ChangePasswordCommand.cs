using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid MemberId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : ICommand<Guid>;