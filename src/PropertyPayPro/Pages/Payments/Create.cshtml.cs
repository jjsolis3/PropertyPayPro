using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Payments;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BillingService _billing;
    private readonly MailService _mail;
    private readonly PdfService _pdf;

    public CreateModel(ApplicationDbContext db, BillingService billing, MailService mail, PdfService pdf)
    {
        _db = db;
        _billing = billing;
        _mail = mail;
        _pdf = pdf;
    }

    [BindProperty]
    public RentPayment Payment { get; set; } = new()
    {
        PaidOn = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    [BindProperty]
    public List<int> AllocationChargeIds { get; set; } = new();

    [BindProperty]
    public List<decimal> AllocationAmounts { get; set; } = new();

    public SelectList Leases { get; private set; } = default!;
    public List<RentalCharge> OutstandingCharges { get; private set; } = new();
    public List<decimal> SuggestedAmounts { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? leaseId)
    {
        if (leaseId is int id) Payment.LeaseId = id;
        await LoadLeasesAsync();
        await LoadOutstandingAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLeasesAsync();
            await LoadOutstandingAsync();
            return Page();
        }

        var totalAllocated = AllocationAmounts.Where(a => a > 0).Sum();
        if (totalAllocated > Payment.Amount)
        {
            ModelState.AddModelError(string.Empty,
                $"Total allocated ({totalAllocated:C}) exceeds payment amount ({Payment.Amount:C}).");
            await LoadLeasesAsync();
            await LoadOutstandingAsync();
            return Page();
        }

        _db.RentPayments.Add(Payment);
        await _db.SaveChangesAsync();

        var allocatedChargeIds = new List<int>();
        for (var i = 0; i < AllocationChargeIds.Count && i < AllocationAmounts.Count; i++)
        {
            if (AllocationAmounts[i] <= 0) continue;
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                RentPaymentId = Payment.Id,
                RentalChargeId = AllocationChargeIds[i],
                Amount = AllocationAmounts[i]
            });
            allocatedChargeIds.Add(AllocationChargeIds[i]);
        }
        await _db.SaveChangesAsync();

        // Generate the receipt PDF (always, even if email isn't configured) — it's the
        // archival record. MailService re-uses it as the email attachment.
        try { await _pdf.GenerateReceiptAsync(Payment.Id); }
        catch (Exception ex) { TempData["PdfStatus"] = $"Receipt PDF not generated: {ex.Message}"; }

        // For every charge this payment touched, if it's now fully paid, generate the
        // "Bill Paid in Full" confirmation PDF.
        foreach (var chargeId in allocatedChargeIds.Distinct())
        {
            try { await _pdf.GenerateBillPaidConfirmationIfClosedAsync(chargeId); }
            catch { /* don't fail save if confirmation PDF fails */ }
        }

        if (_mail.IsConfigured)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            try
            {
                var log = await _mail.SendReceiptAsync(baseUrl, Payment.Id);
                TempData["EmailStatus"] = log.Status == EmailStatus.Sent
                    ? $"Receipt emailed to {log.ToAddress}."
                    : $"Receipt not emailed: {log.Error}";
            }
            catch (Exception ex)
            {
                TempData["EmailStatus"] = $"Receipt not emailed: {ex.Message}";
            }
        }

        return RedirectToPage("/Receipts/Show", new { id = Payment.Id });
    }

    private async Task LoadLeasesAsync()
    {
        var leases = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync();

        Leases = new SelectList(
            leases.Select(l => new { l.Id, Label = $"{l.TenantNames} — {l.Property!.Name}" }),
            "Id", "Label");
    }

    private async Task LoadOutstandingAsync()
    {
        if (Payment.LeaseId == 0) return;

        var charges = await _db.RentalCharges
            .Where(c => c.LeaseId == Payment.LeaseId)
            .Include(c => c.Allocations)
            .OrderBy(c => c.BillingPeriodStart)
            .ToListAsync();

        OutstandingCharges = charges.Where(c => c.Balance > 0).ToList();

        var remaining = Payment.Amount;
        foreach (var c in OutstandingCharges)
        {
            if (remaining <= 0)
            {
                SuggestedAmounts.Add(0m);
                continue;
            }
            var apply = Math.Min(c.Balance, remaining);
            SuggestedAmounts.Add(apply);
            remaining -= apply;
        }
    }
}
