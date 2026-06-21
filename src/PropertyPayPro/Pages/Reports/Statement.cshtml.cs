using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Reports;

[Authorize]
public class StatementModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;
    private readonly PdfService _pdf;

    public StatementModel(ApplicationDbContext db, MailService mail, PdfService pdf)
    {
        _db = db;
        _mail = mail;
        _pdf = pdf;
    }

    public Lease? Lease { get; private set; }
    public List<RentalCharge> UnpaidCharges { get; private set; } = new();
    public DateOnly ReportDate { get; private set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public bool MailConfigured => _mail.IsConfigured;
    public GeneratedDocument? LatestStatementPdf { get; private set; }

    public decimal TotalDue => UnpaidCharges.Sum(c => c.AmountDue);
    public decimal TotalPaid => UnpaidCharges.Sum(c => c.AmountPaid);
    public decimal TotalBalance => UnpaidCharges.Sum(c => c.Balance);

    public async Task<IActionResult> OnGetAsync(int leaseId)
    {
        await LoadAsync(leaseId);
        return Lease is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostSendAsync(int leaseId)
    {
        if (!_mail.IsConfigured)
        {
            TempData["EmailStatus"] = "SMTP is not configured.";
            return RedirectToPage(new { leaseId });
        }
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var log = await _mail.SendStatementAsync(baseUrl, leaseId);
            TempData["EmailStatus"] = log.Status == EmailStatus.Sent
                ? $"Statement emailed to {log.ToAddress}."
                : $"Statement not emailed: {log.Error}";
        }
        catch (Exception ex)
        {
            TempData["EmailStatus"] = $"Statement not emailed: {ex.Message}";
        }
        return RedirectToPage(new { leaseId });
    }

    public async Task<IActionResult> OnPostGeneratePdfAsync(int leaseId)
    {
        try
        {
            await _pdf.GenerateUnpaidStatementAsync(leaseId);
            TempData["EmailStatus"] = "Statement PDF generated.";
        }
        catch (Exception ex)
        {
            TempData["EmailStatus"] = $"PDF generation failed: {ex.Message}";
        }
        return RedirectToPage(new { leaseId });
    }

    private async Task LoadAsync(int leaseId)
    {
        Lease = await _db.Leases
            .Include(l => l.Property)
            .Include(l => l.Tenants)
            .Include(l => l.Charges).ThenInclude(c => c.Allocations)
            .FirstOrDefaultAsync(l => l.Id == leaseId);
        if (Lease is null) return;
        UnpaidCharges = Lease.Charges.Where(c => c.Balance > 0).OrderBy(c => c.DueDate).ToList();

        LatestStatementPdf = await _db.GeneratedDocuments
            .Where(d => d.LeaseId == leaseId && d.Kind == GeneratedDocumentKind.UnpaidStatement)
            .OrderByDescending(d => d.CreatedUtc)
            .FirstOrDefaultAsync();
    }
}
