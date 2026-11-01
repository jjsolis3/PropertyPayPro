using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;

namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Daily sweep: send a reminder for any inspection scheduled in the
/// next 48 hours that isn't already Completed. Dedupe window: 24 hours
/// per inspection, keyed on the inspection's LeaseId since EmailLog
/// doesn't carry an InspectionId column (keeping the schema minimal).
/// </summary>
public class InspectionReminderHandler : IScheduledTaskHandler
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly NoticeTemplateService _tpl;
    private readonly AppSettingsService _settings;
    private readonly ILogger<InspectionReminderHandler> _logger;

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(24);
    private const int LookaheadDays = 2;

    public InspectionReminderHandler(
        ApplicationDbContext db,
        MailService mail,
        NoticeTemplateService tpl,
        AppSettingsService settings,
        ILogger<InspectionReminderHandler> logger)
    {
        _db = db;
        _mail = mail;
        _tpl = tpl;
        _settings = settings;
        _logger = logger;
    }

    public ScheduledTaskKind Kind => ScheduledTaskKind.InspectionReminder;

    public async Task HandleAsync(ScheduledTask task, CancellationToken ct)
    {
        try
        {
            if (!_mail.IsConfigured)
            {
                _logger.LogInformation("Inspection reminder: SMTP not configured — skipping.");
                return;
            }
            var template = await _tpl.GetBySlugAsync(
                NoticeTemplateScope.SystemReminder,
                NoticeTemplateSeeder.Slugs.InspectionReminderEmail, ct);
            if (template is null)
            {
                _logger.LogWarning("Inspection reminder: template missing — skipping.");
                return;
            }

            var baseUrl = await _settings.GetPublicBaseUrlAsync(ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var window = today.AddDays(LookaheadDays);

            var inspections = await _db.Inspections
                .Include(i => i.Lease).ThenInclude(l => l!.Property)
                .Include(i => i.Lease).ThenInclude(l => l!.Tenants)
                .Where(i => i.Status != InspectionStatus.Completed
                            && i.ScheduledFor >= today
                            && i.ScheduledFor <= window)
                .ToListAsync(ct);

            var sent = 0;
            var skipped = 0;
            foreach (var insp in inspections)
            {
                var lease = insp.Lease;
                if (lease is null) { skipped++; continue; }

                var recipients = lease.Tenants
                    .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                    .Select(t => t.Email!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (recipients.Count == 0) { skipped++; continue; }

                if (await WasRecentlyRemindedAsync(lease.Id, ct)) { skipped++; continue; }

                var rendered = _tpl.Render(template, new TemplateContext(
                    Lease: lease,
                    InspectionDate: insp.ScheduledFor,
                    InspectionKind: insp.Kind.ToString()));

                foreach (var addr in recipients)
                {
                    var log = await _mail.SendReminderAsync(baseUrl,
                        EmailKind.InspectionReminder, addr,
                        rendered.TitleOrSubject, rendered.Body,
                        leaseId: lease.Id, ct: ct);
                    if (log.Status == EmailStatus.Sent) sent++;
                }
            }

            task.Description = $"Inspection reminder: {sent} email(s) sent across {inspections.Count} upcoming inspection(s); {skipped} skipped.";
            _logger.LogInformation("{Desc}", task.Description);
        }
        finally
        {
            RecurringTaskHelpers.EnqueueNextRun(_db, Kind,
                "Daily inspection reminder sweep");
        }
    }

    private async Task<bool> WasRecentlyRemindedAsync(int leaseId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - DedupeWindow;
        return await _db.EmailLogs.AnyAsync(l =>
            l.LeaseId == leaseId
            && l.Kind == EmailKind.InspectionReminder
            && l.Status == EmailStatus.Sent
            && l.CreatedUtc > cutoff, ct);
    }
}
