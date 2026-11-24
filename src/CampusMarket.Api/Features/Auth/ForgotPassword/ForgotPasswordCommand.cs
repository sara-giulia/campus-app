using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand<ForgotPasswordResponse>;
