using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Daily sweep: for every lease with a past-grace outstanding balance,
/// email the tenants a reminder. Dedupe is based on EmailLogs — if a
/// LateRentReminder was already sent to this lease in the last 7 days
/// we skip, so this is safe to run more than once a day.
///
/// Template: NoticeTemplateScope.SystemReminder / slug
/// "late-rent-reminder" (see NoticeTemplateSeeder.Slugs).
/// </summary>
public class LateRentReminderHandler : IScheduledTaskHandler
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly NoticeTemplateService _tpl;
    private readonly ILogger<LateRentReminderHandler> _logger;

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromDays(7);

    public LateRentReminderHandler(
        ApplicationDbContext db,
        MailService mail,
        NoticeTemplateService tpl,
        ILogger<LateRentReminderHandler> logger)
    {
        _db = db;
        _mail = mail;
        _tpl = tpl;
        _logger = logger;
    }

    public ScheduledTaskKind Kind => ScheduledTaskKind.LateRentReminder;

    public async Task HandleAsync(ScheduledTask task, CancellationToken ct)
    {
        try
        {
            if (!_mail.IsConfigured)
            {
                _logger.LogInformation("Late rent reminder: SMTP not configured — skipping.");
                return;
            }

            var template = await _tpl.GetBySlugAsync(
                NoticeTemplateScope.SystemReminder,
                NoticeTemplateSeeder.Slugs.LateRentReminderEmail, ct);
            if (template is null)
            {
                _logger.LogWarning("Late rent reminder: template missing — skipping.");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Pull leases with unpaid rent/late-fee charges past their
            // grace window. We filter in memory for `Balance > 0`
            // because Balance isn't a mapped column (it's computed from
            // the Allocations collection).
            var leases = await _db.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenants)
                .Include(l => l.Charges).ThenInclude(c => c.Allocations)
                .Where(l => l.Charges.Any(c => c.DueDate < today))
                .ToListAsync(ct);

            var sentTotal = 0;
            var skipped = 0;
            foreach (var lease in leases)
            {
                var outstanding = lease.Charges
                    .Where(c => c.DueDate <= today.AddDays(-lease.LateFeeGraceDays))
                    .Sum(c => c.Balance);
                if (outstanding <= 0) { skipped++; continue; }

                var recipients = lease.Tenants
                    .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                    .Select(t => t.Email!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (recipients.Count == 0) { skipped++; continue; }

                if (await WasRecentlyRemindedAsync(lease.Id, ct)) { skipped++; continue; }

                var rendered = _tpl.Render(template, new TemplateContext(
                    Lease: lease, OutstandingAmount: outstanding));

                var baseUrl = ""; // Scheduler has no HTTP context; the composer tolerates an empty baseUrl (logo links break but body renders).
                foreach (var addr in recipients)
                {
                    var log = await _mail.SendReminderAsync(baseUrl,
                        EmailKind.LateRentReminder, addr,
                        rendered.TitleOrSubject, rendered.Body,
                        leaseId: lease.Id, ct: ct);
                    if (log.Status == EmailStatus.Sent) sentTotal++;
                }
            }

            task.Description = $"Late rent: {sentTotal} email(s) sent across {leases.Count} candidate lease(s); {skipped} skipped.";
            _logger.LogInformation("{Desc}", task.Description);
        }
        finally
        {
            // Re-arm for tomorrow even if we hit a soft skip above —
            // the next day may have fresh candidates. If an exception
            // escaped, the dispatcher's retry/backoff path keeps us
            // from needing a next-run row until it exhausts attempts.
            RecurringTaskHelpers.EnqueueNextRun(_db, Kind,
                "Daily late-rent reminder sweep");
        }
    }

    private async Task<bool> WasRecentlyRemindedAsync(int leaseId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - DedupeWindow;
        return await _db.EmailLogs.AnyAsync(l =>
            l.LeaseId == leaseId
            && l.Kind == EmailKind.LateRentReminder
            && l.Status == EmailStatus.Sent
            && l.CreatedUtc > cutoff, ct);
    }
}
