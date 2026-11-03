using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;
using PropertyPayPro.Services.ScheduledTasks;

namespace PropertyPayPro.Pages.Settings;

/// <summary>
/// Settings > Automation: the one place to switch each automatic email/task
/// on or off, see when it runs next and what it last did, and run it now.
/// </summary>
public class SchedulerModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditLog _audit;
    private readonly AppSettingsService _settings;
    private readonly MailService _mail;

    public SchedulerModel(ApplicationDbContext db, IAuditLog audit, AppSettingsService settings, MailService mail)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _mail = mail;
    }

    [BindProperty] public bool AutoLateRentReminders { get; set; }
    [BindProperty] public bool AutoLeaseRenewalReminders { get; set; }
    [BindProperty] public bool AutoInspectionReminders { get; set; }
    [BindProperty] public bool AutoGenerateCharges { get; set; }
    [BindProperty, Range(1, 28)] public int AutoBillingDayOfMonth { get; set; } = 1;
    [BindProperty] public bool AutoApplyLateFees { get; set; }
    [BindProperty] public bool AutoEmailStatements { get; set; }
    [BindProperty] public bool AutoEmailAdminSummary { get; set; }

    public record Job(ScheduledTaskKind Kind, string Name, string SwitchName, bool Enabled,
        ScheduledTask? Next, ScheduledTask? Last);

    public List<Job> Jobs { get; private set; } = new();
    public List<ScheduledTask> Recent { get; private set; } = new();
    public bool MailConfigured => _mail.IsConfigured;

    public static string Label(ScheduledTaskKind kind) => kind switch
    {
        ScheduledTaskKind.LateRentReminder => "Late rent reminders",
        ScheduledTaskKind.LeaseRenewalReminder => "Lease renewal reminders",
        ScheduledTaskKind.InspectionReminder => "Inspection reminders",
        ScheduledTaskKind.BillingSweep => "Billing",
        ScheduledTaskKind.NoticeSend => "Scheduled notice",
        _ => kind.ToString(),
    };

    public async Task OnGetAsync()
    {
        var s = await _settings.GetAsync();
        AutoLateRentReminders = s.AutoLateRentReminders;
        AutoLeaseRenewalReminders = s.AutoLeaseRenewalReminders;
        AutoInspectionReminders = s.AutoInspectionReminders;
        AutoGenerateCharges = s.AutoGenerateCharges;
        AutoBillingDayOfMonth = s.AutoBillingDayOfMonth;
        AutoApplyLateFees = s.AutoApplyLateFees;
        AutoEmailStatements = s.AutoEmailStatements;
        AutoEmailAdminSummary = s.AutoEmailAdminSummary;
        await LoadAsync(s);
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var s = await _settings.GetAsync();
        if (!ModelState.IsValid)
        {
            await LoadAsync(s);
            return Page();
        }

        var before = Describe(s);
        s.AutoLateRentReminders = AutoLateRentReminders;
        s.AutoLeaseRenewalReminders = AutoLeaseRenewalReminders;
        s.AutoInspectionReminders = AutoInspectionReminders;
        s.AutoGenerateCharges = AutoGenerateCharges;
        s.AutoBillingDayOfMonth = AutoBillingDayOfMonth;
        s.AutoApplyLateFees = AutoApplyLateFees;
        s.AutoEmailStatements = AutoEmailStatements;
        s.AutoEmailAdminSummary = AutoEmailAdminSummary;
        await _settings.SaveAsync(s);

        var after = Describe(s);
        if (after != before)
        {
            await _audit.WriteAsync("AutomationChanged", nameof(AppSettings), AppSettingsService.SingletonId,
                $"{before} -> {after}");
        }
        TempData["Message"] = "Automation settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunNowAsync(int kind)
    {
        if (!Enum.IsDefined(typeof(ScheduledTaskKind), kind)) return BadRequest();
        var k = (ScheduledTaskKind)kind;

        if (!IsEnabled(k, await _settings.GetAsync()))
        {
            TempData["Error"] = $"{Label(k)} is switched off. Turn it on and save before running it.";
            return RedirectToPage();
        }

        // Pull the next queued run forward to now; queue one if none exists.
        var now = DateTime.UtcNow;
        var next = await _db.ScheduledTasks
            .Where(t => t.Kind == k && t.Status == ScheduledTaskStatus.Pending)
            .OrderBy(t => t.RunAtUtc)
            .FirstOrDefaultAsync();
        if (next is not null)
        {
            next.RunAtUtc = now;
        }
        else
        {
            _db.ScheduledTasks.Add(new ScheduledTask
            {
                Kind = k,
                Status = ScheduledTaskStatus.Pending,
                RunAtUtc = now,
                PayloadJson = "{}",
                Description = $"Admin-triggered {Label(k)} run",
                CreatedUtc = now,
            });
        }
        await _db.SaveChangesAsync();
        await _audit.WriteAsync("ScheduledTaskNudged", nameof(ScheduledTask), next?.Id, $"Kind={k}; now runnable");

        TempData["Message"] = $"{Label(k)} will run within about a minute. Refresh to see the result.";
        return RedirectToPage();
    }

    private static bool IsEnabled(ScheduledTaskKind kind, AppSettings s) => kind switch
    {
        ScheduledTaskKind.LateRentReminder => s.AutoLateRentReminders,
        ScheduledTaskKind.LeaseRenewalReminder => s.AutoLeaseRenewalReminders,
        ScheduledTaskKind.InspectionReminder => s.AutoInspectionReminders,
        ScheduledTaskKind.BillingSweep => s.AutoGenerateCharges || s.AutoApplyLateFees,
        _ => true,
    };

    private static string Describe(AppSettings s) =>
        $"lateRent={OnOff(s.AutoLateRentReminders)}, renewal={OnOff(s.AutoLeaseRenewalReminders)}, " +
        $"inspection={OnOff(s.AutoInspectionReminders)}, " +
        $"bills={(s.AutoGenerateCharges ? $"on (day {s.AutoBillingDayOfMonth})" : "off")}, " +
        $"lateFees={OnOff(s.AutoApplyLateFees)}, statements={OnOff(s.AutoEmailStatements)}, " +
        $"adminSummary={OnOff(s.AutoEmailAdminSummary)}";

    private static string OnOff(bool b) => b ? "on" : "off";

    private async Task LoadAsync(AppSettings s)
    {
        ViewData["SettingsTab"] = "scheduler";

        var jobs = new (ScheduledTaskKind Kind, string Switch)[]
        {
            (ScheduledTaskKind.LateRentReminder, nameof(AutoLateRentReminders)),
            (ScheduledTaskKind.LeaseRenewalReminder, nameof(AutoLeaseRenewalReminders)),
            (ScheduledTaskKind.InspectionReminder, nameof(AutoInspectionReminders)),
            (ScheduledTaskKind.BillingSweep, ""),
        };
        foreach (var (kind, switchName) in jobs)
        {
            var next = await _db.ScheduledTasks
                .Where(t => t.Kind == kind
                    && (t.Status == ScheduledTaskStatus.Pending || t.Status == ScheduledTaskStatus.Running))
                .OrderBy(t => t.RunAtUtc)
                .FirstOrDefaultAsync();
            var last = await _db.ScheduledTasks
                .Where(t => t.Kind == kind
                    && (t.Status == ScheduledTaskStatus.Completed || t.Status == ScheduledTaskStatus.Failed))
                .OrderByDescending(t => t.CompletedUtc)
                .FirstOrDefaultAsync();
            Jobs.Add(new Job(kind, Label(kind), switchName, IsEnabled(kind, s), next, last));
        }

        Recent = await _db.ScheduledTasks
            .OrderByDescending(t => t.CreatedUtc)
            .Take(50)
            .ToListAsync();
    }
}
