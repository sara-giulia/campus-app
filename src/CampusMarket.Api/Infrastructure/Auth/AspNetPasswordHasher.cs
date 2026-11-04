using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Identity;

namespace CampusMarket.Api.Infrastructure.Auth;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    // ASP.NET's hasher asks for a user object but does not use it.
    private static readonly object NoUser = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) =>
        _hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(NoUser, passwordHash, password)
            is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
}

