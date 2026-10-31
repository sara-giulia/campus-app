using System.Security.Claims;

namespace CampusMarket.Api.Common.Auth;

public static class CurrentUserExtensions
{
    // Reads the member's Id from the "sub" claim. Only call it on endpoints that need login.
    public static Guid GetMemberId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirst("sub")!.Value);

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.HasClaim("role", "Admin");
}