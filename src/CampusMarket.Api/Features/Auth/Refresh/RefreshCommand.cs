using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Auth.Refresh;

public sealed record RefreshCommand(string RefreshToken) : ICommand<RefreshResponse>;