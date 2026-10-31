using System.Reflection;

namespace CampusMarket.ArchitectureTests;

public abstract class ArchitectureTestBase
{
    protected static readonly Assembly ApiAssembly =
        typeof(CampusMarket.Api.Domain.Common.IUnitOfWork).Assembly;
}
