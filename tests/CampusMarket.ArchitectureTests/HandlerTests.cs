using NetArchTest.Rules;

namespace CampusMarket.ArchitectureTests;

public class HandlerTests : ArchitectureTestBase
{
    [Fact]
    public void Given_QueryHandlers_When_CheckingDependencies_Then_DoNotDependOnUnitOfWork()
    {
        // Query handlers are selected by name (classes whose name starts with "Get" and ends with "Handler"),
        // because NetArchTest's ImplementInterface does not reliably match the open generic IQueryHandler<,>.
        var queryHandlers = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("Get")
            .And().HaveNameEndingWith("Handler")
            .And().AreClasses();

        queryHandlers.GetTypes().Should().NotBeEmpty(
            "the assembly must contain at least one query handler (class named Get*Handler)");

        var result = queryHandlers
            .Should().NotHaveDependencyOn("CampusMarket.Api.Domain.Common.IUnitOfWork")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these query handlers depend on IUnitOfWork: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Given_HandlerClasses_When_CheckingDeclaration_Then_AreSealed()
    {
        // Handlers are selected by name (classes whose name ends with "Handler"),
        // because NetArchTest's ImplementInterface does not reliably match
        // the open generic IQueryHandler<,> and ICommandHandler<,>.
        var handlers = Types.InAssembly(ApiAssembly)
            .That().HaveNameEndingWith("Handler")
            .And().AreClasses();

        handlers.GetTypes().Should().NotBeEmpty(
            "the assembly must contain at least one handler class (name ending with 'Handler')");

        var result = handlers
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "these handler classes are not sealed: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
