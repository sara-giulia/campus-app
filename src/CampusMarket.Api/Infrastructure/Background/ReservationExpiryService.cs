using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings.ExpireReservations;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

public sealed class ReservationExpiryService(
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

        logger.LogInformation(
            "ReservationExpiryService started; running every {Interval}.", interval);

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
                logger.LogError(ex, "ReservationExpiryService run failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
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
            var error = result.Error;
            logger.LogError(
                "ReservationExpiryService run failed: {Code} {Message}",
                error.Code, error.Message);
            return;
        }

        var expired = result.Value;
        if (expired > 0)
            logger.LogInformation(
                "ReservationExpiryService expired {Count} reservation(s).", expired);
    }
}
