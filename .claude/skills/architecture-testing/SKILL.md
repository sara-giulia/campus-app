---
name: architecture-testing
description: Generates the CampusMarket architecture tests with NetArchTest.Rules and xUnit, in a separate test project that checks the dependency rules between Domain, Features, Infrastructure and the CQRS handlers. Use when adding or changing architecture rules, or when the architecture test project does not exist yet.
---

# Architecture testing skill for CampusMarket

## Goal
Create the project `tests/CampusMarket.ArchitectureTests` with one test per architecture rule below. The tests load the compiled `CampusMarket.Api` assembly and check its types with NetArchTest.Rules. Unit tests check behaviour; these tests check that the structure of the code follows the rules the team agreed on.

## Project facts (read the real files, do not guess)
- API project: `src/CampusMarket.Api`, root namespace `CampusMarket.Api`.
- Folders and namespaces: `Domain/` → `CampusMarket.Api.Domain`, `Features/` → `CampusMarket.Api.Features`, `Infrastructure/` → `CampusMarket.Api.Infrastructure`, `Common/` → `CampusMarket.Api.Common`.
- CQRS interfaces are in `Common/CQRS/`: open the folder and read their exact names before writing any rule (`IQueryHandler<TQuery, TResult>`, `ICommandHandler<TCommand, TResult>` and the marker interfaces).
- Repository interfaces are in `Domain/` (`IListingRepository`, `IMemberRepository`); `IUnitOfWork` is in `Domain/Common/`. The EF Core implementations and `CampusMarketDbContext` are in `Infrastructure/`.
- Every slice has one endpoint class named `<UseCase>Endpoint` (a `static class`) and one handler named `<UseCase>Handler`.
- Query handlers implement `IQueryHandler<,>`; command handlers implement `ICommandHandler<,>`.

## Project layout
```
tests/CampusMarket.ArchitectureTests/
  CampusMarket.ArchitectureTests.csproj
  ArchitectureTestBase.cs      # the API assembly, shared by all test classes
  DomainTests.cs
  EndpointTests.cs
  HandlerTests.cs
  FeatureTests.cs
```
- Namespace: `CampusMarket.ArchitectureTests`.
- `.csproj`: copy the structure of `tests/CampusMarket.UnitTests/CampusMarket.UnitTests.csproj`: `net11.0`, `Nullable` and `ImplicitUsings` enabled, `IsPackable` false, `<Using Include="Xunit" />` and `<Using Include="FluentAssertions" />`, `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (the API is a web project, so the test project does not build without it), and a `ProjectReference` to `src/CampusMarket.Api/CampusMarket.Api.csproj`.
- Packages, with the same versions as `CampusMarket.UnitTests` where they overlap: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `FluentAssertions` 7.2.0 (never 8), and `NetArchTest.Rules` (latest 1.3.x).
- Add the project to the solution: `dotnet sln add tests/CampusMarket.ArchitectureTests`. The CI already runs `dotnet test` on the whole solution, so nothing else is needed.

`ArchitectureTestBase.cs` exposes the assembly once:
```csharp
public abstract class ArchitectureTestBase
{
    protected static readonly Assembly ApiAssembly = typeof(CampusMarket.Api.Domain.Common.IUnitOfWork).Assembly;
}
```

## Rules to test (one test each)

| # | Class | Rule |
|---|---|---|
| 1 | `DomainTests` | Types in `CampusMarket.Api.Domain` have no dependency on `Microsoft.EntityFrameworkCore`. |
| 2 | `DomainTests` | Types in `CampusMarket.Api.Domain` have no dependency on `Microsoft.AspNetCore`. |
| 3 | `DomainTests` | Types in `CampusMarket.Api.Domain` have no dependency on `CampusMarket.Api.Infrastructure` or `CampusMarket.Api.Features`. |
| 4 | `EndpointTests` | Classes whose name ends with `Endpoint` have no dependency on `CampusMarket.Api.Infrastructure` (this covers `CampusMarketDbContext` and the EF repositories) nor on `Microsoft.EntityFrameworkCore`. |
| 5 | `EndpointTests` | Classes whose name ends with `Endpoint` have no dependency on `IListingRepository`, `IMemberRepository` or `IUnitOfWork` (use their full type names). |
| 6 | `HandlerTests` | Query handlers (classes implementing `IQueryHandler<,>`) have no dependency on `IUnitOfWork`. |
| 7 | `HandlerTests` | Every handler class (implementing `IQueryHandler<,>` or `ICommandHandler<,>`) is `sealed`. |
| 8 | `FeatureTests` | Types in `CampusMarket.Api.Features` have no dependency on `CampusMarket.Api.Infrastructure`: handlers and endpoints use only the interfaces from `Domain`. This is the main architecture rule. |

How to select the types:
- Always add `.And().AreClasses()` when selecting by name, so the CQRS interfaces themselves (whose names also end with `Handler`) are never selected.
- If `ImplementInterface` does not match the open generic interfaces, select handlers by name instead (`HaveNameEndingWith("Handler")`), and query handlers as the handlers whose name starts with `Get`. Say in the report which way was used.
- Every rule must select at least one type. Add an assertion that the selection is not empty (for example `Types.InAssembly(ApiAssembly).That()...GetTypes().Should().NotBeEmpty()`), so a rule that matches nothing never passes by accident.

## Naming and structure
- Method: `Given_<types>_When_<checking>_Then_<expected>`, for example `Given_DomainTypes_When_CheckingDependencies_Then_DoNotDependOnEntityFrameworkCore`.
- Each test: build the rule, call `GetResult()`, then assert with FluentAssertions and show the failing types in the message:
```csharp
result.IsSuccessful.Should().BeTrue(
    "these types break the rule: {0}",
    string.Join(", ", result.FailingTypeNames ?? []));
```
- One rule per test. No `if`, no loops.

## When a rule fails
A failing rule means the production code breaks an agreed rule. Never change files in `src/` and never weaken or delete a rule to make it pass. Leave the test failing, and report the rule and the failing type names, so the author of that code can fix it.

## Never do
- Never edit `src/`, `tests/CampusMarket.UnitTests`, `tests/CampusMarket.IntegrationTests`, `postman/`, `.github/` or `.claude/skills/`.
- Never use FluentAssertions 8 or a mocking library.
- Never commit or push; report the results.
- Every run starts in a new Claude Code session, so that the current version of this file is read.

## Check after generating
- `dotnet build` finishes with 0 errors and 0 warnings.
- `dotnet test tests/CampusMarket.ArchitectureTests` runs 8 tests (or more, if a rule was split).
- `CampusMarket.sln` lists the new project.
- `git status` shows changes only in `tests/CampusMarket.ArchitectureTests/` and `CampusMarket.sln`.