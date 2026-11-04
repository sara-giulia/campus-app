using NetArchTest.Rules;

namespace CampusMarket.ArchitectureTests;

public class MlTests : ArchitectureTestBase
{
    [Fact]
    public void Given_DomainTypes_When_CheckingDependencies_Then_DoNotDependOnMicrosoftML()
    {
        var domainTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("CampusMarket.Api.Domain");

        domainTypes.GetTypes().Should().NotBeEmpty(
            "the Domain namespace must contain at least one type for this rule to be meaningful");

        var result = domainTypes
            .Should().NotHaveDependencyOn("Microsoft.ML")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Domain types depend on Microsoft.ML: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Given_FeatureTypes_When_CheckingDependencies_Then_DoNotDependOnMicrosoftML()
    {
        var featureTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("CampusMarket.Api.Features");

        featureTypes.GetTypes().Should().NotBeEmpty(
            "the Features namespace must contain at least one type for this rule to be meaningful");

        var result = featureTypes
            .Should().NotHaveDependencyOn("Microsoft.ML")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Features types depend on Microsoft.ML: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
