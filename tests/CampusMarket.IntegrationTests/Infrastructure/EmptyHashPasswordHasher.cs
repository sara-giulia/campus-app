using CampusMarket.Api.Domain.Members;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Returns an empty hash, so Member.Register fails with Member.PasswordRequired.
public sealed class EmptyHashPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "";

    public bool Verify(string passwordHash, string password) => false;
}
