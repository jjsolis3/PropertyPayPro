using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PropertyPayPro.Data;
using PropertyPayPro.Models;
using PropertyPayPro.Services;

namespace PropertyPayPro.Pages.Receipts;

[Authorize]
public class ShowModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mail;

    public ShowModel(ApplicationDbContext db, MailService mail)
    {
        _db = db;
        _mail = mail;
    }

    public RentPayment? Payment { get; private set; }
    public bool MailConfigured => _mail.IsConfigured;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Payment = await _db.RentPayments
            .Include(p => p.Lease).ThenInclude(l => l!.Property)
            .Include(p => p.Lease).ThenInclude(l => l!.Tenants)
            .Include(p => p.Allocations).ThenInclude(a => a.RentalCharge)
            .FirstOrDefaultAsync(p => p.Id == id);
        return Payment is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostEmailAsync(int id)
    {
        if (!_mail.IsConfigured)
        {
            TempData["EmailStatus"] = "SMTP is not configured.";
            return RedirectToPage(new { id });
        }
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var log = await _mail.SendReceiptAsync(baseUrl, id);
            TempData["EmailStatus"] = log.Status == EmailStatus.Sent
                ? $"Receipt emailed to {log.ToAddress}."
                : $"Receipt not emailed: {log.Error}";
        }
        catch (Exception ex)
        {
            TempData["EmailStatus"] = $"Receipt not emailed: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }
}
