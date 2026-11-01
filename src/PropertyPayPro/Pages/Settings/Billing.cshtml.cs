using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Settings;

[Authorize(Roles = IdentitySeed.AdminRole)]
public class BillingModel : PageModel
{
    private readonly AppSettingsService _settings;
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly IAuditLog _audit;

    public BillingModel(AppSettingsService settings, ApplicationDbContext db, MailService mail, IAuditLog audit)
    {
        _settings = settings;
        _db = db;
        _mail = mail;
        _audit = audit;
    }

    [BindProperty, Range(1, 31)]
    public int DefaultRentDueDay { get; set; }

    [BindProperty, Range(0, 60)]
    public int DefaultLateFeeGraceDays { get; set; }

    [BindProperty, Range(0, 10_000)]
    public decimal DefaultLateFeeAmount { get; set; }

    [BindProperty] public bool AutoGenerateCharges { get; set; }

    [BindProperty, Range(1, 28)]
    public int AutoBillingDayOfMonth { get; set; } = 1;

    [BindProperty] public bool AutoApplyLateFees { get; set; }
    [BindProperty] public bool AutoEmailStatements { get; set; }
    [BindProperty] public bool AutoEmailAdminSummary { get; set; }

    public AppSettings Current { get; private set; } = new();
    public ScheduledTask? NextSweep { get; private set; }
    public ScheduledTask? LastSweep { get; private set; }
    public bool MailConfigured => _mail.IsConfigured;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        DefaultRentDueDay = Current.DefaultRentDueDay;
        DefaultLateFeeGraceDays = Current.DefaultLateFeeGraceDays;
        DefaultLateFeeAmount = Current.DefaultLateFeeAmount;
        AutoGenerateCharges = Current.AutoGenerateCharges;
        AutoBillingDayOfMonth = Current.AutoBillingDayOfMonth;
        AutoApplyLateFees = Current.AutoApplyLateFees;
        AutoEmailStatements = Current.AutoEmailStatements;
        AutoEmailAdminSummary = Current.AutoEmailAdminSummary;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();

        var automationBefore = Describe(Current);
        Current.DefaultRentDueDay = DefaultRentDueDay;
        Current.DefaultLateFeeGraceDays = DefaultLateFeeGraceDays;
        Current.DefaultLateFeeAmount = DefaultLateFeeAmount;
        Current.AutoGenerateCharges = AutoGenerateCharges;
        Current.AutoBillingDayOfMonth = AutoBillingDayOfMonth;
        Current.AutoApplyLateFees = AutoApplyLateFees;
        Current.AutoEmailStatements = AutoEmailStatements;
        Current.AutoEmailAdminSummary = AutoEmailAdminSummary;
        await _settings.SaveAsync(Current);

        var automationAfter = Describe(Current);
        if (automationAfter != automationBefore)
        {
            await _audit.WriteAsync("BillingAutomationChanged", nameof(AppSettings), Current.Id,
                $"{automationBefore} -> {automationAfter}");
        }

        TempData["Message"] = "Billing settings saved.";
        return RedirectToPage();
    }

    private static string Describe(AppSettings s) =>
        $"bills={(s.AutoGenerateCharges ? $"on (day {s.AutoBillingDayOfMonth})" : "off")}, " +
        $"lateFees={(s.AutoApplyLateFees ? "on" : "off")}, " +
        $"statements={(s.AutoEmailStatements ? "on" : "off")}, " +
        $"adminSummary={(s.AutoEmailAdminSummary ? "on" : "off")}";

    private async Task LoadAsync()
    {
        Current = await _settings.GetAsync();
        NextSweep = await _db.ScheduledTasks
            .Where(t => t.Kind == ScheduledTaskKind.BillingSweep && t.Status == ScheduledTaskStatus.Pending)
            .OrderBy(t => t.RunAtUtc)
            .FirstOrDefaultAsync();
        LastSweep = await _db.ScheduledTasks
            .Where(t => t.Kind == ScheduledTaskKind.BillingSweep
                && (t.Status == ScheduledTaskStatus.Completed || t.Status == ScheduledTaskStatus.Failed))
            .OrderByDescending(t => t.CompletedUtc)
            .FirstOrDefaultAsync();
    }
}
