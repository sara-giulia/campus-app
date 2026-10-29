using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hashed:";

    public string Hash(string password) => $"{Prefix}{password}";

    public bool Verify(string passwordHash, string password) =>
        passwordHash == $"{Prefix}{password}";
}
