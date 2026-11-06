namespace CampusMarket.IntegrationTests.Infrastructure;

// The ProblemDetails fields the tests read. Title and type are written by ASP.NET, so they are never asserted.
public sealed record ProblemResponse(int Status, string Title, string Instance, string Code, string TraceId);
