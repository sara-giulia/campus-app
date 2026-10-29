using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

