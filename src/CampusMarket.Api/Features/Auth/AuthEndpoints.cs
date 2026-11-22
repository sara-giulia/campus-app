using CampusMarket.Api.Features.Auth.ForgotPassword;
using CampusMarket.Api.Features.Auth.Login;
using CampusMarket.Api.Features.Auth.Logout;
using CampusMarket.Api.Features.Auth.Refresh;
using CampusMarket.Api.Features.Auth.Register;
using CampusMarket.Api.Features.Auth.ResetPassword;

namespace CampusMarket.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");   // => /api/v1/auth

        RegisterEndpoint.Map(group);
        LoginEndpoint.Map(group);
        RefreshEndpoint.Map(group);
        LogoutEndpoint.Map(group);
        ForgotPasswordEndpoint.Map(group);
        ResetPasswordEndpoint.Map(group);

        return app;
    }
}