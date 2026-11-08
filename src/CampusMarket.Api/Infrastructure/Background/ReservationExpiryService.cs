using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings.ExpireReservations;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

public sealed partial class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationExpiryOptions> options,
    TimeProvider timeProvider,
    ILogger<ReservationExpiryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;
        using var timer = new PeriodicTimer(interval, timeProvider);

        LogStarted(logger, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                // Catch so one failed run does not stop the service.
                LogRunException(logger, ex);
            }

            try
            {
                // It returns false only after the timer is disposed, which happens when this method
                // ends; the loop stops through the cancellation below.
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        // The handler is Scoped (depends on the DbContext); each tick gets its own scope.
        using var scope = scopeFactory.CreateScope();
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<ExpireReservationsCommand, int>>();

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), ct);

        if (result.IsFailure)
        {
            LogRunFailed(logger, result.Error.Code, result.Error.Message);
            return;
        }

        var expired = result.Value;
        if (expired > 0)
            LogExpired(logger, expired);
    }

    // Source-generated log methods: the arguments are only formatted when the log level is enabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "ReservationExpiryService started; running every {Interval}.")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Error, Message = "ReservationExpiryService run failed.")]
    private static partial void LogRunException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "ReservationExpiryService run failed: {Code} {Message}")]
    private static partial void LogRunFailed(ILogger logger, string code, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "ReservationExpiryService expired {Count} reservation(s).")]
    private static partial void LogExpired(ILogger logger, int count);
}
