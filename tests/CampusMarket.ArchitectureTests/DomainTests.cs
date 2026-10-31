using NetArchTest.Rules;

namespace CampusMarket.ArchitectureTests;

public class DomainTests : ArchitectureTestBase
{
    private const string DomainNamespace = "CampusMarket.Api.Domain";

    [Fact]
    public void Given_DomainTypes_When_CheckingDependencies_Then_DoNotDependOnEntityFrameworkCore()
    {
        var domainTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace(DomainNamespace);

        domainTypes.GetTypes().Should().NotBeEmpty(
            "the Domain namespace must contain at least one type for this rule to be meaningful");

        var result = domainTypes
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Domain types depend on Microsoft.EntityFrameworkCore: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Given_DomainTypes_When_CheckingDependencies_Then_DoNotDependOnAspNetCore()
    {
        var domainTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace(DomainNamespace);

        domainTypes.GetTypes().Should().NotBeEmpty(
            "the Domain namespace must contain at least one type for this rule to be meaningful");

        var result = domainTypes
            .Should().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Domain types depend on Microsoft.AspNetCore: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Given_DomainTypes_When_CheckingDependencies_Then_DoNotDependOnInfrastructureOrFeatures()
    {
        var domainTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace(DomainNamespace);

        domainTypes.GetTypes().Should().NotBeEmpty(
            "the Domain namespace must contain at least one type for this rule to be meaningful");

        var result = domainTypes
            .Should().NotHaveDependencyOnAny(
                "CampusMarket.Api.Infrastructure",
                "CampusMarket.Api.Features")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Domain types depend on Infrastructure or Features: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
