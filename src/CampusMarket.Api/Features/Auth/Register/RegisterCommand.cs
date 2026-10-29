using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.Register;

public sealed record RegisterCommand(string Name, string Email, string Password) : ICommand<RegisterResponse>;
