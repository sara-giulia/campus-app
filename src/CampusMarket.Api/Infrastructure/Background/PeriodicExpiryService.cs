namespace CampusMarket.Api.Infrastructure.Background;

// The loop shared by the expiry services: one run at start, then one on every tick of a
// PeriodicTimer driven by the TimeProvider; a failed run is logged and the next tick tries again.
// A subclass gives the interval and what one run does. Whether the service is registered at all
// (the Enabled option) is decided in CampusMarketSetup.
public abstract partial class PeriodicExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger logger)
    : BackgroundService
{
    protected ILogger Logger { get; } = logger;

    protected abstract string ServiceName { get; }

    protected abstract TimeSpan Interval { get; }

    // The handlers are Scoped (they depend on the DbContext); each tick gets its own scope.
    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = Interval;
        using var timer = new PeriodicTimer(interval, timeProvider);

        LogStarted(Logger, ServiceName, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
                break;
            }
            catch (Exception ex)
            {
                // Catch so one failed run does not stop the service.
                LogRunException(Logger, ex, ServiceName);
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

    // Source-generated log methods: the arguments are only formatted when the log level is enabled.
    [LoggerMessage(Level = LogLevel.Information, Message = "{Service} started; running every {Interval}.")]
    private static partial void LogStarted(ILogger logger, string service, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Service} run failed.")]
    private static partial void LogRunException(ILogger logger, Exception exception, string service);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Service} run failed: {Code} {Message}")]
    protected static partial void LogRunFailed(ILogger logger, string service, string code, string message);
}
