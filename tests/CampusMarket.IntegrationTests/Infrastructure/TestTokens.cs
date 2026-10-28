using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Creates JWTs for the tests, signed with the same key the test API uses.
public static class TestTokens
{
    public const string Key = "campusmarket-integration-tests-key-0123456789";

    public static string Create(Guid memberId, string role = "Student") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "CampusMarket",
            Audience = "CampusMarket",
            Expires = DateTime.UtcNow.AddMinutes(30),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", memberId.ToString()),
                new Claim("email", $"{memberId:N}@test.ro"),
                new Claim("role", role)
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                SecurityAlgorithms.HmacSha256)
        });
}
