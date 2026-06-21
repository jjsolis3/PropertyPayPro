using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Bills;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BillingService _billing;
    private readonly MailService _mail;

    public IndexModel(ApplicationDbContext db, BillingService billing, MailService mail)
    {
        _db = db;
        _billing = billing;
        _mail = mail;
    }

    public bool MailConfigured => _mail.IsConfigured;

    [BindProperty(SupportsGet = true)]
    public ChargeStatus? StatusFilter { get; set; }

    [BindProperty]
    public int GenerateYear { get; set; } = DateTime.UtcNow.Year;

    [BindProperty]
    public int GenerateMonth { get; set; } = DateTime.UtcNow.Month;

    public IList<RentalCharge> Charges { get; private set; } = new List<RentalCharge>();

    public decimal OutstandingTotal { get; private set; }
    public int OverdueCount { get; private set; }
    public decimal OverdueTotal { get; private set; }
    public decimal CollectedThisMonth { get; private set; }
    public int LateFeeCount { get; private set; }
    public decimal LateFeeTotal { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadChargesAsync();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        if (GenerateMonth < 1 || GenerateMonth > 12)
        {
            TempData["GenerateError"] = "Month must be 1–12.";
            return RedirectToPage();
        }

        var created = await _billing.GenerateChargesForPeriodAsync(GenerateYear, GenerateMonth);
        var periodLabel = new DateOnly(GenerateYear, GenerateMonth, 1).ToString("MMMM yyyy");
        TempData["GenerateMessage"] = created == 0
            ? $"No new bills generated for {periodLabel} (existing bills were skipped)."
            : $"Generated {created} new bill(s) for {periodLabel}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendAllStatementsAsync()
    {
        if (!_mail.IsConfigured)
        {
            TempData["GenerateError"] = "SMTP is not configured.";
            return RedirectToPage();
        }

        var charges = await _db.RentalCharges.Include(c => c.Allocations).ToListAsync();
        var leaseIds = charges.Where(c => c.Balance > 0).Select(c => c.LeaseId).Distinct().ToList();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        int sent = 0, skipped = 0, failed = 0;
        foreach (var leaseId in leaseIds)
        {
            try
            {
                var log = await _mail.SendStatementAsync(baseUrl, leaseId);
                if (log.Status == EmailStatus.Sent) sent++;
                else skipped++;
            }
            catch { failed++; }
        }
        TempData["GenerateMessage"] = $"Statements: {sent} sent, {skipped} skipped (no email/disabled), {failed} failed.";
        return RedirectToPage();
    }

    private async Task LoadChargesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);

        var allCharges = await _db.RentalCharges
            .Include(c => c.Lease).ThenInclude(l => l!.Property)
            .Include(c => c.Lease).ThenInclude(l => l!.Tenants)
            .Include(c => c.Allocations)
            .OrderByDescending(c => c.BillingPeriodStart)
            .ToListAsync();

        OutstandingTotal = allCharges.Sum(c => c.Balance);
        var overdue = allCharges.Where(c => c.Status == ChargeStatus.Overdue).ToList();
        OverdueCount = overdue.Count;
        OverdueTotal = overdue.Sum(c => c.Balance);
        CollectedThisMonth = await _db.RentPayments
            .Where(p => p.PaidOn >= firstOfMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var lateFees = allCharges.Where(c => c.Kind == ChargeKind.LateFee).ToList();
        LateFeeCount = lateFees.Count;
        LateFeeTotal = lateFees.Sum(c => c.AmountDue);

        Charges = StatusFilter.HasValue
            ? allCharges.Where(c => c.Status == StatusFilter.Value).ToList()
            : allCharges;
    }
}
