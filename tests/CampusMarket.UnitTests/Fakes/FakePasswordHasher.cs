using CampusMarket.Api.Domain.Members;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hashed:";

    public int VerifyCount { get; private set; }

    public string Hash(string password) => $"{Prefix}{password}";

    public bool Verify(string passwordHash, string password)
    {
        VerifyCount++;
        return passwordHash == $"{Prefix}{password}";
    }
}
