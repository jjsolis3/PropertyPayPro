using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Resolves the <see cref="IScheduledTaskHandler"/> for a given task
/// kind and runs it inside a transaction. Lives inside the per-task
/// scope the runner creates; the runner itself is a singleton
/// (BackgroundService) and must not touch the DbContext directly.
/// </summary>
public class ScheduledTaskDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly ILogger<ScheduledTaskDispatcher> _logger;
    private readonly IAuditLog _audit;

    public ScheduledTaskDispatcher(
        ApplicationDbContext db,
        IServiceProvider sp,
        ILogger<ScheduledTaskDispatcher> logger,
        IAuditLog audit)
    {
        _db = db;
        _sp = sp;
        _logger = logger;
        _audit = audit;
    }

    /// <summary>
    /// Grab the next due Pending task (if any), mark it Running, run
    /// the handler, then record success/failure. Returns true if a
    /// task was processed — the runner uses that to decide whether
    /// to loop immediately or sleep.
    /// </summary>
    public async Task<bool> TryRunOneAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Pick the next candidate. One SELECT, no lock; we claim it
        // in the following ExecuteUpdate, which only succeeds if the
        // row is still Pending — if another pod grabbed it first,
        // we see 0 rows and move on.
        var candidate = await _db.ScheduledTasks
            .Where(t => t.Status == ScheduledTaskStatus.Pending && t.RunAtUtc <= now)
            .OrderBy(t => t.RunAtUtc)
            .FirstOrDefaultAsync(ct);
        if (candidate is null) return false;

        var claimed = await _db.ScheduledTasks
            .Where(t => t.Id == candidate.Id && t.Status == ScheduledTaskStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, ScheduledTaskStatus.Running)
                .SetProperty(t => t.StartedUtc, now)
                .SetProperty(t => t.Attempts, t => t.Attempts + 1), ct);
        if (claimed == 0) return true; // Someone else got it; loop and try again.

        // Reload with the claim applied.
        _db.ChangeTracker.Clear();
        var task = await _db.ScheduledTasks.FirstAsync(t => t.Id == candidate.Id, ct);

        var handler = _sp.GetServices<IScheduledTaskHandler>()
            .FirstOrDefault(h => h.Kind == task.Kind);
        if (handler is null)
        {
            await FailAsync(task, $"No handler registered for kind {task.Kind}", retry: false, ct);
            return true;
        }

        try
        {
            await handler.HandleAsync(task, ct);
            task.Status = ScheduledTaskStatus.Completed;
            task.CompletedUtc = DateTime.UtcNow;
            task.LastError = null;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("ScheduledTaskCompleted",
                entityType: nameof(ScheduledTask), entityId: task.Id,
                details: $"{task.Kind}: {task.Description}",
                overrideActor: "scheduler", ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduledTask {Id} ({Kind}) failed", task.Id, task.Kind);
            await FailAsync(task, ex.Message, retry: true, ct);
        }
        return true;
    }

    private async Task FailAsync(ScheduledTask task, string message, bool retry, CancellationToken ct)
    {
        task.LastError = message.Length <= 4000 ? message : message[..4000];
        task.CompletedUtc = DateTime.UtcNow;

        if (retry && task.Attempts < task.MaxAttempts)
        {
            // Exponential backoff: 2^attempts minutes, cap 30 min.
            var delayMin = Math.Min(30, (int)Math.Pow(2, task.Attempts));
            task.Status = ScheduledTaskStatus.Pending;
            task.StartedUtc = null;
            task.CompletedUtc = null;
            task.RunAtUtc = DateTime.UtcNow.AddMinutes(delayMin);
        }
        else
        {
            task.Status = ScheduledTaskStatus.Failed;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("ScheduledTaskFailed",
            entityType: nameof(ScheduledTask), entityId: task.Id,
            details: $"{task.Kind}: {message}",
            overrideActor: "scheduler", ct: ct);
    }
}
