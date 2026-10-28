using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.Api.Features.Auth.Login;

namespace CampusMarket.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");   // => /api/v1/auth

        RegisterEndpoint.Map(group);
        LoginEndpoint.Map(group);

        return app;
    }
}
