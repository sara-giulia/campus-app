using NetArchTest.Rules;

namespace CampusMarket.ArchitectureTests;

public class FeatureTests : ArchitectureTestBase
{
    [Fact]
    public void Given_FeatureTypes_When_CheckingDependencies_Then_DoNotDependOnInfrastructure()
    {
        var featureTypes = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("CampusMarket.Api.Features");

        featureTypes.GetTypes().Should().NotBeEmpty(
            "the Features namespace must contain at least one type for this rule to be meaningful");

        var result = featureTypes
            .Should().NotHaveDependencyOn("CampusMarket.Api.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these Features types depend on Infrastructure: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
