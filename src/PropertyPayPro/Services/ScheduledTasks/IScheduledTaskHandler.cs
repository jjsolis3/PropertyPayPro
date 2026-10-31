using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// A handler that knows how to execute one <see cref="ScheduledTaskKind"/>.
/// Register each handler as scoped in Program.cs so EF-backed services
/// resolve correctly inside the per-task scope the runner creates.
/// </summary>
public interface IScheduledTaskHandler
{
    ScheduledTaskKind Kind { get; }
    Task HandleAsync(ScheduledTask task, CancellationToken ct);
}
