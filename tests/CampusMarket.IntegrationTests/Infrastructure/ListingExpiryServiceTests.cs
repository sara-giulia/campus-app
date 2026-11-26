using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Listings.ExpireListings;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CampusMarket.IntegrationTests.Infrastructure;

public sealed class ListingExpiryServiceTests : IntegrationTestBase
{
    private const string ServiceCategory = "CampusMarket.Api.Infrastructure.Background.ListingExpiryService";

    private readonly CapturingLoggerProvider _logs = new();

    // Starts a copy of the API with the expiry job on, the fake handler and the capturing logger.
    private WebApplicationFactory<Program> WithExpiryJob(FakeExpireListingsHandler handler) =>
        Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ListingExpiry:Enabled", "true");
            builder.UseSetting("ListingExpiry:Interval", "00:00:00.100");
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICommandHandler<ExpireListingsCommand, int>>();
                services.AddSingleton<ICommandHandler<ExpireListingsCommand, int>>(handler);
            });
        });

    private static async Task<bool> WaitForCallsAsync(FakeExpireListingsHandler handler, int calls, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (handler.Calls >= calls)
                return true;
            await Task.Delay(50);
        }
        return handler.Calls >= calls;
    }

    private List<CapturingLoggerProvider.Entry> ServiceErrors() =>
        _logs.Entries.Where(e => e.Category == ServiceCategory && e.Level == LogLevel.Error).ToList();

    private async Task DatabaseShouldBeEmptyAsync()
    {
        var listings = await QueryDbAsync(db => db.Set<Listing>().CountAsync());
        listings.Should().Be(0);
        var reservations = await QueryDbAsync(db => db.Set<Reservation>().CountAsync());
        reservations.Should().Be(0);
    }

    [Fact]
    public async Task Given_HandlerReturnsFailure_When_ServiceRuns_Then_LogsTheErrorAndKeepsRunning()
    {
        // Arrange
        var handler = new FakeExpireListingsHandler(FakeExpireListingsHandler.Behaviour.ReturnFailure);
        await using var app = WithExpiryJob(handler);

        // Act
        app.CreateClient();
        var calledTwice = await WaitForCallsAsync(handler, 2, TimeSpan.FromSeconds(5));

        // Assert
        calledTwice.Should().BeTrue("the service must keep running after a failed run and call the handler again within 5 seconds");
        ServiceErrors().Should().Contain(e =>
            e.Message.Contains("Test.Failure") && e.Message.Contains("Run failed on purpose."));

        await DatabaseShouldBeEmptyAsync();
    }

    [Fact]
    public async Task Given_HandlerThrows_When_ServiceRuns_Then_LogsTheExceptionAndKeepsRunning()
    {
        // Arrange
        var handler = new FakeExpireListingsHandler(FakeExpireListingsHandler.Behaviour.Throw);
        await using var app = WithExpiryJob(handler);

        // Act
        app.CreateClient();
        var calledTwice = await WaitForCallsAsync(handler, 2, TimeSpan.FromSeconds(5));

        // Assert
        calledTwice.Should().BeTrue("the service must keep running after an exception and call the handler again within 5 seconds");
        ServiceErrors().Should().Contain(e =>
            e.Exception is InvalidOperationException && e.Message == "ListingExpiryService run failed.");

        await DatabaseShouldBeEmptyAsync();
    }

    [Fact]
    public async Task Given_HandlerWaitsUntilCancelled_When_AppStops_Then_ServiceEndsWithoutError()
    {
        // Arrange
        var handler = new FakeExpireListingsHandler(FakeExpireListingsHandler.Behaviour.WaitUntilCancelled);
        var app = WithExpiryJob(handler);
        app.CreateClient();
        var waitForFirstCall = () => handler.FirstCall.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await waitForFirstCall.Should().NotThrowAsync("the service must call the handler within 5 seconds");

        // Act
        var stop = async () => await app.DisposeAsync();

        // Assert
        await stop.Should().NotThrowAsync();
        handler.Calls.Should().Be(1);
        ServiceErrors().Should().BeEmpty();

        await DatabaseShouldBeEmptyAsync();
    }

    [Fact]
    public async Task Given_HandlerExpiresNothing_When_ServiceRuns_Then_KeepsRunningWithoutError()
    {
        // Arrange
        var handler = new FakeExpireListingsHandler(FakeExpireListingsHandler.Behaviour.ReturnZero);
        await using var app = WithExpiryJob(handler);

        // Act
        app.CreateClient();
        var calledTwice = await WaitForCallsAsync(handler, 2, TimeSpan.FromSeconds(5));

        // Assert
        calledTwice.Should().BeTrue("the service must call the handler on every tick");
        ServiceErrors().Should().BeEmpty();

        await DatabaseShouldBeEmptyAsync();
    }
}
