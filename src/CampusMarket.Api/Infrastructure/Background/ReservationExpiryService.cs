using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Features.Listings.ExpireReservations;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Background;

public sealed partial class ReservationExpiryService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationExpiryOptions> options,
    TimeProvider timeProvider,
    ILogger<ReservationExpiryService> logger)
    : PeriodicExpiryService(scopeFactory, timeProvider, logger)
{
    protected override string ServiceName => nameof(ReservationExpiryService);

    protected override TimeSpan Interval => options.Value.Interval;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var handler = services.GetRequiredService<ICommandHandler<ExpireReservationsCommand, int>>();

        var result = await handler.HandleAsync(new ExpireReservationsCommand(), ct);

        if (result.IsFailure)
        {
            LogRunFailed(Logger, ServiceName, result.Error.Code, result.Error.Message);
            return;
        }

        var expired = result.Value;
        if (expired > 0)
            LogExpired(Logger, expired);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ReservationExpiryService expired {Count} reservation(s).")]
    private static partial void LogExpired(ILogger logger, int count);
}
