namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Polls the ScheduledTasks table once a minute and asks the
/// dispatcher to run whatever is due. Keep this class dumb — the real
/// work lives in <see cref="ScheduledTaskDispatcher"/>, which is a
/// scoped service so EF, audit, and handlers resolve correctly.
/// </summary>
public class ScheduledTaskRunner : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ScheduledTaskRunner> _logger;
    private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BusyPause = TimeSpan.FromMilliseconds(100);

    public ScheduledTaskRunner(IServiceProvider sp, ILogger<ScheduledTaskRunner> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One-time stagger so a fresh pod doesn't hit the DB at exactly
        // the top of the second alongside every other startup task.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var ranSomething = false;
            try
            {
                using var scope = _sp.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<ScheduledTaskDispatcher>();
                ranSomething = await dispatcher.TryRunOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                // The dispatcher itself is wrapped in try/catch for
                // handler errors; this catch only fires on DbContext
                // or scope-resolution failures.
                _logger.LogError(ex, "ScheduledTaskRunner tick failed");
            }

            try
            {
                // When we drained a task, loop again almost immediately
                // in case more are due. Otherwise idle for a full minute.
                await Task.Delay(ranSomething ? BusyPause : IdlePoll, stoppingToken);
            }
            catch (OperationCanceledException) { return; }
        }
    }
}
