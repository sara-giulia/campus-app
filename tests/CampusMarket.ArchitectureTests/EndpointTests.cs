using NetArchTest.Rules;

namespace CampusMarket.ArchitectureTests;

public class EndpointTests : ArchitectureTestBase
{
    [Fact]
    public void Given_EndpointClasses_When_CheckingDependencies_Then_DoNotDependOnInfrastructureOrEntityFrameworkCore()
    {
        var endpoints = Types.InAssembly(ApiAssembly)
            .That().HaveNameEndingWith("Endpoint")
            .And().AreClasses();

        endpoints.GetTypes().Should().NotBeEmpty(
            "the assembly must contain at least one class whose name ends with 'Endpoint'");

        var result = endpoints
            .Should().NotHaveDependencyOnAny(
                "CampusMarket.Api.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these endpoint classes depend on Infrastructure or EF Core: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Given_EndpointClasses_When_CheckingDependencies_Then_DoNotDependOnRepositoriesOrUnitOfWork()
    {
        var endpoints = Types.InAssembly(ApiAssembly)
            .That().HaveNameEndingWith("Endpoint")
            .And().AreClasses();

        endpoints.GetTypes().Should().NotBeEmpty(
            "the assembly must contain at least one class whose name ends with 'Endpoint'");

        var result = endpoints
            .Should().NotHaveDependencyOnAny(
                "CampusMarket.Api.Domain.Listings.IListingRepository",
                "CampusMarket.Api.Domain.Members.IMemberRepository",
                "CampusMarket.Api.Domain.Common.IUnitOfWork")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these endpoint classes depend on a repository or IUnitOfWork: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
