using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Daily sweep: leases whose EndDate falls within the next 60 days
/// (and that are not already month-to-month) get a "please talk to us
/// about renewal" reminder. Dedupe window: 30 days per lease.
/// </summary>
public class LeaseRenewalReminderHandler : IScheduledTaskHandler
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly NoticeTemplateService _tpl;
    private readonly ILogger<LeaseRenewalReminderHandler> _logger;

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromDays(30);
    private const int LookaheadDays = 60;

    public LeaseRenewalReminderHandler(
        ApplicationDbContext db,
        MailService mail,
        NoticeTemplateService tpl,
        ILogger<LeaseRenewalReminderHandler> logger)
    {
        _db = db;
        _mail = mail;
        _tpl = tpl;
        _logger = logger;
    }

    public ScheduledTaskKind Kind => ScheduledTaskKind.LeaseRenewalReminder;

    public async Task HandleAsync(ScheduledTask task, CancellationToken ct)
    {
        try
        {
            if (!_mail.IsConfigured)
            {
                _logger.LogInformation("Lease renewal reminder: SMTP not configured — skipping.");
                return;
            }
            var template = await _tpl.GetBySlugAsync(
                NoticeTemplateScope.SystemReminder,
                NoticeTemplateSeeder.Slugs.LeaseRenewalReminderEmail, ct);
            if (template is null)
            {
                _logger.LogWarning("Lease renewal reminder: template missing — skipping.");
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var window = today.AddDays(LookaheadDays);

            var leases = await _db.Leases
                .Include(l => l.Property)
                .Include(l => l.Tenants)
                .Where(l => !l.IsMonthToMonth
                            && l.EndDate >= today
                            && l.EndDate <= window)
                .ToListAsync(ct);

            var sent = 0;
            var skipped = 0;
            foreach (var lease in leases)
            {
                var recipients = lease.Tenants
                    .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                    .Select(t => t.Email!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (recipients.Count == 0) { skipped++; continue; }

                if (await WasRecentlyRemindedAsync(lease.Id, ct)) { skipped++; continue; }

                var rendered = _tpl.Render(template, new TemplateContext(Lease: lease));

                foreach (var addr in recipients)
                {
                    var log = await _mail.SendReminderAsync("",
                        EmailKind.LeaseRenewalReminder, addr,
                        rendered.TitleOrSubject, rendered.Body,
                        leaseId: lease.Id, ct: ct);
                    if (log.Status == EmailStatus.Sent) sent++;
                }
            }

            task.Description = $"Lease renewal: {sent} email(s) sent across {leases.Count} expiring lease(s); {skipped} skipped.";
            _logger.LogInformation("{Desc}", task.Description);
        }
        finally
        {
            RecurringTaskHelpers.EnqueueNextRun(_db, Kind,
                "Daily lease-renewal reminder sweep");
        }
    }

    private async Task<bool> WasRecentlyRemindedAsync(int leaseId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - DedupeWindow;
        return await _db.EmailLogs.AnyAsync(l =>
            l.LeaseId == leaseId
            && l.Kind == EmailKind.LeaseRenewalReminder
            && l.Status == EmailStatus.Sent
            && l.CreatedUtc > cutoff, ct);
    }
}
