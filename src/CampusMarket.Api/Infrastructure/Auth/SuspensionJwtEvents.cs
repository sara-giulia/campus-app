using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CampusMarket.Api.Infrastructure.Auth;

// The JWT bearer events that make a suspension work on tokens issued before it:
// - OnTokenValidated: a valid token of a suspended member fails authentication. On a public endpoint
//   the request simply goes on as anonymous; on one that needs login the challenge below answers.
// - OnChallenge: that failure gets a 401 with the Auth.AccountSuspended error as JSON (like every other
//   error of the API), so the frontend can tell it from an expired token. Other 401s are left as they were.
public static class SuspensionJwtEvents
{
    private const string FailureMessage = Member.AccountSuspendedCode;

    public static JwtBearerEvents Create() => new()
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var memberId))
                return;

            var check = context.HttpContext.RequestServices.GetRequiredService<ISuspensionCheck>();
            if (await check.IsSuspendedAsync(memberId, context.HttpContext.RequestAborted))
                context.Fail(FailureMessage);
        },
        OnChallenge = async context =>
        {
            if (context.AuthenticateFailure?.Message != FailureMessage)
                return;

            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate =
                "Bearer error=\"invalid_token\", error_description=\"The account is suspended\"";
            await context.Response.WriteAsJsonAsync(
                Error.Forbidden(Member.AccountSuspendedCode, "This account is suspended."),
                context.HttpContext.RequestAborted);
        }
    };
}
