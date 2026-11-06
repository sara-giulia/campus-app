using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand<Guid>;