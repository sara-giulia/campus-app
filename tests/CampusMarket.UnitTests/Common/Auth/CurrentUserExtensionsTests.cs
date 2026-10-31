using System.Security.Claims;
using CampusMarket.Api.Common.Auth;

namespace CampusMarket.UnitTests.Common.Auth;

public class CurrentUserExtensionsTests
{
    [Fact]
    public void Given_SubClaim_When_GettingMemberId_Then_ReturnsThatGuid()
    {
        var memberId = new Guid("11111111-1111-1111-1111-111111111111");
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", memberId.ToString())], "test"));

        var result = user.GetMemberId();

        result.Should().Be(memberId);
    }

    [Fact]
    public void Given_AdminRoleClaim_When_CheckingIsAdmin_Then_ReturnsTrue()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "Admin")], "test"));

        var result = user.IsAdmin();

        result.Should().BeTrue();
    }

    [Fact]
    public void Given_StudentRoleClaim_When_CheckingIsAdmin_Then_ReturnsFalse()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "Student")], "test"));

        var result = user.IsAdmin();

        result.Should().BeFalse();
    }

    [Fact]
    public void Given_NoRoleClaim_When_CheckingIsAdmin_Then_ReturnsFalse()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "11111111-1111-1111-1111-111111111111")], "test"));

        var result = user.IsAdmin();

        result.Should().BeFalse();
    }
}
